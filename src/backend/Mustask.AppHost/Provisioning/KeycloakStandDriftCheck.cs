using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Mustask.AppHost.Provisioning;

/// <summary>
/// The stand's start drift check (mod-014 D-10 (a); ADR-0025 § 7): compares
/// the live realm <c>mustask</c> of a persistent Keycloak store with the realm
/// import, through the admin REST surface — allowed here because
/// <c>Mustask.AppHost</c> is stand-only code that is never deployed, the
/// placement ADR-0023 established. Every finding names what differs and the
/// remedy; no secret's value ever appears in one.
/// </summary>
/// <remarks>
/// Compared: realm roles both ways (Keycloak's built-ins aside) with their
/// composites; the roles of every client the import declares, both ways
/// (undeclared clients — Keycloak's own and ADR-0023's <c>mustask-e2e</c> —
/// are not compared); the provider's enablement; declared groups one way —
/// each must exist with exactly its declared role mappings, groups only in
/// the live realm are ignored; and the values a store takes only when it is
/// created: the administrator password, the <c>mustask-panel</c> secret, and
/// the client URLs filled from the stand's ports.
/// </remarks>
public static class KeycloakStandDriftCheck
{
    /// <summary>Keycloak's own realm roles, present in every realm whatever the import says.</summary>
    public static readonly IReadOnlySet<string> BuiltInRealmRoles = new HashSet<string>(StringComparer.Ordinal)
    {
        "offline_access", "uma_authorization",
    };

    private const string Reset = "reset the stand: " + UserProjectionIdentifiers.ResetCommand;

    public static async Task<IReadOnlyList<string>> RunAsync(
        HttpClient http,
        KeycloakDriftCheckOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);

        var findings = new List<string>();
        var expected = options.Import;

        var token = await AdminTokenAsync(http, options, cancellationToken);

        if (token is null)
        {
            findings.Add(
                "Keycloak refuses the administrator password: the store keeps the password it was created with. "
                + "Restore the old value of Parameters:keycloak-admin-password, or " + Reset + ".");
            return findings;
        }

        var realmPath = "/admin/realms/" + Uri.EscapeDataString(expected.Realm);
        var realm = await GetAsync<JsonObject>(http, token, realmPath, cancellationToken);

        if (realm is null)
        {
            findings.Add($"Realm `{expected.Realm}` does not exist in this Keycloak store; {Reset}.");
            return findings;
        }

        CompareListener(expected, realm, findings);

        var clients = await GetAsync<JsonArray>(http, token, realmPath + "/clients", cancellationToken) ?? [];
        var clientIdsByInternalId = clients
            .OfType<JsonObject>()
            .ToDictionary(client => (string)client["id"]!, client => (string)client["clientId"]!, StringComparer.Ordinal);

        await CompareRealmRolesAsync(http, token, realmPath, expected, clientIdsByInternalId, findings, cancellationToken);
        await CompareClientsAsync(http, token, realmPath, options, clients, findings, cancellationToken);
        await CompareGroupsAsync(http, token, realmPath, expected, findings, cancellationToken);

