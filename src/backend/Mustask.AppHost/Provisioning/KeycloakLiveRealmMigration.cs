using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace Mustask.AppHost.Provisioning;

/// <summary>What <see cref="KeycloakLiveRealmMigration"/> needs: where the realm is and who signs in.</summary>
public sealed record KeycloakLiveRealmMigrationOptions
{
    /// <summary>The only realm the command touches.</summary>
    public string Realm { get; init; } = "mustask";

    /// <summary>The master realm's administrator — always <c>admin</c> on this stand (<c>AppHost.cs</c>).</summary>
    public string AdminUsername { get; init; } = "admin";

    /// <summary>The master realm administrator's password, from <c>KEYCLOAK_ADMIN_PASSWORD</c>. Sent once in a form body, never printed.</summary>
    public required string AdminPassword { get; init; }
}

/// <summary>A request to the admin surface that did not succeed: what was asked and the status, never a header, a body or a URL with an id.</summary>
public sealed class KeycloakRequestFailedException(string method, string routeTemplate, int status)
    : Exception($"{method} {routeTemplate} answered {status}.")
{
    public string Method { get; } = method;

    public string RouteTemplate { get; } = routeTemplate;

    public int Status { get; } = status;
}

/// <summary>
/// The live-realm migration of mod-016 (BR-M1-28, D-6, D-16; ADR-0027): moves the realm <c>mustask</c> of a stand whose Keycloak store
/// was created from the PRE-mod-016 import to the realm the new import declares — in place, with no reset, so that users keep their
/// ids, credentials and attributes and the memberships and the user projection still match.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it does, in this order</b> (every grant before any removal; each step reads, acts only on the difference, and says so when
/// there is none): S0 a read-only preflight that stops — exit code 2, nothing written — when the structure is not the one the command
/// knows (a subgroup of /Сотрудники other than /Участники, a role mapped on either group other than <c>viewer</c> or <c>member</c>,
/// a realm role that has <c>admin</c> as a composite, a missing client role); S1 the realm role <c>instance-admin</c>, a composite of
/// the instance codes of the catalog; S2 it is mapped onto /Администраторы and onto every other group, at any depth, that maps
/// <c>admin</c>; S3 onto every user who holds <c>admin</c> directly; S4 the read code of the dictionaries into the realm's default
/// roles; <i>grants complete</i> — checked, by reading, before the first removal; S5 the realm roles <c>admin</c>, <c>member</c>,
/// <c>viewer</c> are deleted (which ends every mapping of them, on users, groups and roles alike); S6 the groups /Сотрудники/Участники
/// and /Сотрудники are deleted (their users stay, in no group, and read the dictionaries through S4).
/// </para>
/// <para>
/// <b>What it leaves alone</b>: no user is created or deleted; ids, credentials, attributes, every other group membership and role
/// mapping, clients and their secrets, Keycloak's built-in roles (the default role gains the one code) and every other realm.
/// A run stopped at any point is completed by running it again: S1–S4 are safe to repeat, S5–S6 skip what is gone, and since every
/// grant precedes every removal a user who held <c>admin</c> never has neither role.
/// </para>
/// <para>
/// <b>Secrets.</b> The password goes once in a form body to the token endpoint — never in a URL, never an argument. The access token
/// lives in memory and in the <c>Authorization</c> header of each request; no logging handler is attached; a failure is reported as
/// the method, the route template and the status only. The output holds fixed phrases, role and group names, and users by username.
/// </para>
/// <para>
/// Exit codes: <c>0</c> done or nothing to change, <c>2</c> stopped before any write (the report names what it found), <c>1</c> a
/// request failed (run it again).
/// </para>
/// </remarks>
public static class KeycloakLiveRealmMigration
{
    public const int ExitDone = 0;

    public const int ExitFailed = 1;

    public const int ExitStopped = 2;

    /// <summary>The realm role that replaces <c>admin</c> (BR-M1-29): the instance codes, nothing inside an organization.</summary>
    public const string InstanceAdminRole = "instance-admin";

