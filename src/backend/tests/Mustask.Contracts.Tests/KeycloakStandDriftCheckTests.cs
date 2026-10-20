using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Mustask.AppHost.Provisioning;
using Mustask.Domain.Identity;

namespace Mustask.Contracts.Tests;

/// <summary>
/// mod-014 US-4 "A realm import that differs from the live realm fails the
/// start" (D-10 (a); ADR-0025 § 7): the drift check against a fake Keycloak
/// admin surface whose live realm starts equal to the real realm import and
/// is then changed one way at a time. No Docker, no Keycloak. tech-024
/// (ADR-0031): the import declares four client roles and no realm roles or
/// groups, and the realm's name is a value the stand takes from configuration.
/// </summary>
public sealed class KeycloakStandDriftCheckTests
{
    internal const string PanelSecret = "panel-secret-example-not-real-0123456789";

    internal const string AdminPassword = "admin-password-example-not-real";

    private static readonly Dictionary<string, string> StandValues = new(StringComparer.Ordinal)
    {
        ["MUSTASK_PANEL_URL"] = "http://localhost:3000",
        ["MUSTASK_API_URL"] = "http://localhost:5000",
        ["MUSTASK_PANEL_CLIENT_SECRET"] = PanelSecret,
    };

    internal static KeycloakRealmImport Import(IReadOnlyDictionary<string, string>? values = null, string realm = "mustask") =>
        KeycloakRealmImport.Load(
            Path.Combine(RepositoryLayout.Root.FullName, "deploy", "keycloak", "realms"), realm, values ?? StandValues);

    private static async Task<IReadOnlyList<string>> CheckAsync(
        FakeKeycloak live,
        KeycloakRealmImport? import = null,
        string adminPassword = AdminPassword,
        string panelSecret = PanelSecret)
    {
        using var http = new HttpClient(live) { BaseAddress = new Uri("http://keycloak.test") };

        return await KeycloakStandDriftCheck.RunAsync(
            http,
            new KeycloakDriftCheckOptions
            {
                Import = import ?? Import(),
                AdminUsername = "admin",
                AdminPassword = adminPassword,
                SecretClientId = "mustask-panel",
                SecretClientSecret = panelSecret,
            },
            CancellationToken.None);
    }

