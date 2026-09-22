using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mustask.AppHost.Provisioning;

namespace Mustask.Contracts.Tests;

/// <summary>
/// Unit tests over
/// <see cref="KeycloakStandProvisioner"/> against a fake
/// <see cref="HttpMessageHandler"/> — no Docker, no HTTP host, no DB,
/// matching this project's own charter (see the comment at the top of
/// <see cref="KeycloakRealmRolesTests"/>'s project file). What a real,
/// live Keycloak still has to confirm (a token is actually obtainable, no
/// "Account is not fully set up", an admin event is actually logged) is a
/// live check, not here — this names the gap it leaves rather than
/// hiding it.
/// </summary>
public sealed class KeycloakStandProvisioningTests
{
    [Fact]
    public async Task ProvisionAsync_FreshRealm_CreatesClientAndFixtureUser()
    {
        var handler = new RecordingKeycloakHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://keycloak.invalid") };
        var standFilePath = TempFilePath();

        try
        {
            var options = Options(standFilePath);

            await KeycloakStandProvisioner.ProvisionAsync(http, options, CancellationToken.None);

            // The client is created (POST, not PUT — nothing existed yet).
            Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Post && r.Path == "/admin/realms/mustask/clients");
            Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Put && r.Path.StartsWith("/admin/realms/mustask/clients/", StringComparison.Ordinal));

            var clientBody = JsonDocument.Parse(handler.Requests.Single(r => r.Method == HttpMethod.Post && r.Path == "/admin/realms/mustask/clients").Body!).RootElement;
            Assert.Equal("mustask-e2e", clientBody.GetProperty("clientId").GetString());
            Assert.Equal(options.ClientSecret, clientBody.GetProperty("secret").GetString());
            Assert.True(clientBody.GetProperty("serviceAccountsEnabled").GetBoolean());
            Assert.False(clientBody.GetProperty("publicClient").GetBoolean());

            // Both realm-management roles requested on the service account.
            var grantBody = JsonDocument.Parse(handler.Requests.Single(r =>
                r.Method == HttpMethod.Post && r.Path == "/admin/realms/mustask/users/service-account-user-id/role-mappings/clients/realm-management-id").Body!).RootElement;
            var grantedRoleNames = grantBody.EnumerateArray().Select(r => r.GetProperty("name").GetString()!).ToHashSet(StringComparer.Ordinal);
            Assert.Equal(new HashSet<string> { "impersonation", "manage-users" }, grantedRoleNames);

            // The fixture user is created (POST) with the profile fields realm
            // `mustask`'s declarative user profile requires.
            var userCreateBody = JsonDocument.Parse(handler.Requests.Single(r => r.Method == HttpMethod.Post && r.Path == "/admin/realms/mustask/users").Body!).RootElement;
            Assert.Equal("e2e-fixture", userCreateBody.GetProperty("username").GetString());
            Assert.True(userCreateBody.GetProperty("enabled").GetBoolean());
            Assert.True(userCreateBody.GetProperty("emailVerified").GetBoolean());
            Assert.False(string.IsNullOrEmpty(userCreateBody.GetProperty("email").GetString()));
            Assert.False(string.IsNullOrEmpty(userCreateBody.GetProperty("firstName").GetString()));
            Assert.False(string.IsNullOrEmpty(userCreateBody.GetProperty("lastName").GetString()));

