using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mustask.AppHost.Provisioning;

/// <summary>
/// ADR-0023: provisions the stand-only, impersonation-capable Keycloak
/// client and the E2E fixture user as part of bringing the stand up — never
/// by realm-import (`--import-realm` cannot add a client to a realm that
/// already exists, and this client must never appear in
/// `deploy/keycloak/realms/*.json`). Called once per stand start from
/// `AppHost.cs`'s `ResourceReadyEvent` subscription on the `keycloak`
/// resource.
///
/// Idempotent by design ("re-seeding on every start is harmless") — the
/// same create-or-update shape `scripts/keycloak-seed.sh`'s own `seed_user` already uses for the human-facing accounts: resolve by a
/// natural key first (`clientId`/`username`), then branch into create+fetch
/// or a straight update. Never calls a password-reset endpoint for the
/// fixture user — there is no such call anywhere in this file.
/// </summary>
public static class KeycloakStandProvisioner
{
    /// <summary>Keycloak's own `realm-management` client roles an automation client needs to impersonate within its own realm.</summary>
    private static readonly string[] ImpersonationRoles = ["impersonation", "manage-users"];

    /// <summary>
    /// Serializes overlapping calls within one process. DCP was observed
    /// re-firing `ResourceReadyEvent` repeatedly (~30s) after a bare `docker stop`/`start` of the `keycloak`
    /// container — the same window `KeycloakStandClientSecret`'s own header
    /// already reasons about for the secret. Without this, two overlapping
    /// passes could both see "client does not exist"
    /// (<see cref="CreateOrUpdateClientAsync"/>'s find-then-create branch)
    /// and both `POST`, and the loser's `EnsureSuccessStatusCode` would turn
    /// a harmless repeat firing into a failed provisioning. A process-wide
    /// lock is simpler and cheaper than making every create-or-update call
    /// site additionally tolerate `409 Conflict`, and it removes the
    /// interleaved-writes question (two concurrent get-modify-put realm
    /// updates) entirely, not just the create race.
    /// </summary>
    private static readonly SemaphoreSlim ProvisioningLock = new(1, 1);

    public static async Task ProvisionAsync(
        HttpClient http,
        KeycloakStandProvisioningOptions options,
        CancellationToken cancellationToken)
    {
        await ProvisioningLock.WaitAsync(cancellationToken);

        try
        {
            var token = await GetAdminTokenAsync(http, options.AdminUsername, options.AdminPassword, cancellationToken);

            // The realm's own access-token lifetime is set here, with the admin
            // token above — never through `mustask-e2e`, which deliberately
            // lacks manage-realm/view-realm (a client that can already impersonate anybody does not also get
            // the right to rewrite realm configuration for test speed).
            await SetAccessTokenLifespanAsync(http, token, options.Realm, options.AccessTokenLifespanSeconds, cancellationToken);

            var clientObjectId = await CreateOrUpdateClientAsync(http, token, options, cancellationToken);
            var serviceAccountUserId = await GetServiceAccountUserIdAsync(http, token, options.Realm, clientObjectId, cancellationToken);
            await GrantImpersonationRolesAsync(http, token, options.Realm, serviceAccountUserId, cancellationToken);

            var fixtureUserId = await CreateOrUpdateFixtureUserAsync(http, token, options, cancellationToken);
            await AddUserToGroupAsync(http, token, options.Realm, fixtureUserId, options.GroupPath, cancellationToken);

            await WriteStandFileAsync(options, cancellationToken);
        }
        finally
        {
            ProvisioningLock.Release();
        }
    }