    /// <summary>The group the new import maps to <see cref="InstanceAdminRole"/>.</summary>
    public const string AdministratorsGroup = "Администраторы";

    /// <summary>
    /// The read code every user gets through the realm's default roles (BR-M1-29). The AppHost cannot reference the domain (Aspire
    /// references the services as resources, not as assemblies), so the codes are written here and a contract test holds them to
    /// <c>PermissionCatalog</c>.
    /// </summary>
    public const string DictionariesReadCode = "dictionaries.languages.read";

    /// <summary>The codes <see cref="InstanceAdminRole"/> holds: the instance codes of the catalog — and no code of an organization.</summary>
    public static readonly IReadOnlyList<string> InstanceCodes =
    [
        "dictionaries.languages.read",
        "dictionaries.languages.write",
        "organizations.organization.create",
    ];

    private const string ResourceClient = "mustask-api";
    private const string OldAdminRole = "admin";
    private const string OldMemberRole = "member";
    private const string OldViewerRole = "viewer";
    private const string StaffGroup = "Сотрудники";
    private const string ParticipantsGroup = "Участники";
    private const int PageSize = 100;

    /// <summary>Runs the migration; answers the exit code and writes the report to <paramref name="output"/>.</summary>
    public static async Task<int> RunAsync(
        HttpClient http, KeycloakLiveRealmMigrationOptions options, TextWriter output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);

        var run = new Run(http, options, output);

