using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Mustask.AppHost.Provisioning;

namespace Mustask.Contracts.Tests;

/// <summary>
/// mod-014 US-4 "A realm import that differs from the live realm fails the
/// start" (D-10 (a); ADR-0025 § 7): the drift check against a fake Keycloak
/// admin surface whose live realm starts equal to the real realm import and
/// is then changed one way at a time. No Docker, no Keycloak.
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

    internal static KeycloakRealmImport Import(IReadOnlyDictionary<string, string>? values = null) =>
        KeycloakRealmImport.Load(
            Path.Combine(RepositoryLayout.Root.FullName, "deploy", "keycloak", "realms"), "mustask", values ?? StandValues);

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
        Assert.Empty(await CheckAsync(FakeKeycloak.FromImport(Import(), PanelSecret, AdminPassword)));
    }

    // US-4: e.g. mod-002 adds a realm role to the import; a store created before never gets it.
    [Fact]
    public async Task ARealmRoleAddedToTheImport_FailsNamingItAndTheReset()
    {
        var live = FakeKeycloak.FromImport(Import(), PanelSecret, AdminPassword);
        live.RealmRoles.Remove("dictionaries-admin");
        live.Groups["/Администраторы справочников"].Realm.Clear();

        var findings = await CheckAsync(live);

        Assert.Contains(findings, finding => finding.Contains("`dictionaries-admin`", StringComparison.Ordinal) && finding.Contains("scripts/stand-reset.sh", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARealmRoleOnlyInTheLiveRealm_Fails()
    {
        var live = FakeKeycloak.FromImport(Import(), PanelSecret, AdminPassword);
        live.RealmRoles["auditor"] = (new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, HashSet<string>>(StringComparer.Ordinal));

        AssertOneFindingNamingTheReset(await CheckAsync(live), "`auditor`");
    }

    [Fact]
    public async Task ACompositeThatDiffers_Fails()
    {
        var live = FakeKeycloak.FromImport(Import(), PanelSecret, AdminPassword);
        live.RealmRoles["dictionaries-admin"].Client["mustask-api"].Add("organizations.organization.create");

        AssertOneFindingNamingTheReset(await CheckAsync(live), "`dictionaries-admin`");
    }

    // mod-016 BR-M1-37: the default roles are declared in the import, so their composites are compared exactly — the read code every
    // user gets is part of the realm, and a live realm whose default role lacks it (a store created before mod-016) is not ready.
    [Fact]
    public async Task TheDefaultRoleWithoutTheReadCode_FailsNamingItAndTheReset()
    {
        var live = FakeKeycloak.FromImport(Import(), PanelSecret, AdminPassword);
        live.RealmRoles["default-roles-mustask"].Client["mustask-api"].Remove("dictionaries.languages.read");

        AssertOneFindingNamingTheReset(await CheckAsync(live), "`default-roles-mustask`");
    }

    // The same for an extra composite: a default role that grants a code to everyone is as much a drift as one that lacks one.
    [Fact]
    public async Task TheDefaultRoleWithAnExtraCode_FailsNamingIt()
    {
        var live = FakeKeycloak.FromImport(Import(), PanelSecret, AdminPassword);
        live.RealmRoles["default-roles-mustask"].Client["mustask-api"].Add("dictionaries.languages.write");

        AssertOneFindingNamingTheReset(await CheckAsync(live), "`default-roles-mustask`");
    }

    // mod-016 BR-M1-37, US-8 "The stand comes up ready after the script": a live realm created from the PRE-mod-016 import, checked
    // against the new one, fails with exactly the findings src/backend/AGENTS.md names — the realm roles admin, member and viewer,
    // the missing instance-admin, the default role's composites and the mappings of /Администраторы — and nothing else; the command
    // of BR-M1-28 clears all of them (KeycloakLiveRealmMigrationTests), not the reset the findings name.
    [Fact]
    public async Task TheOldRealm_FailsWithExactlyTheFindingsTheAgentsFileNames()
    {
        var oldImport = KeycloakRealmImport.Parse(File.ReadAllText(PreMod016RealmPath), StandValues);
        var live = FakeKeycloak.FromImport(oldImport, PanelSecret, AdminPassword);

        var findings = await CheckAsync(live);

        Assert.Equal(6, findings.Count);
        Assert.Contains(findings, finding => finding.StartsWith("Realm role `admin` is in the live realm but not in the realm import", StringComparison.Ordinal));
        Assert.Contains(findings, finding => finding.StartsWith("Realm role `member` is in the live realm but not in the realm import", StringComparison.Ordinal));
        Assert.Contains(findings, finding => finding.StartsWith("Realm role `viewer` is in the live realm but not in the realm import", StringComparison.Ordinal));
        Assert.Contains(findings, finding => finding.StartsWith("Realm role `instance-admin` is in the realm import but not in the live realm", StringComparison.Ordinal));
        Assert.Contains(findings, finding => finding.StartsWith("Realm role `default-roles-mustask` has other composites", StringComparison.Ordinal));
        Assert.Contains(findings, finding => finding.StartsWith("Group `/Администраторы` has other role mappings", StringComparison.Ordinal));
        Assert.All(findings, finding => Assert.Contains("scripts/stand-reset.sh", finding, StringComparison.Ordinal));
    }

    /// <summary>The import as it was before mod-016, kept as test data: the realm of the human's stand today.</summary>
    internal static string PreMod016RealmPath =>
        Path.Combine(RepositoryLayout.Root.FullName, "src", "backend", "tests", "Mustask.Contracts.Tests", "TestData", "mustask-realm.pre-mod-016.json");

    [Fact]
    public async Task AClientRoleMissingOrExtra_Fails()
    {
        var live = FakeKeycloak.FromImport(Import(), PanelSecret, AdminPassword);
        live.Clients["mustask-api"].Roles.Add("work-items.read");

        AssertOneFindingNamingTheReset(await CheckAsync(live), "`mustask-api/work-items.read`");
    }

    [Fact]
    public async Task TheListenerNotEnabledInTheLiveRealm_Fails()
    {
        var live = FakeKeycloak.FromImport(Import(), PanelSecret, AdminPassword);
        live.Listeners.Remove(UserProjectionIdentifiers.ProviderId);

        AssertOneFindingNamingTheReset(await CheckAsync(live), UserProjectionIdentifiers.ProviderId);
    }

    [Fact]
    public async Task ADeclaredGroupMissingOrMappedOtherwise_Fails()
    {
        var missing = FakeKeycloak.FromImport(Import(), PanelSecret, AdminPassword);
        missing.Groups.Remove("/Администраторы справочников");

        AssertOneFindingNamingTheReset(await CheckAsync(missing), "`/Администраторы справочников`");

        var remapped = FakeKeycloak.FromImport(Import(), PanelSecret, AdminPassword);
        remapped.Groups["/Создатели организаций"].Realm.Add("dictionaries-admin");

        AssertOneFindingNamingTheReset(await CheckAsync(remapped), "`/Создатели организаций`");
    }

    // ADR-0025 § 7 / D-16: groups are compared one way — a group created in the console does not stop the stand.
    [Fact]
    public async Task AGroupOnlyInTheLiveRealm_IsIgnored()
    {
        var live = FakeKeycloak.FromImport(Import(), PanelSecret, AdminPassword);
        live.Groups["/Отдел продаж"] = (new HashSet<string>(StringComparer.Ordinal) { "dictionaries-admin" }, new Dictionary<string, HashSet<string>>(StringComparer.Ordinal));

        Assert.Empty(await CheckAsync(live));
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
        var panel = Import().Clients["mustask-panel"];

        Assert.Equal("http://localhost:3000", panel.RootUrl);
        Assert.Contains("http://localhost:5000/api/session/callback", panel.RedirectUris);
        Assert.True(panel.HasSecret);
        Assert.Equal(
            ["/Администраторы", "/Администраторы справочников", "/Создатели организаций"],
            Import().Groups.Select(group => group.Path).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(Import().Groups, group => group.Path.StartsWith("/Сотрудники", StringComparison.Ordinal));
    }

    // mod-016 BR-M1-37: the import's default roles are read like any other declared role — the drift check and the contract tests
    // read the one declaration.
    [Fact]
    public void TheImport_DeclaresTheDefaultRoleWithItsComposites()
    {
        var role = Import().RealmRoles["default-roles-mustask"];

        Assert.Equal(["offline_access", "uma_authorization"], role.RealmComposites.Order(StringComparer.Ordinal));
        Assert.Equal(["dictionaries.languages.read"], role.ClientComposites["mustask-api"]);
        Assert.Equal(["manage-account", "view-profile"], role.ClientComposites["account"].Order(StringComparer.Ordinal));
    }

    /// <summary>A Keycloak admin surface over an in-memory realm (and the realm's public endpoint).</summary>
    internal sealed class FakeKeycloak : HttpMessageHandler
    {
        public bool RealmExists { get; set; } = true;

        /// <summary>How many times the master administrator signed in — once per drift check run.</summary>
        public int AdminSignIns { get; private set; }

        public string AdminPassword { get; private init; } = string.Empty;

        public List<string> Listeners { get; } = [];

        public Dictionary<string, (HashSet<string> Realm, Dictionary<string, HashSet<string>> Client)> RealmRoles { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, FakeClient> Clients { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, (HashSet<string> Realm, Dictionary<string, HashSet<string>> Client)> Groups { get; } = new(StringComparer.Ordinal);

        public static FakeKeycloak FromImport(KeycloakRealmImport import, string panelSecret, string adminPassword)
        {
            var live = new FakeKeycloak { AdminPassword = adminPassword };
            live.Listeners.AddRange(import.EventsListeners);

            foreach (var role in import.RealmRoles.Values)
            {
                live.RealmRoles[role.Name] = (role.RealmComposites.ToHashSet(StringComparer.Ordinal), Copy(role.ClientComposites));
            }

            // Keycloak's own: every realm has them. An import declares them (mod-016: the realm's default roles are in the import,
            // BR-M1-37) and the lines above copied the declaration; an import that does not — the pre-mod-016 one kept as test
            // data — gets them as Keycloak creates them, the default role with the account client's two roles it always carries.
            live.RealmRoles.TryAdd("offline_access", ([], new(StringComparer.Ordinal)));
            live.RealmRoles.TryAdd("uma_authorization", ([], new(StringComparer.Ordinal)));
            live.RealmRoles.TryAdd(
                "default-roles-mustask",
                (["offline_access", "uma_authorization"], new(StringComparer.Ordinal) { ["account"] = ["view-profile", "manage-account"] }));

            foreach (var client in import.Clients.Values)
            {
                live.Clients[client.ClientId] = new FakeClient(
                    "id-" + client.ClientId, client.Roles.ToHashSet(StringComparer.Ordinal), client.RootUrl, client.RedirectUris.ToList(),
                    client.PostLogoutRedirectUris, client.HasSecret ? panelSecret : null);
            }

            // ADR-0023's stand-only client and Keycloak's own: never compared.
            live.Clients["mustask-e2e"] = new FakeClient("id-mustask-e2e", ["something"], null, [], null, "e2e-secret");
            live.Clients["account"] = new FakeClient("id-account", ["manage-account"], "${authBaseUrl}", ["/realms/mustask/account/*"], null, null);

            foreach (var group in import.Groups)
            {
                live.Groups[group.Path] = (group.RealmRoles.ToHashSet(StringComparer.Ordinal), Copy(group.ClientRoles));
            }

            return live;
        }

        private static Dictionary<string, HashSet<string>> Copy(IReadOnlyDictionary<string, IReadOnlySet<string>> byClient) =>
            byClient.ToDictionary(pair => pair.Key, pair => pair.Value.ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;

            // What the stand's health check asks first, unauthenticated: does realm mustask answer.
            if (request.Method == HttpMethod.Get && path == "/realms/mustask")
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

            if (segments is not ["admin", "realms", "mustask", ..] || !RealmExists)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return segments[3..] switch
            {
                [] => Json(new JsonObject { ["realm"] = "mustask", ["eventsListeners"] = Array(Listeners) }),
                ["clients"] => Json(new JsonArray(Clients.Select(pair => (JsonNode)ClientJson(pair.Key, pair.Value)).ToArray())),
                ["clients", var id, "roles"] => Json(RolesJson(ClientById(id).Roles)),
                ["clients", var id, "client-secret"] => Json(new JsonObject { ["value"] = ClientById(id).Secret }),
                ["roles"] => Json(new JsonArray(RealmRoles.Select(pair => (JsonNode)new JsonObject
                {
                    ["name"] = pair.Key,
                    ["composite"] = pair.Value.Realm.Count + pair.Value.Client.Sum(client => client.Value.Count) > 0,
                }).ToArray())),
                ["roles", var name, "composites"] => Json(CompositesJson(RealmRoles[name])),
                ["group-by-path", .. var groupPath] => Groups.ContainsKey("/" + string.Join('/', groupPath))
                    ? Json(new JsonObject { ["id"] = GroupId("/" + string.Join('/', groupPath)) })
                    : new HttpResponseMessage(HttpStatusCode.NotFound),
                ["groups", var groupId, "role-mappings"] => Json(MappingsJson(Groups[Groups.Keys.Single(key => GroupId(key) == groupId)])),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }

        private FakeClient ClientById(string id) => Clients.Values.Single(client => client.Id == id);

        /// <summary>An id with no slash in it, as Keycloak's are.</summary>
        private static string GroupId(string path) =>
            "g-" + Convert.ToHexStringLower(System.Text.Encoding.UTF8.GetBytes(path));

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

        private static JsonObject MappingsJson((HashSet<string> Realm, Dictionary<string, HashSet<string>> Client) group)
        {
            var byClient = new JsonObject();

            foreach (var (clientId, names) in group.Client)
            {
                byClient[clientId] = new JsonObject { ["client"] = clientId, ["mappings"] = RolesJson(names) };
            }

            return new JsonObject { ["realmMappings"] = RolesJson(group.Realm), ["clientMappings"] = byClient };
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
