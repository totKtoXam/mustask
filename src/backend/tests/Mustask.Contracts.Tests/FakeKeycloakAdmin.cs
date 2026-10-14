using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Mustask.AppHost.Provisioning;

namespace Mustask.Contracts.Tests;

/// <summary>
/// A write-capable Keycloak admin surface over an in-memory realm (plan-backend § 4.7): everything the drift check reads
/// (<see cref="KeycloakStandDriftCheck"/>) and everything the live-realm migration reads and writes
/// (<see cref="KeycloakLiveRealmMigration"/>), with Keycloak's own consequences of the writes — deleting a realm role ends every
/// mapping of it on users and groups and every composite that held it, deleting a group ends its members' membership of it and
/// deletes its subgroups. It records every request, can fail the k-th mutating request (before or after it takes effect, the way a
/// dropped connection does), and takes a <see cref="Snapshot"/> of the whole realm so that a test compares "everything else" by
/// value. No Docker, no Keycloak: it mirrors the admin API the code expects, not Keycloak's real behaviour — which the opt-in live
/// test (<c>LiveRealmMigrationTests</c>) and the task-stand trial prove (plan-backend R-7).
/// </summary>
internal sealed class FakeKeycloakAdmin : HttpMessageHandler
{
    public const string Realm = "mustask";

    public const string AdminPassword = "fake-admin-password-not-real";

    /// <summary>What a token request answers with — recognizable, so that a test can look for it in the report.</summary>
    public const string AccessToken = "fake-admin-access-token-not-real";

    public const string ResourceClient = "mustask-api";

    public bool RealmExists { get; set; } = true;

    /// <summary>The master realm administrator's password the fake accepts.</summary>
    public string AcceptedPassword { get; set; } = AdminPassword;

    public Dictionary<string, FakeRole> RealmRoles { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, FakeClientState> Clients { get; } = new(StringComparer.Ordinal);

    public List<FakeGroup> TopGroups { get; } = [];

    public Dictionary<string, FakeUser> Users { get; } = new(StringComparer.Ordinal);

    public List<string> Listeners { get; } = [];

    public string DefaultRole { get; set; } = "default-roles-mustask";

    /// <summary>Every request, as "METHOD path-and-query" (ids resolved).</summary>
    public List<string> Requests { get; } = [];

    /// <summary>How many mutating requests took effect or were attempted so far.</summary>
    public int Mutations { get; private set; }

    /// <summary>The 1-based number of the mutating request to fail, or <c>null</c>.</summary>
    public int? FailMutation { get; set; }

    /// <summary>Whether the failed mutating request first takes effect (a lost answer) or does not (a refused request).</summary>
    public bool FailedMutationTakesEffect { get; set; }

    /// <summary>Called before each <c>DELETE</c> is applied (the grant-before-removal check of the migration runs here).</summary>
    public Action<FakeKeycloakAdmin>? BeforeDelete { get; set; }

    /// <summary>The headers of the admin requests that carried none of the expected token (a leak check reads them).</summary>
    public List<string> AuthorizationHeaders { get; } = [];

    // ---- building the realm ----------------------------------------------------------------------------------------------

    /// <summary>A realm as an import declares it, plus Keycloak's own: the two built-ins, the default role, the account client.</summary>
    public static FakeKeycloakAdmin FromImport(KeycloakRealmImport import, string panelSecret)
    {
        var realm = new FakeKeycloakAdmin();
        realm.Listeners.AddRange(import.EventsListeners);

        foreach (var client in import.Clients.Values)
        {
            realm.Clients[client.ClientId] = new FakeClientState(
                "id-" + client.ClientId,
                client.Roles.ToDictionary(role => role, role => "role-" + client.ClientId + "-" + role, StringComparer.Ordinal),
                client.RootUrl,
                client.RedirectUris.ToList(),
                client.PostLogoutRedirectUris,
                client.HasSecret ? panelSecret : null);
        }

        realm.Clients["account"] = new FakeClientState(
            "id-account",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["view-profile"] = "role-account-view-profile", ["manage-account"] = "role-account-manage-account" },
            "${authBaseUrl}",
            ["/realms/mustask/account/*"],
            null,
            null);
        realm.Clients["mustask-e2e"] = new FakeClientState(
            "id-mustask-e2e", new Dictionary<string, string>(StringComparer.Ordinal) { ["something"] = "role-e2e-something" }, null, [], null, "e2e-secret");

        foreach (var declared in import.RealmRoles.Values)
        {
            var role = realm.AddRole(declared.Name);
            role.Composites.UnionWith(declared.RealmComposites.Select(name => new FakeComposite("realm", name)));

            foreach (var (clientId, names) in declared.ClientComposites)
            {
                role.Composites.UnionWith(names.Select(name => new FakeComposite(clientId, name)));
            }
        }

        // Keycloak creates these in every realm; an import that does not declare them (the pre-mod-016 one) still has them.
        foreach (var builtIn in KeycloakStandDriftCheck.BuiltInRealmRoles)
        {
            if (!realm.RealmRoles.ContainsKey(builtIn))
            {
                realm.AddRole(builtIn);
            }
        }

        if (!realm.RealmRoles.TryGetValue(realm.DefaultRole, out var defaultRole))
        {
            defaultRole = realm.AddRole(realm.DefaultRole);
            defaultRole.Composites.Add(new FakeComposite("realm", "offline_access"));
            defaultRole.Composites.Add(new FakeComposite("realm", "uma_authorization"));
            defaultRole.Composites.Add(new FakeComposite("account", "view-profile"));
            defaultRole.Composites.Add(new FakeComposite("account", "manage-account"));
        }

        foreach (var group in import.Groups)
        {
            realm.EnsureGroup(group.Path).RealmRoles.UnionWith(group.RealmRoles);
        }

        return realm;
    }