        try
        {
            return await run.ExecuteAsync(cancellationToken);
        }
        catch (KeycloakRequestFailedException failure)
        {
            output.WriteLine(
                $"Stopped: a request failed ({failure.Method} {failure.RouteTemplate} answered {failure.Status}). "
                + "Nothing needs undoing: run the command again and it completes what is left.");

            return ExitFailed;
        }
        catch (HttpRequestException failure)
        {
            output.WriteLine(
                $"Stopped: Keycloak did not answer ({failure.GetType().Name}). Nothing needs undoing: run the command again and it completes what is left.");

            return ExitFailed;
        }
    }

    private sealed class Run(HttpClient http, KeycloakLiveRealmMigrationOptions options, TextWriter output)
    {
        private string _token = string.Empty;
        private int _changes;

        public async Task<int> ExecuteAsync(CancellationToken ct)
        {
            output.WriteLine($"Live-realm migration of realm `{options.Realm}` (mod-016, BR-M1-28).");

            if (!await SignInAsync(ct))
            {
                output.WriteLine("Stopped: Keycloak refuses the administrator password (KEYCLOAK_ADMIN_PASSWORD). Nothing was changed.");

                return ExitStopped;
            }

            var realm = await SendAsync(HttpMethod.Get, string.Empty, "/admin/realms/{realm}", null, ct, HttpStatusCode.NotFound);

            if (realm is null)
            {
                output.WriteLine($"Stopped: realm `{options.Realm}` does not exist in this Keycloak. Nothing was changed.");

                return ExitStopped;
            }

            var defaultRole = (string?)realm["defaultRole"]?["name"] ?? $"default-roles-{options.Realm}";

            // ---- S0: read-only preflight --------------------------------------------------------------------------------
            var roles = await ListRealmRolesAsync(ct);
            var codes = InstanceCodes.Order(StringComparer.Ordinal).ToList();
            var client = await FindClientAsync(ResourceClient, ct);
            var codeRoles = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            var problems = new List<string>();

            if (client is null)
            {
                problems.Add($"Client `{ResourceClient}` does not exist.");
            }
            else
            {
                foreach (var code in codes)
                {
                    var role = await SendAsync(
                        HttpMethod.Get, $"/clients/{Esc(client)}/roles/{Esc(code)}", "/admin/realms/{realm}/clients/{id}/roles/{role}", null, ct, HttpStatusCode.NotFound);

                    if (role is JsonObject found)
                    {
                        codeRoles[code] = found;
                    }
                    else
                    {
                        problems.Add($"Client role `{ResourceClient}/{code}` does not exist.");
                    }
                }
            }

            if (!roles.ContainsKey(defaultRole))
            {
                problems.Add($"The realm's default role `{defaultRole}` does not exist.");
            }

            var staff = await FindTopGroupAsync(StaffGroup, ct);
            GroupRef? participants = null;

            if (staff is not null)
            {
                var children = await ChildrenAsync(staff, ct);

                foreach (var child in children.Where(child => child.Name != ParticipantsGroup))
                {
                    problems.Add($"Group `{staff.Path}` holds the subgroup `{child.Path}`, which this command does not know: it would be deleted with its parent.");
                }

                participants = children.FirstOrDefault(child => child.Name == ParticipantsGroup);

                problems.AddRange(await UnexpectedMappingsAsync(staff, OldViewerRole, ct));

                if (participants is not null)
                {
                    foreach (var grandchild in await ChildrenAsync(participants, ct))
                    {
                        problems.Add($"Group `{participants.Path}` holds the subgroup `{grandchild.Path}`, which this command does not know: it would be deleted with its parent.");
                    }

                    problems.AddRange(await UnexpectedMappingsAsync(participants, OldMemberRole, ct));
                }
            }

            if (roles.ContainsKey(OldAdminRole))
            {
                foreach (var (name, composite) in roles.Where(pair => pair.Key != OldAdminRole))
                {
                    if (composite && (await CompositesAsync(name, ct)).Any(c => c is { Client: false, Name: OldAdminRole }))
                    {
                        problems.Add($"Realm role `{name}` has `{OldAdminRole}` as a composite: deleting `{OldAdminRole}` would take it from that role, and this command does not decide who then holds `{InstanceAdminRole}`.");
                    }
                }
            }

            if (problems.Count > 0)
            {
                output.WriteLine("Stopped before any change — the structure of the realm is not the one this command migrates:");

                foreach (var problem in problems)
                {
                    output.WriteLine($"  - {problem}");
                }

                output.WriteLine("Nothing was changed. Resolve the difference in the Keycloak console (or ask for the command to be extended), then run it again.");

                return ExitStopped;
            }

            output.WriteLine("Preflight: the realm has the structure this command migrates.");

            var allGroups = await AllGroupsAsync(ct);
            var adminGroups = new List<GroupRef>();
            var adminUsers = new List<UserRef>();

            if (roles.ContainsKey(OldAdminRole))
            {
                foreach (var group in allGroups)
                {
                    if ((await RealmMappingsOfGroupAsync(group, ct)).Contains(OldAdminRole))
                    {
                        adminGroups.Add(group);
                    }
                }

                adminUsers = await UsersWithRoleAsync(OldAdminRole, ct);
            }

            // ---- S1: instance-admin -------------------------------------------------------------------------------------
            await EnsureInstanceAdminAsync(codes, codeRoles, ct);
            var instanceAdmin = (JsonObject)(await SendAsync(HttpMethod.Get, $"/roles/{Esc(InstanceAdminRole)}", "/admin/realms/{realm}/roles/{role}", null, ct))!;

            // ---- S2: groups ---------------------------------------------------------------------------------------------
            var grantedGroups = allGroups.Where(group => group.Path == "/" + AdministratorsGroup).Concat(adminGroups).DistinctBy(group => group.Id).ToList();

            foreach (var group in grantedGroups)
            {
                await EnsureGroupMappingAsync(group, instanceAdmin, ct);
            }

            if (grantedGroups.Count == 0)
            {
                output.WriteLine($"Groups: no group `/{AdministratorsGroup}` and none that maps `{OldAdminRole}` — nothing to map.");
            }

            // ---- S3: users ----------------------------------------------------------------------------------------------
            foreach (var user in adminUsers)
            {
                await EnsureUserMappingAsync(user, instanceAdmin, ct);
            }

            if (adminUsers.Count == 0)
            {
                output.WriteLine($"Users: nobody holds `{OldAdminRole}` directly — nothing to map.");
            }

            // ---- S4: the default roles ----------------------------------------------------------------------------------
            await EnsureDefaultRoleReadsAsync(defaultRole, codeRoles[DictionariesReadCode], ct);

            // ---- grants complete: checked by reading, before the first removal -------------------------------------------
            if (roles.ContainsKey(OldAdminRole))
            {
                foreach (var group in adminGroups)
                {
                    if (!(await RealmMappingsOfGroupAsync(group, ct)).Contains(InstanceAdminRole))
                    {
                        output.WriteLine($"Stopped: group `{group.Path}` maps `{OldAdminRole}` but does not yet map `{InstanceAdminRole}`. Nothing was removed; run the command again.");

                        return ExitFailed;
                    }
                }

                foreach (var user in adminUsers)
                {
                    if (!(await RealmMappingsOfUserAsync(user, ct)).Contains(InstanceAdminRole))
                    {
                        output.WriteLine($"Stopped: user `{user.Username}` holds `{OldAdminRole}` but does not yet hold `{InstanceAdminRole}`. Nothing was removed; run the command again.");

                        return ExitFailed;
                    }
                }
            }

            output.WriteLine("Grants complete.");

            // ---- S5: the old realm roles ---------------------------------------------------------------------------------
            foreach (var name in new[] { OldAdminRole, OldMemberRole, OldViewerRole })
            {
                if (roles.ContainsKey(name))
                {
                    await SendAsync(HttpMethod.Delete, $"/roles/{Esc(name)}", "/admin/realms/{realm}/roles/{role}", null, ct, mutating: true);
                    output.WriteLine($"Realm role `{name}`: deleted.");
                }
                else
                {
                    output.WriteLine($"Realm role `{name}`: already gone.");
                }
            }

            // ---- S6: the old groups ---------------------------------------------------------------------------------------
            foreach (var group in new[] { participants, staff })
            {
                if (group is not null)
                {
                    await SendAsync(HttpMethod.Delete, $"/groups/{Esc(group.Id)}", "/admin/realms/{realm}/groups/{id}", null, ct, mutating: true);
                    output.WriteLine($"Group `{group.Path}`: deleted (its users stay, in no group).");
                }
            }

            if (staff is null)
            {
                output.WriteLine($"Group `/{StaffGroup}`: already gone.");
            }

            output.WriteLine(_changes == 0 ? "Nothing to change." : $"Done: {_changes} change(s) made.");

            return ExitDone;
        }

        // ----- S1 ----------------------------------------------------------------------------------------------------------

        private async Task EnsureInstanceAdminAsync(List<string> codes, Dictionary<string, JsonObject> codeRoles, CancellationToken ct)
        {
            var existing = await SendAsync(
                HttpMethod.Get, $"/roles/{Esc(InstanceAdminRole)}", "/admin/realms/{realm}/roles/{role}", null, ct, HttpStatusCode.NotFound);

            if (existing is null)
            {
                await SendAsync(
                    HttpMethod.Post,
                    "/roles",
                    "/admin/realms/{realm}/roles",
                    new JsonObject
                    {
                        ["name"] = InstanceAdminRole,
                        ["description"] = "Роль: администратор экземпляра — справочники и создание организаций (ADR-0027). Прав внутри организаций не даёт: они выводятся из членства в организации",
                    },
                    ct,
                    mutating: true);
                output.WriteLine($"Realm role `{InstanceAdminRole}`: created.");
            }

            var present = (await CompositesAsync(InstanceAdminRole, ct))
                .Where(composite => composite.Client && composite.Container == ResourceClient)
                .Select(composite => composite.Name)
                .ToHashSet(StringComparer.Ordinal);
            var missing = codes.Where(code => !present.Contains(code)).ToList();

            if (missing.Count > 0)
            {
                await SendAsync(
                    HttpMethod.Post,
                    $"/roles/{Esc(InstanceAdminRole)}/composites",
                    "/admin/realms/{realm}/roles/{role}/composites",
                    new JsonArray(missing.Select(code => (JsonNode)Representation(codeRoles[code])).ToArray()),
                    ct,
                    mutating: true);
                output.WriteLine($"Realm role `{InstanceAdminRole}`: composites added ({string.Join(", ", missing)}).");
            }
            else if (existing is not null)
            {
                output.WriteLine($"Realm role `{InstanceAdminRole}`: already complete.");
            }
        }

        // ----- S2 / S3 -----------------------------------------------------------------------------------------------------

        private async Task EnsureGroupMappingAsync(GroupRef group, JsonObject instanceAdmin, CancellationToken ct)
        {
            if ((await RealmMappingsOfGroupAsync(group, ct)).Contains(InstanceAdminRole))
            {
                output.WriteLine($"Group `{group.Path}`: already maps `{InstanceAdminRole}`.");

                return;
            }

            await SendAsync(
                HttpMethod.Post,
                $"/groups/{Esc(group.Id)}/role-mappings/realm",
                "/admin/realms/{realm}/groups/{id}/role-mappings/realm",
                new JsonArray(Representation(instanceAdmin)),
                ct,
                mutating: true);
            output.WriteLine($"Group `{group.Path}`: now maps `{InstanceAdminRole}`.");
        }

        private async Task EnsureUserMappingAsync(UserRef user, JsonObject instanceAdmin, CancellationToken ct)
        {
            if ((await RealmMappingsOfUserAsync(user, ct)).Contains(InstanceAdminRole))
            {
                output.WriteLine($"User `{user.Username}`: already holds `{InstanceAdminRole}`.");

                return;
            }

            await SendAsync(
                HttpMethod.Post,
                $"/users/{Esc(user.Id)}/role-mappings/realm",
                "/admin/realms/{realm}/users/{id}/role-mappings/realm",
                new JsonArray(Representation(instanceAdmin)),
                ct,
                mutating: true);
            output.WriteLine($"User `{user.Username}`: now holds `{InstanceAdminRole}`.");
        }

        // ----- S4 ----------------------------------------------------------------------------------------------------------

        private async Task EnsureDefaultRoleReadsAsync(string defaultRole, JsonObject readRole, CancellationToken ct)
        {
            var holds = (await CompositesAsync(defaultRole, ct))
                .Any(composite => composite.Client && composite.Container == ResourceClient && composite.Name == DictionariesReadCode);

            if (holds)
            {
                output.WriteLine($"Default roles `{defaultRole}`: already hold `{DictionariesReadCode}`.");

                return;
            }

            await SendAsync(
                HttpMethod.Post,
                $"/roles/{Esc(defaultRole)}/composites",
                "/admin/realms/{realm}/roles/{role}/composites",
                new JsonArray(Representation(readRole)),
                ct,
                mutating: true);
            output.WriteLine($"Default roles `{defaultRole}`: now hold `{DictionariesReadCode}`.");
        }

        // ----- reads -------------------------------------------------------------------------------------------------------

        private async Task<Dictionary<string, bool>> ListRealmRolesAsync(CancellationToken ct)
        {
            var roles = new Dictionary<string, bool>(StringComparer.Ordinal);

            await foreach (var item in PagedAsync("/roles", "/admin/realms/{realm}/roles", "&briefRepresentation=false", ct))
            {
                roles[(string)item["name"]!] = (bool?)item["composite"] == true;
            }

            return roles;
        }

        private async Task<string?> FindClientAsync(string clientId, CancellationToken ct)
        {
            var clients = await SendAsync(
                HttpMethod.Get, $"/clients?clientId={Esc(clientId)}", "/admin/realms/{realm}/clients", null, ct) as JsonArray;

            return clients?.OfType<JsonObject>().FirstOrDefault(client => (string?)client["clientId"] == clientId) is { } found ? (string)found["id"]! : null;
        }

        private async Task<List<CompositeRef>> CompositesAsync(string role, CancellationToken ct)
        {
            var composites = new List<CompositeRef>();

            await foreach (var item in PagedAsync(
                $"/roles/{Esc(role)}/composites", "/admin/realms/{realm}/roles/{role}/composites", string.Empty, ct))
            {
                var isClient = (bool?)item["clientRole"] == true;
                composites.Add(new CompositeRef(isClient, (string)item["name"]!, isClient ? await ClientIdOfAsync((string)item["containerId"]!, ct) : "realm"));
            }

            return composites;
        }

        private readonly Dictionary<string, string> _clientIds = new(StringComparer.Ordinal);

        private async Task<string> ClientIdOfAsync(string internalId, CancellationToken ct)
        {
            if (_clientIds.Count == 0)
            {
                var clients = await SendAsync(HttpMethod.Get, "/clients", "/admin/realms/{realm}/clients", null, ct) as JsonArray;

                foreach (var client in clients?.OfType<JsonObject>() ?? [])
                {
                    _clientIds[(string)client["id"]!] = (string)client["clientId"]!;
                }
            }

            return _clientIds.TryGetValue(internalId, out var clientId) ? clientId : internalId;
        }

        private async Task<GroupRef?> FindTopGroupAsync(string name, CancellationToken ct)
        {
            await foreach (var item in PagedAsync("/groups", "/admin/realms/{realm}/groups", "&briefRepresentation=true", ct))
            {
                if ((string?)item["name"] == name)
                {
                    return new GroupRef((string)item["id"]!, name, (string?)item["path"] ?? "/" + name);
                }
            }

            return null;
        }

        private async Task<List<GroupRef>> ChildrenAsync(GroupRef parent, CancellationToken ct)
        {
            var children = new List<GroupRef>();

            await foreach (var item in PagedAsync(
                $"/groups/{Esc(parent.Id)}/children", "/admin/realms/{realm}/groups/{id}/children", "&briefRepresentation=true", ct))
            {
                var name = (string)item["name"]!;
                children.Add(new GroupRef((string)item["id"]!, name, (string?)item["path"] ?? parent.Path + "/" + name));
            }

            return children;
        }

        /// <summary>Every group of the realm, at any depth.</summary>
        private async Task<List<GroupRef>> AllGroupsAsync(CancellationToken ct)
        {
            var all = new List<GroupRef>();
            var queue = new Queue<GroupRef>();

            await foreach (var item in PagedAsync("/groups", "/admin/realms/{realm}/groups", "&briefRepresentation=true", ct))
            {
                var name = (string)item["name"]!;
                queue.Enqueue(new GroupRef((string)item["id"]!, name, (string?)item["path"] ?? "/" + name));
            }

            while (queue.Count > 0)
            {
                var group = queue.Dequeue();
                all.Add(group);

                foreach (var child in await ChildrenAsync(group, ct))
                {
                    queue.Enqueue(child);
                }
            }

            return all;
        }

        private async Task<HashSet<string>> RealmMappingsOfGroupAsync(GroupRef group, CancellationToken ct) =>
            NamesOf(await SendAsync(
                HttpMethod.Get, $"/groups/{Esc(group.Id)}/role-mappings/realm", "/admin/realms/{realm}/groups/{id}/role-mappings/realm", null, ct));

        private async Task<HashSet<string>> RealmMappingsOfUserAsync(UserRef user, CancellationToken ct) =>
            NamesOf(await SendAsync(
                HttpMethod.Get, $"/users/{Esc(user.Id)}/role-mappings/realm", "/admin/realms/{realm}/users/{id}/role-mappings/realm", null, ct));

        private async Task<List<UserRef>> UsersWithRoleAsync(string role, CancellationToken ct)
        {
            var users = new List<UserRef>();

            await foreach (var item in PagedAsync(
                $"/roles/{Esc(role)}/users", "/admin/realms/{realm}/roles/{role}/users", "&briefRepresentation=true", ct))
            {
                var id = (string)item["id"]!;
                users.Add(new UserRef(id, (string?)item["username"] ?? id));
            }

            return users;
        }

        /// <summary>The problems with the mappings of <paramref name="group"/>: any realm role but <paramref name="expected"/>, any client role.</summary>
        private async Task<List<string>> UnexpectedMappingsAsync(GroupRef group, string expected, CancellationToken ct)
        {
            var problems = new List<string>();
            var mappings = await SendAsync(
                HttpMethod.Get, $"/groups/{Esc(group.Id)}/role-mappings", "/admin/realms/{realm}/groups/{id}/role-mappings", null, ct) as JsonObject;

            var realmRoles = NamesOf(mappings?["realmMappings"]);
            var others = realmRoles.Where(role => role != expected).Order(StringComparer.Ordinal).ToList();

            if (others.Count > 0)
            {
                problems.Add($"Group `{group.Path}` maps {string.Join(", ", others.Select(role => $"`{role}`"))} beside or instead of `{expected}`: deleting the group would end that mapping, and this command does not know why it is there.");
            }

            if (mappings?["clientMappings"] is JsonObject { Count: > 0 } clientMappings)
            {
                problems.Add($"Group `{group.Path}` maps client roles ({string.Join(", ", clientMappings.Select(pair => $"`{pair.Key}`"))}): deleting the group would end those mappings.");
            }

            return problems;
        }

        // ----- transport ---------------------------------------------------------------------------------------------------

        private async Task<bool> SignInAsync(CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/realms/master/protocol/openid-connect/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = "admin-cli",
                    ["grant_type"] = "password",
                    ["username"] = options.AdminUsername,
                    ["password"] = options.AdminPassword,
                }),
            };

            using var response = await http.SendAsync(request, ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
            {
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new KeycloakRequestFailedException("POST", "/realms/master/protocol/openid-connect/token", (int)response.StatusCode);
            }

            var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
            _token = (string?)body?["access_token"] ?? throw new KeycloakRequestFailedException("POST", "/realms/master/protocol/openid-connect/token", 0);

            return true;
        }

        /// <summary>One request to the admin surface. <paramref name="tolerated"/> statuses answer <c>null</c>; any other failure throws.</summary>
        private async Task<JsonNode?> SendAsync(
            HttpMethod method,
            string path,
            string routeTemplate,
            JsonNode? body,
            CancellationToken ct,
            HttpStatusCode? tolerated = null,
            bool mutating = false)
        {
            using var request = new HttpRequestMessage(method, $"/admin/realms/{Uri.EscapeDataString(options.Realm)}{path}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            if (body is not null)
            {
                request.Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
            }

            using var response = await http.SendAsync(request, ct);

            if (tolerated is { } status && response.StatusCode == status)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new KeycloakRequestFailedException(method.Method, routeTemplate, (int)response.StatusCode);
            }

            if (mutating)
            {
                _changes++;
            }

            var text = await response.Content.ReadAsStringAsync(ct);

            return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
        }

        private async IAsyncEnumerable<JsonObject> PagedAsync(
            string path, string routeTemplate, string extraQuery, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            for (var first = 0; ; first += PageSize)
            {
                var separator = path.Contains('?', StringComparison.Ordinal) ? "&" : "?";
                var page = await SendAsync(HttpMethod.Get, $"{path}{separator}first={first}&max={PageSize}{extraQuery}", routeTemplate, null, ct) as JsonArray;
                var items = page?.OfType<JsonObject>().ToList() ?? [];

                foreach (var item in items)
                {
                    yield return item;
                }

                if (items.Count < PageSize)
                {
                    yield break;
                }
            }
        }

        private static HashSet<string> NamesOf(JsonNode? array) =>
            (array as JsonArray)?.OfType<JsonObject>().Select(role => (string)role["name"]!).ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The part of a role Keycloak needs to map it or to add it as a composite: its id and name.</summary>
        private static JsonObject Representation(JsonObject role) =>
            new() { ["id"] = (string)role["id"]!, ["name"] = (string)role["name"]! };

        private static string Esc(string value) => Uri.EscapeDataString(value);
    }

    private sealed record GroupRef(string Id, string Name, string Path);

    private sealed record UserRef(string Id, string Username);

    private sealed record CompositeRef(bool Client, string Name, string Container);
}