    private static async Task<string> GetAdminTokenAsync(
        HttpClient http, string username, string password, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/realms/master/protocol/openid-connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = "admin-cli",
                ["grant_type"] = "password",
                ["username"] = username,
                ["password"] = password,
            }),
        };

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken)
            ?? throw new InvalidOperationException("Keycloak's token endpoint returned an empty body.");

        return body["access_token"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Keycloak's token response has no access_token.");
    }

    // --- The realm's own access-token lifetime -----------------------------------

    /// <summary>
    /// Get-modify-put on the full realm representation, not a partial body —
    /// the same caution `e2e/support/keycloakAdmin.ts`'s own header comment
    /// records: a partial `PUT /admin/realms/{realm}` replaces the whole
    /// object and would silently wipe clients, themes and everything else
    /// not named in the body. Idempotent — safe to run on every stand start,
    /// same as the client/user provisioning below.
    /// </summary>
    private static async Task SetAccessTokenLifespanAsync(
        HttpClient http, string token, string realm, int seconds, CancellationToken cancellationToken)
    {
        using var getRequest = Authorized(HttpMethod.Get, $"/admin/realms/{realm}", token);
        using var getResponse = await http.SendAsync(getRequest, cancellationToken);
        getResponse.EnsureSuccessStatusCode();

        var representation = await getResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken)
            ?? throw new InvalidOperationException($"Realm `{realm}` representation came back empty.");

        representation["accessTokenLifespan"] = seconds;

        using var putRequest = Authorized(HttpMethod.Put, $"/admin/realms/{realm}", token, representation);
        using var putResponse = await http.SendAsync(putRequest, cancellationToken);
        putResponse.EnsureSuccessStatusCode();
    }

    // --- The stand-only client -------------------------------------------------

    private static async Task<string> CreateOrUpdateClientAsync(
        HttpClient http, string token, KeycloakStandProvisioningOptions options, CancellationToken cancellationToken)
    {
        var representation = new JsonObject
        {
            ["clientId"] = options.ClientId,
            ["name"] = "MUSTASK E2E",
            ["enabled"] = true,
            ["publicClient"] = false,
            ["secret"] = options.ClientSecret,
            ["serviceAccountsEnabled"] = true,
            ["standardFlowEnabled"] = false,
            ["implicitFlowEnabled"] = false,
            ["directAccessGrantsEnabled"] = false,
        };

        var existingId = await FindByExactMatchAsync(http, token, $"/admin/realms/{options.Realm}/clients?clientId={Uri.EscapeDataString(options.ClientId)}", cancellationToken);

        if (existingId is null)
        {
            using var create = Authorized(HttpMethod.Post, $"/admin/realms/{options.Realm}/clients", token, representation);
            using var createResponse = await http.SendAsync(create, cancellationToken);
            createResponse.EnsureSuccessStatusCode();

            existingId = await FindByExactMatchAsync(http, token, $"/admin/realms/{options.Realm}/clients?clientId={Uri.EscapeDataString(options.ClientId)}", cancellationToken)
                ?? throw new InvalidOperationException($"Client `{options.ClientId}` was created but cannot be found again by clientId.");
        }
        else
        {
            using var update = Authorized(HttpMethod.Put, $"/admin/realms/{options.Realm}/clients/{existingId}", token, representation);
            using var updateResponse = await http.SendAsync(update, cancellationToken);
            updateResponse.EnsureSuccessStatusCode();
        }

        return existingId;
    }

    private static async Task<string> GetServiceAccountUserIdAsync(
        HttpClient http, string token, string realm, string clientObjectId, CancellationToken cancellationToken)
    {
        using var request = Authorized(HttpMethod.Get, $"/admin/realms/{realm}/clients/{clientObjectId}/service-account-user", token);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken)
            ?? throw new InvalidOperationException("Keycloak returned an empty service-account-user body.");

        return body["id"]?.GetValue<string>()
            ?? throw new InvalidOperationException("The service account user has no id.");
    }

    private static async Task GrantImpersonationRolesAsync(
        HttpClient http, string token, string realm, string serviceAccountUserId, CancellationToken cancellationToken)
    {
        var realmManagementId = await FindByExactMatchAsync(http, token, $"/admin/realms/{realm}/clients?clientId=realm-management", cancellationToken)
            ?? throw new InvalidOperationException(
                "Realm `" + realm + "` has no `realm-management` client — Keycloak creates it for every realm; " +
                "an absent client here means the realm itself is not the one this code expects.");

        using var rolesRequest = Authorized(HttpMethod.Get, $"/admin/realms/{realm}/clients/{realmManagementId}/roles", token);
        using var rolesResponse = await http.SendAsync(rolesRequest, cancellationToken);
        rolesResponse.EnsureSuccessStatusCode();

        var allRoles = await rolesResponse.Content.ReadFromJsonAsync<JsonArray>(cancellationToken)
            ?? throw new InvalidOperationException("`realm-management`'s role list came back empty.");

        var grant = new JsonArray();

        foreach (var roleName in ImpersonationRoles)
        {
            var role = allRoles
                .Select(r => r!.AsObject())
                .FirstOrDefault(r => string.Equals(r["name"]?.GetValue<string>(), roleName, StringComparison.Ordinal))
                ?? throw new InvalidOperationException(
                    $"Keycloak's own `realm-management` client has no `{roleName}` role — " +
                    "this Keycloak version no longer matches what ADR-0023 assumes.");

            grant.Add(new JsonObject
            {
                ["id"] = role["id"]!.GetValue<string>(),
                ["name"] = roleName,
            });
        }

        using var grantRequest = Authorized(
            HttpMethod.Post, $"/admin/realms/{realm}/users/{serviceAccountUserId}/role-mappings/clients/{realmManagementId}", token, grant);
        using var grantResponse = await http.SendAsync(grantRequest, cancellationToken);
        grantResponse.EnsureSuccessStatusCode();
    }

    // --- The fixture user --------------------------------------------------------

    private static async Task<string> CreateOrUpdateFixtureUserAsync(
        HttpClient http, string token, KeycloakStandProvisioningOptions options, CancellationToken cancellationToken)
    {
        // `email`, `firstName`, `lastName` are not
        // decoration — realm `mustask`'s declarative user profile requires all
        // three, and a user seeded without them is created but cannot
        // complete a sign-in ("Account is not fully set up",
        // `scripts/keycloak-seed.sh`'s own header). `emailVerified`/`enabled`
        // leave no pending required action behind either.
        //
        // Deliberately absent: any `credentials` field — no code path here
        // ever sets a password for this user.
        var profile = new JsonObject
        {
            ["username"] = options.FixtureUsername,
            ["enabled"] = true,
            ["emailVerified"] = true,
            ["email"] = $"{options.FixtureUsername}@stand.local",
            ["firstName"] = "E2E",
            ["lastName"] = "Fixture",
        };

        var existingId = await FindByExactMatchAsync(
            http, token, $"/admin/realms/{options.Realm}/users?username={Uri.EscapeDataString(options.FixtureUsername)}&exact=true", cancellationToken);

        if (existingId is null)
        {
            using var create = Authorized(HttpMethod.Post, $"/admin/realms/{options.Realm}/users", token, profile);
            using var createResponse = await http.SendAsync(create, cancellationToken);
            createResponse.EnsureSuccessStatusCode();

            existingId = await FindByExactMatchAsync(
                http, token, $"/admin/realms/{options.Realm}/users?username={Uri.EscapeDataString(options.FixtureUsername)}&exact=true", cancellationToken)
                ?? throw new InvalidOperationException($"User `{options.FixtureUsername}` was created but cannot be found again by username.");
        }

        // Always PUT, whether just created or already existing — the same
        // shape as `keycloak-seed.sh`'s `seed_user`: an account created by an
        // older version of this code must not be left without a field this
        // version considers mandatory.
        using var update = Authorized(HttpMethod.Put, $"/admin/realms/{options.Realm}/users/{existingId}", token, profile);
        using var updateResponse = await http.SendAsync(update, cancellationToken);
        updateResponse.EnsureSuccessStatusCode();

        return existingId;
    }

    private static async Task AddUserToGroupAsync(
        HttpClient http, string token, string realm, string userId, string groupPath, CancellationToken cancellationToken)
    {
        var encodedPath = string.Join('/', groupPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));

        using var groupRequest = Authorized(HttpMethod.Get, $"/admin/realms/{realm}/group-by-path/{encodedPath}", token);
        using var groupResponse = await http.SendAsync(groupRequest, cancellationToken);
        groupResponse.EnsureSuccessStatusCode();

        var group = await groupResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken)
            ?? throw new InvalidOperationException($"Group `{groupPath}` was not found in realm `{realm}` — did the realm import not happen?");

        var groupId = group["id"]?.GetValue<string>()
            ?? throw new InvalidOperationException($"Group `{groupPath}` has no id.");

        using var membershipRequest = Authorized(HttpMethod.Put, $"/admin/realms/{realm}/users/{userId}/groups/{groupId}", token);
        using var membershipResponse = await http.SendAsync(membershipRequest, cancellationToken);
        membershipResponse.EnsureSuccessStatusCode();
    }

    // --- The stand file -----------------------------------------------------------

    private static async Task WriteStandFileAsync(KeycloakStandProvisioningOptions options, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(options.StandFilePath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var contents = new JsonObject
        {
            ["issuer"] = options.Issuer,
            ["clientId"] = options.ClientId,
            ["clientSecret"] = options.ClientSecret,
            ["keycloakContainerName"] = options.KeycloakContainerName,
        };

        await File.WriteAllTextAsync(
            options.StandFilePath,
            contents.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    // --- Shared plumbing ----------------------------------------------------------

    /// <summary>
    /// Runs a GET expected to return a Keycloak search-style JSON array and
    /// returns the first element's `id`, or <see langword="null"/> when the
    /// array is empty — the same "does it already exist" check
    /// `keycloak-seed.sh`'s own `first_id`/`group_id_by_path` make, just
    /// against real JSON instead of `sed` over text.
    /// </summary>
    private static async Task<string?> FindByExactMatchAsync(
        HttpClient http, string token, string path, CancellationToken cancellationToken)
    {
        using var request = Authorized(HttpMethod.Get, path, token);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var results = await response.Content.ReadFromJsonAsync<JsonArray>(cancellationToken)
            ?? throw new InvalidOperationException($"Expected a JSON array from `{path}`.");

        return results.Count == 0
            ? null
            : results[0]!.AsObject()["id"]?.GetValue<string>();
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token, JsonNode? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }
}
