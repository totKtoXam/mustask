using System.Text.Json.Nodes;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Realm.Live;

/// <summary>
/// mod-016 plan-backend § 4.3 (the spike that stays), reshaped by tech-024 (ADR-0033): Keycloak imports the repository's <b>real</b>
/// realm file and the realm is what the contract tests and the stand's drift check read from it - no realm role of ours, no group,
/// the four client roles of <c>mustask-api</c> with their descriptions, a token that carries the role names a user holds directly,
/// and the stand's own drift check reporting nothing. A file test cannot promise any of this: Keycloak rejects or reshapes an import
/// silently (the default role, for one, must be created by Keycloak when the import declares none). Opt-in
/// (<see cref="LiveRealmFactAttribute"/>): <c>scripts/verify.sh</c>'s <c>keycloak</c> bucket runs it whenever a realm file changes
/// (D-16 OP-10).
/// </summary>
[Trait("Category", "KeycloakLive")]
public sealed class LiveRealmImportTests
{
    private static string RealmsDirectory => Path.Combine(RepositoryRoot(), "deploy", "keycloak", "realms");

    private static string RealmFile => Path.Combine(RealmsDirectory, "mustask-realm.json");

    internal static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Mustask.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Mustask.sln not found above the test binaries.");
    }

    [LiveRealmFact]
    public async Task TheRepositorysRealm_IsImportedByKeycloak_AsTheContractTestsAndTheDriftCheckReadIt()
    {
        await using var keycloak = await LiveRealmContainer.StartAsync(File.ReadAllText(RealmFile));

        // The realm roles Keycloak holds are the ones it creates itself: no business role, no group (ADR-0033).
        var (_, roles, _) = await keycloak.AdminAsync(HttpMethod.Get, "/roles?briefRepresentation=false");
        var names = ((JsonArray)roles!).Select(role => (string)role!["name"]!).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["default-roles-mustask", "offline_access", "uma_authorization"], names);

        var (_, groups, _) = await keycloak.AdminAsync(HttpMethod.Get, "/groups");
        Assert.Empty((JsonArray)groups!);

        // The default role is Keycloak's own and carries no code of mustask-api.
        var (_, realm, _) = await keycloak.AdminAsync(HttpMethod.Get, string.Empty);
        Assert.Equal("default-roles-mustask", (string)realm!["defaultRole"]!["name"]!);
        var (_, composites, _) = await keycloak.AdminAsync(HttpMethod.Get, "/roles/default-roles-mustask/composites");
        var (_, clients, _) = await keycloak.AdminAsync(HttpMethod.Get, "/clients");
        var clientIds = ((JsonArray)clients!).ToDictionary(client => (string)client!["id"]!, client => (string)client!["clientId"]!);
        var held = ((JsonArray)composites!)
            .Select(role => ((bool)role!["clientRole"]! ? clientIds[(string)role["containerId"]!] : "realm") + "/" + (string)role["name"]!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["account/manage-account", "account/view-profile", "realm/offline_access", "realm/uma_authorization"], held);

        // The four client roles of mustask-api, with the descriptions of the table.
        var apiId = clientIds.Single(pair => pair.Value == "mustask-api").Key;
        var (_, apiRoles, _) = await keycloak.AdminAsync(HttpMethod.Get, $"/clients/{apiId}/roles");
        Assert.Equal(
            InstanceRoles.All.Select(role => (role.Name, role.Description)).Order(),
            ((JsonArray)apiRoles!).Select(role => ((string)role!["name"]!, (string)role["description"]!)).Order());

        // Tokens: what a user holds directly arrives as resource_access.mustask-api.roles, the names and nothing else; a user with no
        // role has no mustask-api entry at all.
        var nobody = await keycloak.CreateUserAsync("live.nobody");
        var plain = await keycloak.CreateUserAsync("live.user", "user");
        var administrator = await keycloak.CreateUserAsync("live.admin", "user", "system-admin");
        var creator = await keycloak.CreateUserAsync("live.creator", "user", "organization-creator");

        Assert.Empty(await keycloak.ApiRolesOfAsync(nobody));
        Assert.Equal(["user"], await keycloak.ApiRolesOfAsync(plain));
        Assert.Equal(["system-admin", "user"], await keycloak.ApiRolesOfAsync(administrator));
        Assert.Equal(["organization-creator", "user"], await keycloak.ApiRolesOfAsync(creator));

        // The panel client keeps the audience, and the token carries no groups claim.
        var token = await keycloak.ExampleAccessTokenAsync(administrator);
        Assert.Null(token["groups"]);

        // The stand's own drift check, against the live realm made from this very file: nothing.
        Assert.Empty(await keycloak.DriftFindingsAsync(RealmsDirectory));
    }

    // tech-024: the realm's name is configuration - the same file imported with MUSTASK_REALM set becomes that realm.
    [LiveRealmFact]
    public async Task TheRealmNameComesFromTheEnvironment()
    {
        await using var keycloak = await LiveRealmContainer.StartAsync(File.ReadAllText(RealmFile), realm: "company");

        var (status, realm, _) = await keycloak.AdminAsync(HttpMethod.Get, string.Empty);

        Assert.Equal(200, status);
        Assert.Equal("company", (string)realm!["realm"]!);
        Assert.Equal("default-roles-company", (string)realm["defaultRole"]!["name"]!);
        Assert.Empty(await keycloak.DriftFindingsAsync(RealmsDirectory));
    }
}