    private static void AssertOneFindingNamingTheReset(IReadOnlyList<string> findings, string mentions)
    {
        var finding = Assert.Single(findings);

        Assert.Contains(mentions, finding, StringComparison.Ordinal);
        Assert.Contains("scripts/stand-reset.sh", finding, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ALiveRealmMadeFromTheImport_HasNoFinding_EvenWithUndeclaredClientsGroupsAndBuiltIns()
    {
        var live = FakeKeycloak.FromImport(Import(), PanelSecret, AdminPassword);
        live.Groups.Add("/Отдел продаж"); // the company realm has groups of its own: never compared

        Assert.Empty(await CheckAsync(live));
    }

    [Fact]
    public async Task ARealmRoleOnlyInTheLiveRealm_Fails()
    {
        var live = FakeKeycloak.FromImport(Import(), PanelSecret, AdminPassword);
        live.RealmRoles["auditor"] = (new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, HashSet<string>>(StringComparer.Ordinal));

        AssertOneFindingNamingTheReset(await CheckAsync(live), "`auditor`");
    }

    // A realm role that an import declares (a future import may) is compared with its composites.
    [Fact]
    public async Task ACompositeThatDiffers_Fails()
    {
        var import = KeycloakRealmImport.Parse(
            """
            { "realm": "mustask", "roles": { "realm": [ { "name": "legacy", "composite": true, "composites": { "client": { "mustask-api": [ "user" ] } } } ] } }
            """,
            StandValues);
        var live = FakeKeycloak.FromImport(import, PanelSecret, AdminPassword);
        live.Clients["mustask-api"] = new FakeClient("id-mustask-api", [], null, [], null, null);

        Assert.Empty(await CheckAsync(live, import));

        live.RealmRoles["legacy"].Client["mustask-api"].Add("system-admin");

        AssertOneFindingNamingTheReset(await CheckAsync(live, import), "`legacy`");
    }

    // tech-024: the default role (default-roles-<realm>) is Keycloak's own — never declared and never compared, whatever the realm is called.
    [Theory]
    [InlineData("mustask")]
    [InlineData("company")]
    public async Task TheDefaultRolesOfTheLiveRealm_AreNotCompared_UnderTheConfiguredRealmName(string realm)
    {
        var import = Import(WithRealm(realm), realm);
        var live = FakeKeycloak.FromImport(import, PanelSecret, AdminPassword);
        live.RealmRoles[$"default-roles-{realm}"].Client["mustask-api"] = ["user"];

        Assert.Equal(realm, import.Realm);
        Assert.Empty(await CheckAsync(live, import));
    }

    // tech-024 · realm name configuration: the live realm is looked for under the configured name, and the finding names it.
    [Fact]
    public async Task TheRealmNameIsConfigurable_TheConfiguredRealmIsTheOneChecked()
    {
        var import = Import(WithRealm("company"), "company");
        var live = FakeKeycloak.FromImport(import, PanelSecret, AdminPassword);

        Assert.Empty(await CheckAsync(live, import));

        live.RealmExists = false;

        AssertOneFindingNamingTheReset(await CheckAsync(live, import), "`company`");
    }

    // tech-024: a store created from the import before the client-role model (realm roles instance-admin, dictionaries-admin,
    // organization-creator, groups, raw permission codes as client roles), checked against the new one, fails with findings that
    // name the reset — the live-realm command that once converted older stores is retired; the reset picks up the new import.
    [Fact]
    public async Task ARealmMadeFromThePreClientRolesImport_FailsNamingTheReset()
    {
        var oldImport = KeycloakRealmImport.Parse(File.ReadAllText(PreTech024RealmPath), StandValues);
        var live = FakeKeycloak.FromImport(oldImport, PanelSecret, AdminPassword);

        var findings = await CheckAsync(live);

        Assert.Contains(findings, finding => finding.StartsWith("Realm role `instance-admin` is in the live realm but not in the realm import", StringComparison.Ordinal));
        Assert.Contains(findings, finding => finding.StartsWith("Realm role `dictionaries-admin` is in the live realm but not in the realm import", StringComparison.Ordinal));
        Assert.Contains(findings, finding => finding.StartsWith("Realm role `organization-creator` is in the live realm but not in the realm import", StringComparison.Ordinal));

        foreach (var role in InstanceRoles.Names)
        {
            Assert.Contains(findings, finding => finding.StartsWith($"Client role `mustask-api/{role}` is in the realm import but not in the live realm", StringComparison.Ordinal));
        }

        foreach (var code in PermissionCatalog.InstanceCodes)
        {
            Assert.Contains(findings, finding => finding.StartsWith($"Client role `mustask-api/{code}` is in the live realm but not in the realm import", StringComparison.Ordinal));
        }

        Assert.Equal(3 + InstanceRoles.Names.Count + PermissionCatalog.InstanceCodes.Count, findings.Count);
        Assert.All(findings, finding => Assert.Contains("scripts/stand-reset.sh", finding, StringComparison.Ordinal));
    }

    /// <summary>The import as it was before tech-024, kept as test data: the realm of a stand created from it.</summary>
    internal static string PreTech024RealmPath =>
        Path.Combine(RepositoryLayout.Root.FullName, "src", "backend", "tests", "Mustask.Contracts.Tests", "TestData", "mustask-realm.pre-tech-024.json");

    [Fact]
    public async Task AClientRoleMissingOrExtra_Fails()
    {
        var live = FakeKeycloak.FromImport(Import(), PanelSecret, AdminPassword);
        live.Clients["mustask-api"].Roles.Add("work-items.read");

        AssertOneFindingNamingTheReset(await CheckAsync(live), "`mustask-api/work-items.read`");

        var lacking = FakeKeycloak.FromImport(Import(), PanelSecret, AdminPassword);
        lacking.Clients["mustask-api"].Roles.Remove("system-admin");

        AssertOneFindingNamingTheReset(await CheckAsync(lacking), "`mustask-api/system-admin`");
    }

    [Fact]
    public async Task TheListenerNotEnabledInTheLiveRealm_Fails()
    {
        var live = FakeKeycloak.FromImport(Import(), PanelSecret, AdminPassword);
        live.Listeners.Remove(UserProjectionIdentifiers.ProviderId);

        AssertOneFindingNamingTheReset(await CheckAsync(live), UserProjectionIdentifiers.ProviderId);
    }

    // R-13 / D-9: a changed administrator password is announced at the start, by name, never printed.
    [Fact]
    public async Task AChangedAdministratorPassword_FailsNamingTheParameter_NeverTheValue()
    {
        var live = FakeKeycloak.FromImport(Import(), PanelSecret, AdminPassword);

        var findings = await CheckAsync(live, adminPassword: "a-new-admin-password-not-real");

        AssertOneFindingNamingTheReset(findings, "Parameters:keycloak-admin-password");
        Assert.DoesNotContain("a-new-admin-password-not-real", findings[0], StringComparison.Ordinal);
        Assert.DoesNotContain(AdminPassword, findings[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AChangedPanelSecret_FailsNamingTheParameter_NeverEitherValue()
    {
        var live = FakeKeycloak.FromImport(Import(), PanelSecret, AdminPassword);

        var findings = await CheckAsync(live, panelSecret: "a-new-panel-secret-not-real-0123456789");

        AssertOneFindingNamingTheReset(findings, "Parameters:keycloak-panel-client-secret");
        Assert.DoesNotContain(PanelSecret, findings[0], StringComparison.Ordinal);
        Assert.DoesNotContain("a-new-panel-secret-not-real-0123456789", findings[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AChangedStandPort_FailsNamingThePorts()
    {
        var live = FakeKeycloak.FromImport(Import(), PanelSecret, AdminPassword);
        var movedPanel = new Dictionary<string, string>(StandValues, StringComparer.Ordinal) { ["MUSTASK_PANEL_URL"] = "http://localhost:3001" };

        var findings = await CheckAsync(live, Import(movedPanel));

        AssertOneFindingNamingTheReset(findings, "Ports:Frontend");
    }

    [Fact]
    public async Task ARealmMissingFromTheStore_Fails()
    {
        var live = FakeKeycloak.FromImport(Import(), PanelSecret, AdminPassword);
        live.RealmExists = false;

        AssertOneFindingNamingTheReset(await CheckAsync(live), "`mustask`");
    }

    [Fact]
    public void TheImport_IsReadWithTheStandsPlaceholders()
    {
        var import = Import();
        var panel = import.Clients["mustask-panel"];

        Assert.Equal("mustask", import.Realm);
        Assert.Equal("http://localhost:3000", panel.RootUrl);
        Assert.Contains("http://localhost:5000/api/session/callback", panel.RedirectUris);
        Assert.True(panel.HasSecret);
    }

    // tech-024: the realm name is a placeholder of the import, resolved with the stand's value; found by that name among the files.
    [Fact]
    public void TheImport_TakesItsRealmNameFromThePlaceholder()
    {
        Assert.Equal("company", Import(WithRealm("company"), "company").Realm);
        Assert.Throws<InvalidOperationException>(() => Import(WithRealm("company"), "mustask"));
    }

    // tech-024: the client roles of mustask-api are the four of the table; no realm role is declared, so nothing is compared but them.
    [Fact]
    public void TheImport_DeclaresTheFourClientRoles_AndNoRealmRoles()
    {
        var import = Import();

        Assert.Empty(import.RealmRoles);
        Assert.Equal(InstanceRoles.Names.Order(StringComparer.Ordinal), import.Clients["mustask-api"].Roles.Order(StringComparer.Ordinal));
    }

    private static Dictionary<string, string> WithRealm(string realm) =>
        new(StandValues, StringComparer.Ordinal) { ["MUSTASK_REALM"] = realm };

    /// <summary>A Keycloak admin surface over an in-memory realm (and the realm's public endpoint).</summary>
    internal sealed class FakeKeycloak : HttpMessageHandler
    {
        public string Realm { get; private init; } = "mustask";

        public bool RealmExists { get; set; } = true;

        /// <summary>How many times the master administrator signed in — once per drift check run.</summary>
        public int AdminSignIns { get; private set; }

        public string AdminPassword { get; private init; } = string.Empty;

        public List<string> Listeners { get; } = [];

        public Dictionary<string, (HashSet<string> Realm, Dictionary<string, HashSet<string>> Client)> RealmRoles { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, FakeClient> Clients { get; } = new(StringComparer.Ordinal);

        /// <summary>Groups of the live realm: present, never compared (the company realm has its own).</summary>
        public List<string> Groups { get; } = [];

        public static FakeKeycloak FromImport(KeycloakRealmImport import, string panelSecret, string adminPassword)
        {
            var live = new FakeKeycloak { AdminPassword = adminPassword, Realm = import.Realm };
            live.Listeners.AddRange(import.EventsListeners);

            foreach (var role in import.RealmRoles.Values)
            {
                live.RealmRoles[role.Name] = (role.RealmComposites.ToHashSet(StringComparer.Ordinal), Copy(role.ClientComposites));
            }

            // Keycloak's own: every realm has them, declared by the import or not.
            live.RealmRoles.TryAdd("offline_access", ([], new(StringComparer.Ordinal)));
            live.RealmRoles.TryAdd("uma_authorization", ([], new(StringComparer.Ordinal)));
            live.RealmRoles.TryAdd(
                "default-roles-" + import.Realm,
                (["offline_access", "uma_authorization"], new(StringComparer.Ordinal) { ["account"] = ["view-profile", "manage-account"] }));

            foreach (var client in import.Clients.Values)
            {
                live.Clients[client.ClientId] = new FakeClient(
                    "id-" + client.ClientId, client.Roles.ToHashSet(StringComparer.Ordinal), client.RootUrl, client.RedirectUris.ToList(),
                    client.PostLogoutRedirectUris, client.HasSecret ? panelSecret : null);
            }

            // ADR-0023's stand-only client and Keycloak's own: never compared.
            live.Clients["mustask-e2e"] = new FakeClient("id-mustask-e2e", ["something"], null, [], null, "e2e-secret");
            live.Clients["account"] = new FakeClient("id-account", ["manage-account"], "${authBaseUrl}", [$"/realms/{import.Realm}/account/*"], null, null);

            return live;
        }

        private static Dictionary<string, HashSet<string>> Copy(IReadOnlyDictionary<string, IReadOnlySet<string>> byClient) =>
            byClient.ToDictionary(pair => pair.Key, pair => pair.Value.ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;

            // What the stand's health check asks first, unauthenticated: does the realm answer.
            if (request.Method == HttpMethod.Get && path == $"/realms/{Realm}")
            {
                return new HttpResponseMessage(RealmExists ? HttpStatusCode.OK : HttpStatusCode.NotFound);
            }

            if (request.Method == HttpMethod.Post && path == "/realms/master/protocol/openid-connect/token")
            {
                AdminSignIns++;
                var form = await request.Content!.ReadAsStringAsync(cancellationToken);

                return form.Contains("password=" + Uri.EscapeDataString(AdminPassword), StringComparison.Ordinal)
                    ? Json(new JsonObject { ["access_token"] = "token" })
                    : new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            Assert.Equal("Bearer token", request.Headers.Authorization?.ToString());

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString).ToArray();

            if (segments.Length < 3 || segments[0] != "admin" || segments[1] != "realms" || segments[2] != Realm || !RealmExists)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return segments[3..] switch
            {
                [] => Json(new JsonObject { ["realm"] = Realm, ["eventsListeners"] = Array(Listeners) }),
                ["clients"] => Json(new JsonArray(Clients.Select(pair => (JsonNode)ClientJson(pair.Key, pair.Value)).ToArray())),
                ["clients", var id, "roles"] => Json(RolesJson(ClientById(id).Roles)),
                ["clients", var id, "client-secret"] => Json(new JsonObject { ["value"] = ClientById(id).Secret }),
                ["roles"] => Json(new JsonArray(RealmRoles.Select(pair => (JsonNode)new JsonObject
                {
                    ["name"] = pair.Key,
                    ["composite"] = pair.Value.Realm.Count + pair.Value.Client.Sum(client => client.Value.Count) > 0,
                }).ToArray())),
                ["roles", var name, "composites"] => Json(CompositesJson(RealmRoles[name])),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }

        private FakeClient ClientById(string id) => Clients.Values.Single(client => client.Id == id);

        private static JsonObject ClientJson(string clientId, FakeClient client)
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

        private JsonArray CompositesJson((HashSet<string> Realm, Dictionary<string, HashSet<string>> Client) role)
        {
            var composites = new JsonArray();

            foreach (var name in role.Realm)
            {
                composites.Add(new JsonObject { ["name"] = name, ["clientRole"] = false, ["containerId"] = "realm-id" });
            }

            foreach (var (clientId, names) in role.Client)
            {
                foreach (var name in names)
                {
                    composites.Add(new JsonObject { ["name"] = name, ["clientRole"] = true, ["containerId"] = Clients[clientId].Id });
                }
            }

            return composites;
        }

        private static JsonArray RolesJson(IEnumerable<string> names) =>
            new(names.Select(name => (JsonNode)new JsonObject { ["name"] = name }).ToArray());

        private static JsonArray Array(IEnumerable<string> values) =>
            new(values.Select(value => (JsonNode)JsonValue.Create(value)!).ToArray());

        private static HttpResponseMessage Json(JsonNode body) =>
            new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
    }

    internal sealed record FakeClient(string Id, HashSet<string> Roles, string? RootUrl, List<string> RedirectUris, string? PostLogout, string? Secret);
}