            // The fixture user is put in the same group `mustask` uses today.
            Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Put && r.Path == "/admin/realms/mustask/users/existing-user-id/groups/group-admins-id");
        }
        finally
        {
            File.Delete(standFilePath);
        }
    }

    [Fact]
    public async Task ProvisionAsync_ClientAndUserAlreadyExist_UpdatesInPlace()
    {
        var handler = new RecordingKeycloakHandler { ClientExists = true, UserExists = true };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://keycloak.invalid") };
        var standFilePath = TempFilePath();

        try
        {
            var options = Options(standFilePath);

            // Re-seeding on an already-provisioned realm must not throw
            // and must not attempt a second create.
            await KeycloakStandProvisioner.ProvisionAsync(http, options, CancellationToken.None);

            Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post && r.Path == "/admin/realms/mustask/clients");
            Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Put && r.Path == "/admin/realms/mustask/clients/existing-client-id");

            Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post && r.Path == "/admin/realms/mustask/users");
            Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Put && r.Path == "/admin/realms/mustask/users/existing-user-id");

            // The client's secret is whatever the caller passed in `options`
            // (AppHost generates a fresh one per *process* — see
            // KeycloakStandClientSecret — and hands it in here unchanged).
            var updateBody = JsonDocument.Parse(handler.Requests.Single(r => r.Method == HttpMethod.Put && r.Path == "/admin/realms/mustask/clients/existing-client-id").Body!).RootElement;
            Assert.Equal(options.ClientSecret, updateBody.GetProperty("secret").GetString());
        }
        finally
        {
            File.Delete(standFilePath);
        }
    }

    [Fact]
    public async Task ProvisionAsync_CalledTwiceWithinOneRun_ConvergesWithoutChangingTheSecret()
    {
        // The live regression this guards: DCP re-fires ResourceReadyEvent
        // repeatedly (~30s) after a bare `docker stop`/`docker start` of the
        // keycloak container, and AppHost.cs used to generate a fresh secret
        // inside that handler — this is the two-firings-in-one-run shape,
        // proven here at the provisioning level: called twice with the
        // *same* options (the same fixed secret AppHost.cs now passes every
        // time, KeycloakStandClientSecret), the second pass must not throw
        // and must leave the client with the same secret, not a new one.
        var handler = new RecordingKeycloakHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://keycloak.invalid") };
        var standFilePath = TempFilePath();

        try
        {
            var options = Options(standFilePath);

            await KeycloakStandProvisioner.ProvisionAsync(http, options, CancellationToken.None);
            await KeycloakStandProvisioner.ProvisionAsync(http, options, CancellationToken.None);

            // First pass creates (POST), second finds it and updates (PUT) —
            // both must carry the identical secret, since `options` did not change.
            var createBody = JsonDocument.Parse(
                handler.Requests.Single(r => r.Method == HttpMethod.Post && r.Path == "/admin/realms/mustask/clients").Body!).RootElement;
            var updateBody = JsonDocument.Parse(
                handler.Requests.Single(r => r.Method == HttpMethod.Put && r.Path == "/admin/realms/mustask/clients/existing-client-id").Body!).RootElement;

            Assert.Equal(options.ClientSecret, createBody.GetProperty("secret").GetString());
            Assert.Equal(options.ClientSecret, updateBody.GetProperty("secret").GetString());

            // The role grant, group membership and realm-lifespan write are
            // each attempted a second time too (Aspire re-fires the whole
            // event, not just the client step) — none of them threw, which
            // is the fake's stand-in for "Keycloak tolerates a repeat
            // add-to-a-set call"; the live check confirms the real server
            // agrees.
            Assert.Equal(2, handler.Requests.Count(r =>
                r.Method == HttpMethod.Post && r.Path == "/admin/realms/mustask/users/service-account-user-id/role-mappings/clients/realm-management-id"));
            Assert.Equal(2, handler.Requests.Count(r =>
                r.Method == HttpMethod.Put && r.Path == "/admin/realms/mustask/users/existing-user-id/groups/group-admins-id"));
            Assert.Equal(2, handler.Requests.Count(r => r.Method == HttpMethod.Put && r.Path == "/admin/realms/mustask"));
        }
        finally
        {
            File.Delete(standFilePath);
        }
    }

    [Fact]
    public async Task ProvisionAsync_NeverCallsResetPassword()
    {
        // "The fixture user is created with no password credential, and no code
        // path (seed or otherwise) ever sets one." The fake handler has no branch for
        // a reset-password call at all — if the code under test ever issued one, this
        // test would fail with "Unexpected request", not silently. That covers the
        // *path* a regression would take; it does not cover the shape one would
        // actually take: Keycloak accepts a `credentials`
        // array inside the user representation itself, so a future
        // `profile["credentials"] = ...` at the create/update call sites would set a
        // password, send no new request and touch no new path, leaving a path-only
        // assertion green. So this test also inspects the *bodies* sent to every
        // `/admin/realms/{realm}/users*` request, not only their paths.
        var handler = new RecordingKeycloakHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://keycloak.invalid") };
        var standFilePath = TempFilePath();

        try
        {
            var options = Options(standFilePath);

            await KeycloakStandProvisioner.ProvisionAsync(http, options, CancellationToken.None);

            Assert.DoesNotContain(handler.Requests, r => r.Path.Contains("reset-password", StringComparison.Ordinal));

            // Only the user-representation endpoints themselves — `.../users`
            // (create) and `.../users/{id}` (update) — carry a user profile
            // body; sub-resources under the same prefix (role-mappings,
            // groups) send arrays or no body at all and are not what the
            // no-password rule is about.
            var userResource = new Regex($"^/admin/realms/{Regex.Escape(options.Realm)}/users(/[^/]+)?$");

            var userRequestsWithBodies = handler.Requests
                .Where(r => userResource.IsMatch(r.Path) && r.Body is not null)
                .ToList();

            Assert.NotEmpty(userRequestsWithBodies);

            foreach (var request in userRequestsWithBodies)
            {
                var body = JsonDocument.Parse(request.Body!).RootElement;

                Assert.False(
                    body.TryGetProperty("credentials", out _),
                    $"{request.Method} {request.Path} отправил тело с полем `credentials` — запрещено "
                    + "заводить пароль фикстурному пользователю любым путём, а не только через "
                    + "reset-password.");
            }
        }
        finally
        {
            File.Delete(standFilePath);
        }
    }

    [Fact]
    public async Task ProvisionAsync_WritesTheStandFile_WithIssuerClientAndContainerName()
    {
        // The one gitignored file the suite reads, with the
        // fields the frontend stage needs — the secret, and the two staleness/resolution
        // fields (issuer, container name).
        var handler = new RecordingKeycloakHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://keycloak.invalid") };
        var standFilePath = TempFilePath();

        try
        {
            var options = Options(standFilePath);

            await KeycloakStandProvisioner.ProvisionAsync(http, options, CancellationToken.None);

            Assert.True(File.Exists(standFilePath));

            var written = JsonDocument.Parse(await File.ReadAllTextAsync(standFilePath)).RootElement;
            Assert.Equal(options.Issuer, written.GetProperty("issuer").GetString());
            Assert.Equal(options.ClientId, written.GetProperty("clientId").GetString());
            Assert.Equal(options.ClientSecret, written.GetProperty("clientSecret").GetString());
            Assert.Equal(options.KeycloakContainerName, written.GetProperty("keycloakContainerName").GetString());
        }
        finally
        {
            File.Delete(standFilePath);
        }
    }

    [Fact]
    public async Task ProvisionAsync_SetsAccessTokenLifespan_WithTheAdminToken_ViaGetModifyPut()
    {
        // AppHost sets the realm's own accessTokenLifespan
        // with the master-realm admin credentials it already holds — never
        // through `mustask-e2e`, which deliberately lacks manage-realm/view-realm
        // (see the dedicated role-set guard below). Get-modify-put, not a
        // partial PUT, so fields the fake doesn't know about survive.
        var handler = new RecordingKeycloakHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://keycloak.invalid") };
        var standFilePath = TempFilePath();

        try
        {
            await KeycloakStandProvisioner.ProvisionAsync(http, Options(standFilePath), CancellationToken.None);

            Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Get && r.Path == "/admin/realms/mustask");

            var putBody = JsonDocument.Parse(
                handler.Requests.Single(r => r.Method == HttpMethod.Put && r.Path == "/admin/realms/mustask").Body!).RootElement;

            Assert.Equal(120, putBody.GetProperty("accessTokenLifespan").GetInt32());
            // Proves get-modify-put, not a partial body that would silently
            // wipe everything the fake's GET returned but this code doesn't
            // itself care about.
            Assert.Equal("untouched", putBody.GetProperty("otherRealmField").GetString());
        }
        finally
        {
            File.Delete(standFilePath);
        }
    }

    [Fact]
    public async Task ProvisionAsync_NeverGrantsMoreThanImpersonationAndManageUsers()
    {
        // The human's own condition: "a client that can
        // already impersonate anybody does not also get the right to
        // rewrite realm configuration for the sake of test speed." Named and
        // isolated on its own — not only folded into the fresh-realm test
        // above — so a future change that widens `mustask-e2e`'s rights (e.g. to
        // avoid re-deriving this exact fix some other way) fails here first,
        // by name.
        var handler = new RecordingKeycloakHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://keycloak.invalid") };
        var standFilePath = TempFilePath();

        try
        {
            await KeycloakStandProvisioner.ProvisionAsync(http, Options(standFilePath), CancellationToken.None);

            var grantBody = JsonDocument.Parse(handler.Requests.Single(r =>
                r.Method == HttpMethod.Post && r.Path == "/admin/realms/mustask/users/service-account-user-id/role-mappings/clients/realm-management-id").Body!).RootElement;

            var grantedRoleNames = grantBody.EnumerateArray().Select(r => r.GetProperty("name").GetString()!).ToHashSet(StringComparer.Ordinal);

            Assert.Equal(new HashSet<string> { "impersonation", "manage-users" }, grantedRoleNames);
            Assert.DoesNotContain("view-realm", grantedRoleNames);
            Assert.DoesNotContain("manage-realm", grantedRoleNames);
            Assert.DoesNotContain("realm-admin", grantedRoleNames);
        }
        finally
        {
            File.Delete(standFilePath);
        }
    }

    [Fact]
    public async Task ProvisionAsync_TwoConcurrentFirings_DoNotRaceOnClientCreation()
    {
        // DCP was observed re-firing ResourceReadyEvent
        // repeatedly (~30s) after a bare `docker stop`/`start` of the `keycloak`
        // container — the same window `KeycloakStandClientSecret`'s own header
        // already reasons about for the secret. Without `KeycloakStandProvisioner`'s
        // own lock, two overlapping passes could both see "client does not exist"
        // and both `POST /clients`, and the loser's `EnsureSuccessStatusCode` would
        // turn a harmless repeat firing into a failed provisioning. The delay below
        // widens exactly that window so a real overlap is exercised, not merely two
        // sequential calls (already covered by
        // ProvisionAsync_CalledTwiceWithinOneRun_ConvergesWithoutChangingTheSecret).
        var handler = new RecordingKeycloakHandler
        {
            DelayBeforeClientExistsCheck = TimeSpan.FromMilliseconds(50),
        };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://keycloak.invalid") };
        var standFilePath = TempFilePath();

        try
        {
            var options = Options(standFilePath);

            await Task.WhenAll(
                KeycloakStandProvisioner.ProvisionAsync(http, options, CancellationToken.None),
                KeycloakStandProvisioner.ProvisionAsync(http, options, CancellationToken.None));

            // Exactly one client create — the second firing found the client
            // the first one made, instead of racing it into a 409.
            Assert.Equal(1, handler.Requests.Count(r => r.Method == HttpMethod.Post && r.Path == "/admin/realms/mustask/clients"));
            Assert.Equal(1, handler.Requests.Count(r => r.Method == HttpMethod.Put && r.Path == "/admin/realms/mustask/clients/existing-client-id"));

            // The fixture user is created once (second firing finds it), but
            // `CreateOrUpdateFixtureUserAsync` always PUTs afterwards by
            // design ("Always PUT, whether just created or already
            // existing") — that happens once per (serialized) call, so 2
            // here, not a race artifact.
            Assert.Equal(1, handler.Requests.Count(r => r.Method == HttpMethod.Post && r.Path == "/admin/realms/mustask/users"));
            Assert.Equal(2, handler.Requests.Count(r =>
                r.Method == HttpMethod.Put && r.Path == "/admin/realms/mustask/users/existing-user-id" && !r.Path.Contains("/groups/", StringComparison.Ordinal)));
        }
        finally
        {
            File.Delete(standFilePath);
        }
    }

    private static KeycloakStandProvisioningOptions Options(string standFilePath) => new()
    {
        AdminUsername = "admin",
        AdminPassword = "admin-password",
        ClientId = "mustask-e2e",
        ClientSecret = "generated-secret-value",
        FixtureUsername = "e2e-fixture",
        KeycloakContainerName = "mustask-e2e-keycloak-5010",
        Issuer = "http://localhost:5010/realms/mustask",
        StandFilePath = standFilePath,
        AccessTokenLifespanSeconds = 120,
    };

    private static string TempFilePath() => Path.Combine(Path.GetTempPath(), $"mustask-e2e-stand-test-{Guid.CreateVersion7():N}.json");

    /// <summary>
    /// Records every request `KeycloakStandProvisioner` makes and answers with
    /// the minimum a real Keycloak admin API would return for each step —
    /// enough to drive the code under test through both the "nothing exists
    /// yet" and "already provisioned" branches, never enough to simulate
    /// Keycloak itself.
    /// </summary>
    private sealed class RecordingKeycloakHandler : HttpMessageHandler
    {
        private readonly Lock _requestsLock = new();

        public List<(HttpMethod Method, string Path, string? Body)> Requests { get; } = [];

        public bool ClientExists { get; set; }

        public bool UserExists { get; set; }

        /// <summary>
        /// Widens the window between "does the
        /// client exist yet" and the create/update decision that follows
        /// it, the same shape a real overlapping `ResourceReadyEvent`
        /// re-firing would hit — used only by
        /// <see cref="KeycloakStandProvisioningTests.ProvisionAsync_TwoConcurrentFirings_DoNotRaceOnClientCreation"/>.
        /// </summary>
        public TimeSpan? DelayBeforeClientExistsCheck { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            lock (_requestsLock)
            {
                Requests.Add((request.Method, path, body));
            }

            if (DelayBeforeClientExistsCheck is { } delay
                && request.Method == HttpMethod.Get
                && path.StartsWith("/admin/realms/mustask/clients?clientId=mustask-e2e", StringComparison.Ordinal))
            {
                await Task.Delay(delay, cancellationToken);
            }

            if (request.Method == HttpMethod.Post && path == "/realms/master/protocol/openid-connect/token")
            {
                return Json("""{"access_token":"fake-admin-token"}""");
            }

            // Get-modify-put on the full realm representation, not a partial
            // body — `otherRealmField` stands in for everything a partial PUT
            // would otherwise silently wipe (clients, themes, ...).
            if (request.Method == HttpMethod.Get && path == "/admin/realms/mustask")
            {
                return Json("""{"realm":"mustask","accessTokenLifespan":900,"otherRealmField":"untouched"}""");
            }

            if (request.Method == HttpMethod.Put && path == "/admin/realms/mustask")
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (request.Method == HttpMethod.Get && path.StartsWith("/admin/realms/mustask/clients?clientId=mustask-e2e", StringComparison.Ordinal))
            {
                return Json(ClientExists ? """[{"id":"existing-client-id"}]""" : "[]");
            }

            if (request.Method == HttpMethod.Post && path == "/admin/realms/mustask/clients")
            {
                ClientExists = true;
                return new HttpResponseMessage(HttpStatusCode.Created);
            }

            if (request.Method == HttpMethod.Put && path == "/admin/realms/mustask/clients/existing-client-id")
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (request.Method == HttpMethod.Get && path == "/admin/realms/mustask/clients/existing-client-id/service-account-user")
            {
                return Json("""{"id":"service-account-user-id"}""");
            }

            if (request.Method == HttpMethod.Get && path == "/admin/realms/mustask/clients?clientId=realm-management")
            {
                return Json("""[{"id":"realm-management-id"}]""");
            }

            if (request.Method == HttpMethod.Get && path == "/admin/realms/mustask/clients/realm-management-id/roles")
            {
                return Json("""[{"id":"role-impersonation-id","name":"impersonation"},{"id":"role-manage-users-id","name":"manage-users"},{"id":"role-view-users-id","name":"view-users"}]""");
            }

            if (request.Method == HttpMethod.Post && path == "/admin/realms/mustask/users/service-account-user-id/role-mappings/clients/realm-management-id")
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (request.Method == HttpMethod.Get && path.StartsWith("/admin/realms/mustask/users?username=e2e-fixture", StringComparison.Ordinal))
            {
                return Json(UserExists ? """[{"id":"existing-user-id"}]""" : "[]");
            }

            if (request.Method == HttpMethod.Post && path == "/admin/realms/mustask/users")
            {
                UserExists = true;
                return new HttpResponseMessage(HttpStatusCode.Created);
            }

            if (request.Method == HttpMethod.Put
                && path == "/admin/realms/mustask/users/existing-user-id"
                && !path.Contains("/groups/", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (request.Method == HttpMethod.Get && path.StartsWith("/admin/realms/mustask/group-by-path/", StringComparison.Ordinal))
            {
                return Json("""{"id":"group-admins-id"}""");
            }

            if (request.Method == HttpMethod.Put && path == "/admin/realms/mustask/users/existing-user-id/groups/group-admins-id")
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            throw new InvalidOperationException($"Unexpected request in KeycloakStandProvisioningTests: {request.Method} {path}");
        }

        private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }
}