        return findings;
    }

    private static void CompareListener(KeycloakRealmImport expected, JsonObject realm, List<string> findings)
    {
        var declared = expected.EventsListeners.Contains(UserProjectionIdentifiers.ProviderId, StringComparer.Ordinal);
        var live = (realm["eventsListeners"] as JsonArray ?? [])
            .Any(item => (string?)item == UserProjectionIdentifiers.ProviderId);

        if (declared != live)
        {
            findings.Add(
                $"The event listener `{UserProjectionIdentifiers.ProviderId}` is {(live ? "enabled" : "not enabled")} in the live realm "
                + $"but the realm import {(declared ? "enables" : "does not enable")} it; {Reset}.");
        }
    }

    private static async Task CompareRealmRolesAsync(
        HttpClient http,
        string token,
        string realmPath,
        KeycloakRealmImport expected,
        IReadOnlyDictionary<string, string> clientIdsByInternalId,
        List<string> findings,
        CancellationToken cancellationToken)
    {
        var liveRoles = (await GetAsync<JsonArray>(http, token, realmPath + "/roles?briefRepresentation=false", cancellationToken) ?? [])
            .OfType<JsonObject>()
            .ToDictionary(role => (string)role["name"]!, StringComparer.Ordinal);

        var defaultRoles = "default-roles-" + expected.Realm;

        foreach (var name in expected.RealmRoles.Keys.Where(name => !liveRoles.ContainsKey(name)).Order(StringComparer.Ordinal))
        {
            findings.Add($"Realm role `{name}` is in the realm import but not in the live realm; {Reset}.");
        }

        foreach (var name in liveRoles.Keys
            .Where(name => !expected.RealmRoles.ContainsKey(name) && !BuiltInRealmRoles.Contains(name) && name != defaultRoles)
            .Order(StringComparer.Ordinal))
        {
            findings.Add($"Realm role `{name}` is in the live realm but not in the realm import; {Reset}.");
        }

        foreach (var declared in expected.RealmRoles.Values.Where(role => liveRoles.ContainsKey(role.Name)).OrderBy(role => role.Name, StringComparer.Ordinal))
        {
            var (realmComposites, clientComposites) = (bool?)liveRoles[declared.Name]["composite"] == true
                ? await CompositesAsync(http, token, realmPath + "/roles/" + Uri.EscapeDataString(declared.Name) + "/composites", clientIdsByInternalId, cancellationToken)
                : (new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal));

            if (!realmComposites.SetEquals(declared.RealmComposites) || !SameByClient(clientComposites, declared.ClientComposites))
            {
                findings.Add($"Realm role `{declared.Name}` has other composites in the live realm than in the realm import; {Reset}.");
            }
        }
    }

    private static async Task<(HashSet<string> Realm, Dictionary<string, IReadOnlySet<string>> ByClient)> CompositesAsync(
        HttpClient http,
        string token,
        string path,
        IReadOnlyDictionary<string, string> clientIdsByInternalId,
        CancellationToken cancellationToken)
    {
        var realmComposites = new HashSet<string>(StringComparer.Ordinal);
        var byClient = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var role in (await GetAsync<JsonArray>(http, token, path, cancellationToken) ?? []).OfType<JsonObject>())
        {
            var name = (string)role["name"]!;

            if ((bool?)role["clientRole"] == true)
            {
                var clientId = clientIdsByInternalId.TryGetValue((string)role["containerId"]!, out var id) ? id : (string)role["containerId"]!;

                if (!byClient.TryGetValue(clientId, out var set))
                {
                    byClient[clientId] = set = new HashSet<string>(StringComparer.Ordinal);
                }

                set.Add(name);
            }
            else
            {
                realmComposites.Add(name);
            }
        }

        return (realmComposites, byClient.ToDictionary(pair => pair.Key, pair => (IReadOnlySet<string>)pair.Value, StringComparer.Ordinal));
    }

    private static async Task CompareClientsAsync(
        HttpClient http,
        string token,
        string realmPath,
        KeycloakDriftCheckOptions options,
        JsonArray liveClients,
        List<string> findings,
        CancellationToken cancellationToken)
    {
        foreach (var declared in options.Import.Clients.Values.OrderBy(client => client.ClientId, StringComparer.Ordinal))
        {
            var live = liveClients.OfType<JsonObject>().FirstOrDefault(client => (string?)client["clientId"] == declared.ClientId);

            if (live is null)
            {
                findings.Add($"Client `{declared.ClientId}` is in the realm import but not in the live realm; {Reset}.");
                continue;
            }

            var clientPath = realmPath + "/clients/" + Uri.EscapeDataString((string)live["id"]!);
            var liveRoles = (await GetAsync<JsonArray>(http, token, clientPath + "/roles", cancellationToken) ?? [])
                .OfType<JsonObject>()
                .Select(role => (string)role["name"]!)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var missing in declared.Roles.Except(liveRoles).Order(StringComparer.Ordinal))
            {
                findings.Add($"Client role `{declared.ClientId}/{missing}` is in the realm import but not in the live realm; {Reset}.");
            }

            foreach (var extra in liveRoles.Except(declared.Roles).Order(StringComparer.Ordinal))
            {
                findings.Add($"Client role `{declared.ClientId}/{extra}` is in the live realm but not in the realm import; {Reset}.");
            }

            var liveRedirects = (live["redirectUris"] as JsonArray ?? []).Select(item => (string)item!).ToHashSet(StringComparer.Ordinal);
            var livePostLogout = (string?)(live["attributes"] as JsonObject)?["post.logout.redirect.uris"];

            if ((declared.RootUrl is not null && declared.RootUrl != (string?)live["rootUrl"])
                || !liveRedirects.SetEquals(declared.RedirectUris)
                || (declared.PostLogoutRedirectUris is not null && declared.PostLogoutRedirectUris != livePostLogout))
            {
                findings.Add(
                    $"Client `{declared.ClientId}` keeps the URLs of other stand ports than Ports:Frontend/Ports:Api now give "
                    + $"(the store took them when it was created): restore the old ports, or {Reset}.");
            }

            if (declared.HasSecret && declared.ClientId == options.SecretClientId)
            {
                var secret = await GetAsync<JsonObject>(http, token, clientPath + "/client-secret", cancellationToken);

                if ((string?)secret?["value"] != options.SecretClientSecret)
                {
                    findings.Add(
                        $"Client `{declared.ClientId}`'s secret in the store differs from Parameters:keycloak-panel-client-secret "
                        + $"(the store took it when it was created): restore the old value of the parameter, or {Reset}.");
                }
            }
        }
    }

    private static async Task CompareGroupsAsync(
        HttpClient http,
        string token,
        string realmPath,
        KeycloakRealmImport expected,
        List<string> findings,
        CancellationToken cancellationToken)
    {
        foreach (var declared in expected.Groups)
        {
            var escapedPath = string.Join('/', declared.Path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
            var group = await GetAsync<JsonObject>(http, token, realmPath + "/group-by-path/" + escapedPath, cancellationToken);

            if (group is null)
            {
                findings.Add($"Group `{declared.Path}` is in the realm import but not in the live realm; {Reset}.");
                continue;
            }

            var mappings = await GetAsync<JsonObject>(
                http, token, realmPath + "/groups/" + Uri.EscapeDataString((string)group["id"]!) + "/role-mappings", cancellationToken) ?? [];

            var realmRoles = (mappings["realmMappings"] as JsonArray ?? [])
                .OfType<JsonObject>()
                .Select(role => (string)role["name"]!)
                .ToHashSet(StringComparer.Ordinal);

            var clientRoles = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

            if (mappings["clientMappings"] is JsonObject byClient)
            {
                foreach (var (clientId, mapping) in byClient)
                {
                    clientRoles[clientId] = (mapping?["mappings"] as JsonArray ?? [])
                        .OfType<JsonObject>()
                        .Select(role => (string)role["name"]!)
                        .ToHashSet(StringComparer.Ordinal);
                }
            }

            if (!realmRoles.SetEquals(declared.RealmRoles) || !SameByClient(clientRoles, declared.ClientRoles))
            {
                findings.Add($"Group `{declared.Path}` has other role mappings in the live realm than in the realm import; {Reset}.");
            }
        }
    }

    private static bool SameByClient(
        IReadOnlyDictionary<string, IReadOnlySet<string>> live,
        IReadOnlyDictionary<string, IReadOnlySet<string>> declared)
    {
        var clients = live.Where(pair => pair.Value.Count > 0).Select(pair => pair.Key)
            .Union(declared.Where(pair => pair.Value.Count > 0).Select(pair => pair.Key), StringComparer.Ordinal);

        return clients.All(client =>
            live.TryGetValue(client, out var liveRoles)
            && declared.TryGetValue(client, out var declaredRoles)
            && liveRoles.SetEquals(declaredRoles));
    }

    private static async Task<string?> AdminTokenAsync(HttpClient http, KeycloakDriftCheckOptions options, CancellationToken cancellationToken)
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

        using var response = await http.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);

        return (string?)body?["access_token"]
            ?? throw new InvalidOperationException("Keycloak's token response has no access_token.");
    }

    /// <summary>A GET with the admin token; null on 404, an exception on any other failure.</summary>
    private static async Task<T?> GetAsync<T>(HttpClient http, string token, string path, CancellationToken cancellationToken)
        where T : JsonNode
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await http.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken) as T;
    }
}

/// <summary>What the drift check compares the live realm with.</summary>
public sealed class KeycloakDriftCheckOptions
{
    public required KeycloakRealmImport Import { get; init; }

    public required string AdminUsername { get; init; }

    public required string AdminPassword { get; init; }

    /// <summary>The client whose secret the stand sets through a parameter (<c>mustask-panel</c>).</summary>
    public required string SecretClientId { get; init; }

    /// <summary>That parameter's value — compared, never printed.</summary>
    public required string SecretClientSecret { get; init; }
}