    public FakeRole AddRole(string name)
    {
        var role = new FakeRole("role-" + name, name);
        RealmRoles[name] = role;

        return role;
    }

    /// <summary>The group at <paramref name="path"/> (<c>/A/B</c>), creating the missing ones.</summary>
    public FakeGroup EnsureGroup(string path)
    {
        FakeGroup? parent = null;

        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var siblings = parent?.Children ?? TopGroups;
            var group = siblings.FirstOrDefault(candidate => candidate.Name == segment);

            if (group is null)
            {
                group = new FakeGroup("group-" + Guid.CreateVersion7().ToString("N"), segment, parent);
                siblings.Add(group);
            }

            parent = group;
        }

        return parent ?? throw new ArgumentException("A group path names at least one group.", nameof(path));
    }

    public FakeGroup? Group(string path) => AllGroups().FirstOrDefault(group => group.Path == path);

    public IEnumerable<FakeGroup> AllGroups()
    {
        var queue = new Queue<FakeGroup>(TopGroups);

        while (queue.Count > 0)
        {
            var group = queue.Dequeue();
            yield return group;

            foreach (var child in group.Children)
            {
                queue.Enqueue(child);
            }
        }
    }

    /// <summary>A user with a credential and attributes; <paramref name="roles"/> are the realm roles mapped on the user directly.</summary>
    public FakeUser AddUser(string username, string[]? roles = null, string[]? groups = null)
    {
        var user = new FakeUser("user-" + username, username)
        {
            Credential = "credential-of-" + username + "-not-real",
        };
        user.Attributes["locale"] = "ru";
        user.Attributes["department"] = "отдел-" + username;

        foreach (var role in roles ?? [])
        {
            user.RealmRoles.Add(role);
        }

        foreach (var path in groups ?? [])
        {
            EnsureGroup(path).Members.Add(user.Id);
        }

        Users[user.Id] = user;

        return user;
    }

    /// <summary>The users that hold the realm role directly or through a group (the roles' composites are not followed).</summary>
    public IEnumerable<FakeUser> HoldersOf(string role) =>
        Users.Values.Where(user =>
            user.RealmRoles.Contains(role)
            || AllGroups().Any(group => group.RealmRoles.Contains(role) && group.Members.Contains(user.Id)));

    // ---- the whole realm, by value -------------------------------------------------------------------------------------

    /// <summary>
    /// Everything a test compares as "the rest of the realm": roles and their composites, clients and their roles, URLs and secrets,
    /// groups with their mappings and members, users with their ids, credentials, attributes, role mappings and groups — one line
    /// each, sorted, so two snapshots diff line by line.
    /// </summary>
    public List<string> Snapshot()
    {
        var lines = new List<string>();

        foreach (var role in RealmRoles.Values)
        {
            lines.Add($"role {role.Name} composites=[{string.Join(", ", role.Composites.Select(c => c.Container + "/" + c.Name).Order(StringComparer.Ordinal))}]");
        }

        foreach (var (clientId, client) in Clients)
        {
            lines.Add(
                $"client {clientId} id={client.Id} roles=[{string.Join(", ", client.Roles.Keys.Order(StringComparer.Ordinal))}] "
                + $"root={client.RootUrl} redirects=[{string.Join(", ", client.RedirectUris)}] secret={client.Secret}");
        }

        foreach (var group in AllGroups())
        {
            lines.Add(
                $"group {group.Path} realm=[{string.Join(", ", group.RealmRoles.Order(StringComparer.Ordinal))}] "
                + $"members=[{string.Join(", ", group.Members.Select(id => Users[id].Username).Order(StringComparer.Ordinal))}]");
        }

        foreach (var user in Users.Values)
        {
            lines.Add(
                $"user {user.Username} id={user.Id} enabled={user.Enabled} credential={user.Credential} "
                + $"attributes=[{string.Join(", ", user.Attributes.OrderBy(a => a.Key, StringComparer.Ordinal).Select(a => a.Key + "=" + a.Value))}] "
                + $"roles=[{string.Join(", ", user.RealmRoles.Order(StringComparer.Ordinal))}]");
        }

        lines.Add($"default-role {DefaultRole}");
        lines.Add($"listeners [{string.Join(", ", Listeners)}]");

        return [.. lines.Order(StringComparer.Ordinal)];
    }

    // ---- the HTTP surface -------------------------------------------------------------------------------------------------

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
        Requests.Add($"{request.Method} {path}{request.RequestUri.Query}");

        if (request.Method == HttpMethod.Get && path == "/realms/mustask")
        {
            return new HttpResponseMessage(RealmExists ? HttpStatusCode.OK : HttpStatusCode.NotFound);
        }

        if (request.Method == HttpMethod.Post && path == "/realms/master/protocol/openid-connect/token")
        {
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);

            return form.Contains("password=" + Uri.EscapeDataString(AcceptedPassword), StringComparison.Ordinal)
                ? Json(new JsonObject { ["access_token"] = AccessToken })
                : new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        var authorization = request.Headers.Authorization?.ToString();
        Assert.Equal("Bearer " + AccessToken, authorization);

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString).ToArray();

        if (segments is not ["admin", "realms", Realm, ..] || !RealmExists)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken));

        if (request.Method != HttpMethod.Get)
        {
            if (request.Method == HttpMethod.Delete)
            {
                BeforeDelete?.Invoke(this);
            }

            Mutations++;

            if (FailMutation == Mutations)
            {
                if (FailedMutationTakesEffect)
                {
                    Handle(request.Method, segments[3..], query, body);
                }

                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        return Handle(request.Method, segments[3..], query, body);
    }

    private HttpResponseMessage Handle(HttpMethod method, string[] route, System.Collections.Specialized.NameValueCollection query, JsonNode? body)
    {
        var first = int.TryParse(query["first"], out var f) ? f : 0;
        var max = int.TryParse(query["max"], out var m) ? m : int.MaxValue;

        IEnumerable<T> Page<T>(IEnumerable<T> items) => items.Skip(first).Take(max);

        if (method == HttpMethod.Get)
        {
            switch (route)
            {
                case []:
                    return Json(new JsonObject
                    {
                        ["realm"] = Realm,
                        ["eventsListeners"] = Array(Listeners),
                        ["defaultRole"] = new JsonObject { ["name"] = DefaultRole },
                    });
                case ["clients"]:
                    return Json(new JsonArray(Clients
                        .Where(pair => query["clientId"] is not { } wanted || pair.Key == wanted)
                        .Select(pair => (JsonNode)ClientJson(pair.Key, pair.Value)).ToArray()));
                case ["clients", var id, "roles"]:
                    return Json(RolesJson(ClientById(id).Roles.Keys));
                case ["clients", var id, "roles", var name]:
                    return ClientById(id).Roles.TryGetValue(name, out var roleId)
                        ? Json(new JsonObject { ["id"] = roleId, ["name"] = name, ["clientRole"] = true, ["containerId"] = id })
                        : new HttpResponseMessage(HttpStatusCode.NotFound);
                case ["clients", var id, "client-secret"]:
                    return Json(new JsonObject { ["value"] = ClientById(id).Secret });
                case ["roles"]:
                    return Json(new JsonArray(Page(RealmRoles.Values.OrderBy(role => role.Name, StringComparer.Ordinal)).Select(role => (JsonNode)RoleJson(role)).ToArray()));
                case ["roles", var name]:
                    return RealmRoles.TryGetValue(name, out var found) ? Json(RoleJson(found)) : new HttpResponseMessage(HttpStatusCode.NotFound);
                case ["roles", var name, "composites"]:
                    return RealmRoles.TryGetValue(name, out var holder)
                        ? Json(new JsonArray(Page(holder.Composites.OrderBy(c => c.Container + "/" + c.Name, StringComparer.Ordinal)).Select(c => (JsonNode)CompositeJson(c)).ToArray()))
                        : new HttpResponseMessage(HttpStatusCode.NotFound);
                case ["roles", var name, "users"]:
                    return Json(new JsonArray(Page(Users.Values.Where(user => user.RealmRoles.Contains(name)).OrderBy(user => user.Username, StringComparer.Ordinal))
                        .Select(user => (JsonNode)new JsonObject { ["id"] = user.Id, ["username"] = user.Username }).ToArray()));
                case ["groups"]:
                    return Json(new JsonArray(Page(TopGroups).Select(group => (JsonNode)GroupJson(group)).ToArray()));
                case ["groups", var id, "children"]:
                    return Json(new JsonArray(Page(GroupById(id).Children).Select(group => (JsonNode)GroupJson(group)).ToArray()));
                case ["groups", var id, "role-mappings", "realm"]:
                    return Json(RolesJson(GroupById(id).RealmRoles));
                case ["groups", var id, "role-mappings"]:
                    return Json(new JsonObject { ["realmMappings"] = RolesJson(GroupById(id).RealmRoles), ["clientMappings"] = new JsonObject() });
                case ["group-by-path", .. var groupPath]:
                    return Group("/" + string.Join('/', groupPath)) is { } byPath
                        ? Json(new JsonObject { ["id"] = byPath.Id, ["name"] = byPath.Name, ["path"] = byPath.Path })
                        : new HttpResponseMessage(HttpStatusCode.NotFound);
                case ["users", var id, "role-mappings", "realm"]:
                    return Json(RolesJson(Users[id].RealmRoles));
            }
        }
        else if (method == HttpMethod.Post)
        {
            switch (route)
            {
                case ["roles"]:
                {
                    var name = (string)body!["name"]!;

                    if (RealmRoles.ContainsKey(name))
                    {
                        return new HttpResponseMessage(HttpStatusCode.Conflict);
                    }

                    AddRole(name).Description = (string?)body["description"];

                    return new HttpResponseMessage(HttpStatusCode.Created);
                }

                case ["roles", var name, "composites"]:
                {
                    foreach (var node in (JsonArray)body!)
                    {
                        RealmRoles[name].Composites.Add(CompositeOf((string)node!["name"]!, (string)node["id"]!));
                    }

                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }

                case ["groups", var id, "role-mappings", "realm"]:
                    GroupById(id).RealmRoles.UnionWith(((JsonArray)body!).Select(node => (string)node!["name"]!));

                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                case ["users", var id, "role-mappings", "realm"]:
                    Users[id].RealmRoles.UnionWith(((JsonArray)body!).Select(node => (string)node!["name"]!));

                    return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
        }
        else if (method == HttpMethod.Delete)
        {
            switch (route)
            {
                case ["roles", var name]:
                    if (!RealmRoles.Remove(name))
                    {
                        return new HttpResponseMessage(HttpStatusCode.NotFound);
                    }

                    // Keycloak ends every mapping and every composite of a deleted role.
                    foreach (var user in Users.Values)
                    {
                        user.RealmRoles.Remove(name);
                    }

                    foreach (var group in AllGroups())
                    {
                        group.RealmRoles.Remove(name);
                    }

                    foreach (var role in RealmRoles.Values)
                    {
                        role.Composites.RemoveWhere(c => c.Container == "realm" && c.Name == name);
                    }

                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                case ["groups", var id]:
                {
                    var group = AllGroups().FirstOrDefault(candidate => candidate.Id == id);

                    if (group is null)
                    {
                        return new HttpResponseMessage(HttpStatusCode.NotFound);
                    }

                    (group.Parent?.Children ?? TopGroups).Remove(group);

                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }
            }
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private FakeClientState ClientById(string id) => Clients.Values.Single(client => client.Id == id);

    private FakeGroup GroupById(string id) => AllGroups().Single(group => group.Id == id);

    private FakeComposite CompositeOf(string name, string id)
    {
        // A client role is told by its id (the fake gives client roles "role-<client>-<name>").
        foreach (var (clientId, client) in Clients)
        {
            if (client.Roles.TryGetValue(name, out var roleId) && roleId == id)
            {
                return new FakeComposite(clientId, name);
            }
        }

        return new FakeComposite("realm", name);
    }

    private JsonObject RoleJson(FakeRole role) => new()
    {
        ["id"] = role.Id,
        ["name"] = role.Name,
        ["description"] = role.Description,
        ["composite"] = role.Composites.Count > 0,
        ["clientRole"] = false,
        ["containerId"] = "realm-id",
    };

    private JsonObject CompositeJson(FakeComposite composite) => composite.Container == "realm"
        ? new JsonObject { ["id"] = RealmRoles.TryGetValue(composite.Name, out var realmRole) ? realmRole.Id : "role-" + composite.Name, ["name"] = composite.Name, ["clientRole"] = false, ["containerId"] = "realm-id" }
        : new JsonObject { ["id"] = Clients[composite.Container].Roles[composite.Name], ["name"] = composite.Name, ["clientRole"] = true, ["containerId"] = Clients[composite.Container].Id };

    private static JsonObject GroupJson(FakeGroup group) => new()
    {
        ["id"] = group.Id,
        ["name"] = group.Name,
        ["path"] = group.Path,
        ["subGroupCount"] = group.Children.Count,
    };

    private static JsonObject ClientJson(string clientId, FakeClientState client)
    {
        var json = new JsonObject
        {
            ["id"] = client.Id,
            ["clientId"] = clientId,
            ["rootUrl"] = client.RootUrl,
            ["redirectUris"] = Array(client.RedirectUris),
        };

        if (client.PostLogout is not null)
        {
            json["attributes"] = new JsonObject { ["post.logout.redirect.uris"] = client.PostLogout };
        }

        return json;
    }

    private static JsonArray RolesJson(IEnumerable<string> names) =>
        new(names.Select(name => (JsonNode)new JsonObject { ["id"] = "role-" + name, ["name"] = name }).ToArray());

    private static JsonArray Array(IEnumerable<string> values) =>
        new(values.Select(value => (JsonNode)JsonValue.Create(value)!).ToArray());

    private static HttpResponseMessage Json(JsonNode body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
}

internal sealed class FakeRole(string id, string name)
{
    public string Id { get; } = id;

    public string Name { get; } = name;

    public string? Description { get; set; }

    public HashSet<FakeComposite> Composites { get; } = [];
}

/// <summary>A composite of a realm role: a realm role (<c>realm</c>) or a role of a client.</summary>
internal sealed record FakeComposite(string Container, string Name);

internal sealed class FakeClientState(string id, Dictionary<string, string> roles, string? rootUrl, List<string> redirectUris, string? postLogout, string? secret)
{
    public string Id { get; } = id;

    /// <summary>Role name to role id.</summary>
    public Dictionary<string, string> Roles { get; } = roles;

    public string? RootUrl { get; } = rootUrl;

    public List<string> RedirectUris { get; } = redirectUris;

    public string? PostLogout { get; } = postLogout;

    public string? Secret { get; } = secret;
}

internal sealed class FakeGroup(string id, string name, FakeGroup? parent)
{
    public string Id { get; } = id;

    public string Name { get; } = name;

    public FakeGroup? Parent { get; } = parent;

    public List<FakeGroup> Children { get; } = [];

    public HashSet<string> RealmRoles { get; } = [];

    /// <summary>User ids.</summary>
    public HashSet<string> Members { get; } = [];

    public string Path => (Parent?.Path ?? string.Empty) + "/" + Name;
}

internal sealed class FakeUser(string id, string username)
{
    public string Id { get; } = id;

    public string Username { get; } = username;

    public bool Enabled { get; set; } = true;

    public string? Credential { get; set; }

    public Dictionary<string, string> Attributes { get; } = new(StringComparer.Ordinal);

    /// <summary>The realm roles mapped on the user directly.</summary>
    public HashSet<string> RealmRoles { get; } = [];
}
