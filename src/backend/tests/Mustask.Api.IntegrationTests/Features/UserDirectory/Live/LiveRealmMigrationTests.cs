using System.Text.Json.Nodes;
using Mustask.AppHost.Provisioning;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory.Live;

/// <summary>
/// An opt-in live test of the live-realm command against a REAL Keycloak (plan-backend § 4.7, R-7): the logic, order, idempotence and
/// interruption are proved against a fake of the admin surface (<c>KeycloakLiveRealmMigrationTests</c>); this proves the fake mirrors
/// Keycloak — that the endpoints the command calls exist, take the bodies it sends and do to the realm what the fake does. The
/// command changes a realm, so this test has a switch of its own, <c>MUSTASK_KEYCLOAK_LIVE_REALM_TESTS=1</c>, and <b>no</b> verify
/// bucket sets it: it is run on purpose, by QA or the orchestrator, before the task-stand trial that BR-M1-28 requires (the human's
/// stand is only ever migrated after a stand of a task has been).
/// </summary>
[Trait("Category", "KeycloakLiveRealm")]
public sealed class LiveRealmMigrationTests
{
    private static string OldRealmFile =>
        Path.Combine(LiveRealmImportTests.RepositoryRoot(), "src", "backend", "tests", "Mustask.Contracts.Tests", "TestData", "mustask-realm.pre-mod-016.json");

    private static string NewRealmsDirectory => Path.Combine(LiveRealmImportTests.RepositoryRoot(), "deploy", "keycloak", "realms");

    [LiveRealmMigrationFact]
    public async Task TheCommand_MigratesARealmCreatedFromTheOldImport_InPlace_AndIsSafeToRepeat()
    {
        await using var keycloak = await LiveRealmContainer.StartAsync(File.ReadAllText(OldRealmFile));

        var viaGroup = await keycloak.CreateUserAsync("live.admin.group", "/Администраторы");
        var direct = await keycloak.CreateUserAsync("live.admin.direct");
        await GrantDirectlyAsync(keycloak, direct, "admin");
        var staff = await keycloak.CreateUserAsync("live.staff", "/Сотрудники");
        var participant = await keycloak.CreateUserAsync("live.participant", "/Сотрудники/Участники");
        var creator = await keycloak.CreateUserAsync("live.creator", "/Создатели организаций");

        // Before: the old realm differs from the new import.
        Assert.NotEmpty(await keycloak.DriftFindingsAsync(NewRealmsDirectory));
        Assert.Contains("organizations.organization.create", await keycloak.ApiRolesOfAsync(viaGroup));

        var output = new StringWriter();
        var exit = await KeycloakLiveRealmMigration.RunAsync(
            keycloak.Http, new KeycloakLiveRealmMigrationOptions { AdminPassword = keycloak.AdminPassword }, output, CancellationToken.None);

        Assert.True(exit == KeycloakLiveRealmMigration.ExitDone, output.ToString());
        Assert.DoesNotContain(keycloak.AdminPassword, output.ToString(), StringComparison.Ordinal);

        // After: both admin holders create organizations and write dictionaries as before; the users of the old groups are in no group
        // and still read; the creator still creates; the roles and groups are gone; the drift check finds nothing — with no reset.
        string[] all = ["dictionaries.languages.read", "dictionaries.languages.write", "organizations.organization.create"];
        Assert.Equal(all, await keycloak.ApiRolesOfAsync(viaGroup));
        Assert.Equal(all, await keycloak.ApiRolesOfAsync(direct));
        Assert.Equal(["dictionaries.languages.read"], await keycloak.ApiRolesOfAsync(staff));
        Assert.Equal(["dictionaries.languages.read"], await keycloak.ApiRolesOfAsync(participant));
        Assert.Equal(["dictionaries.languages.read", "organizations.organization.create"], await keycloak.ApiRolesOfAsync(creator));

        foreach (var role in new[] { "admin", "member", "viewer" })
        {
            Assert.Equal(404, (await keycloak.AdminAsync(HttpMethod.Get, "/roles/" + role)).Status);
        }

        Assert.Equal(404, (await keycloak.AdminAsync(HttpMethod.Get, "/group-by-path/" + Uri.EscapeDataString("Сотрудники"))).Status);

        foreach (var user in new[] { staff, participant })
        {
            var (_, groups, _) = await keycloak.AdminAsync(HttpMethod.Get, $"/users/{user}/groups");
            Assert.Empty((JsonArray)groups!);
        }

        Assert.Empty(await keycloak.DriftFindingsAsync(NewRealmsDirectory));

        // Safe to repeat.
        var again = new StringWriter();
        var secondExit = await KeycloakLiveRealmMigration.RunAsync(
            keycloak.Http, new KeycloakLiveRealmMigrationOptions { AdminPassword = keycloak.AdminPassword }, again, CancellationToken.None);

        Assert.Equal(KeycloakLiveRealmMigration.ExitDone, secondExit);
        Assert.Contains("Nothing to change.", again.ToString(), StringComparison.Ordinal);
    }

    private static async Task GrantDirectlyAsync(LiveRealmContainer keycloak, string userId, string role)
    {
        var (_, representation, _) = await keycloak.AdminAsync(HttpMethod.Get, "/roles/" + role);
        var (status, _, _) = await keycloak.AdminAsync(
            HttpMethod.Post,
            $"/users/{userId}/role-mappings/realm",
            new JsonArray(new JsonObject { ["id"] = (string)representation!["id"]!, ["name"] = role }));

        Assert.True(status == 204, $"mapping {role} onto the user answered {status}");
    }
}

/// <summary>An opt-in test that runs the live-realm command against a throwaway Keycloak: its own switch, no verify bucket sets it.</summary>
public sealed class LiveRealmMigrationFactAttribute : FactAttribute
{
    public const string Switch = "MUSTASK_KEYCLOAK_LIVE_REALM_TESTS";

    public LiveRealmMigrationFactAttribute()
    {
        if (!IsOn)
        {
            Skip = $"Live-realm command against a real Keycloak: set {Switch}=1 (run on purpose by QA or the orchestrator; scripts/verify.sh does not).";
        }
    }

    public static bool IsOn => Environment.GetEnvironmentVariable(Switch) == "1";
}
