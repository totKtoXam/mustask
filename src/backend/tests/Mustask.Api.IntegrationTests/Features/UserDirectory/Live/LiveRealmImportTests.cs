using System.Text.Json.Nodes;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory.Live;

/// <summary>
/// mod-016 plan-backend § 4.3 (the spike that stays): Keycloak imports the repository's <b>real</b> realm file and the realm is what
/// the contract tests and the stand's drift check read from it — the default role is <c>default-roles-mustask</c> with exactly the
/// declared composites, a user in no group carries <c>dictionaries.languages.read</c> and no other code in a token, the three
/// groups map their roles, and the stand's own drift check reports nothing. A file test cannot promise any of this: Keycloak
/// rejects or reshapes an import silently (the first form tried stopped at "Unable to find composite realm role: uma_authorization"
/// until the two built-in roles were declared). Opt-in (<see cref="LiveProviderFactAttribute"/>): <c>scripts/verify.sh</c>'s
/// <c>provider</c> bucket runs it whenever a realm file changes (D-16 OP-10).
/// </summary>
[Trait("Category", "KeycloakProvider")]
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

    [LiveProviderFact]
    public async Task TheRepositorysRealm_IsImportedByKeycloak_AsTheContractTestsAndTheDriftCheckReadIt()
    {
        await using var keycloak = await LiveRealmContainer.StartAsync(File.ReadAllText(RealmFile));

        // The realm roles Keycloak holds are the import's: the business roles, the default role and the two built-ins.
        var (_, roles, _) = await keycloak.AdminAsync(HttpMethod.Get, "/roles?briefRepresentation=false");
        var names = ((JsonArray)roles!).Select(role => (string)role!["name"]!).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(
            ["default-roles-mustask", "dictionaries-admin", "instance-admin", "offline_access", "organization-creator", "uma_authorization"],
            names);

        // The default role: the one Keycloak uses, with exactly the declared composites.
        var (_, realm, _) = await keycloak.AdminAsync(HttpMethod.Get, string.Empty);
        Assert.Equal("default-roles-mustask", (string)realm!["defaultRole"]!["name"]!);
        var (_, composites, _) = await keycloak.AdminAsync(HttpMethod.Get, "/roles/default-roles-mustask/composites");
        var (_, clients, _) = await keycloak.AdminAsync(HttpMethod.Get, "/clients");
        var clientIds = ((JsonArray)clients!).ToDictionary(client => (string)client!["id"]!, client => (string)client!["clientId"]!);
        var held = ((JsonArray)composites!)
            .Select(role => ((bool)role!["clientRole"]! ? clientIds[(string)role["containerId"]!] : "realm") + "/" + (string)role["name"]!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            ["account/manage-account", "account/view-profile", "mustask-api/dictionaries.languages.read", "realm/offline_access", "realm/uma_authorization"],
            held);

        // The groups and their mappings.
        var (_, groups, _) = await keycloak.AdminAsync(HttpMethod.Get, "/groups?briefRepresentation=false");
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var group in (JsonArray)groups!)
        {
            var (_, mappings, _) = await keycloak.AdminAsync(HttpMethod.Get, $"/groups/{group!["id"]}/role-mappings/realm");
            mapping[(string)group["name"]!] = string.Join(",", ((JsonArray)mappings!).Select(role => (string)role!["name"]!).Order(StringComparer.Ordinal));
        }

        Assert.Equal("instance-admin", mapping["Администраторы"]);
        Assert.Equal("dictionaries-admin", mapping["Администраторы справочников"]);
        Assert.Equal("organization-creator", mapping["Создатели организаций"]);
        Assert.Equal(3, mapping.Count);

        // US-8 · tokens: a user in no group reads the dictionaries and nothing more; the groups add their codes.
        var noGroup = await keycloak.CreateUserAsync("live.nogroup");
        var administrator = await keycloak.CreateUserAsync("live.admin", "/Администраторы");
        var creator = await keycloak.CreateUserAsync("live.creator", "/Создатели организаций");
        var dictionaries = await keycloak.CreateUserAsync("live.dictionaries", "/Администраторы справочников");

        Assert.Equal(["dictionaries.languages.read"], await keycloak.ApiRolesOfAsync(noGroup));
        Assert.Equal(
            ["dictionaries.languages.read", "dictionaries.languages.write", "organizations.organization.create"],
            await keycloak.ApiRolesOfAsync(administrator));
        Assert.Equal(["dictionaries.languages.read", "organizations.organization.create"], await keycloak.ApiRolesOfAsync(creator));
        Assert.Equal(["dictionaries.languages.read", "dictionaries.languages.write"], await keycloak.ApiRolesOfAsync(dictionaries));

        // BR-M1-37 · the stand's own drift check, against the live realm made from this very file: nothing.
        Assert.Empty(await keycloak.DriftFindingsAsync(RealmsDirectory));
    }
}
