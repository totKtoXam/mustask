using Mustask.AppHost.Provisioning;
using Mustask.Domain.Identity;

namespace Mustask.Contracts.Tests;

/// <summary>
/// mod-016 US-8 (BR-M1-28, BR-M1-37, D-6, D-16): the live-realm migration against a write-capable fake of Keycloak's admin surface
/// (<see cref="FakeKeycloakAdmin"/>) built from the PRE-mod-016 import kept as test data — the realm of the human's stand today —
/// with users who hold <c>admin</c> through a group and directly, users of the two groups that go, a user of the creators' group, and
/// a group the import does not declare that maps <c>admin</c> and <c>dictionaries-admin</c>. The logic, the order, the idempotence,
/// the interruption and the secrets are proved here; that the fake mirrors Keycloak is proved by the opt-in live test and the
/// task-stand trial (plan-backend R-7). No Docker, no Keycloak.
/// </summary>
public sealed class KeycloakLiveRealmMigrationTests
{
    private static readonly Dictionary<string, string> StandValues = new(StringComparer.Ordinal)
    {
        ["MUSTASK_PANEL_URL"] = "http://localhost:3000",
        ["MUSTASK_API_URL"] = "http://localhost:5000",
        ["MUSTASK_PANEL_CLIENT_SECRET"] = KeycloakStandDriftCheckTests.PanelSecret,
    };

    private static KeycloakRealmImport OldImport() =>
        KeycloakRealmImport.Parse(File.ReadAllText(KeycloakStandDriftCheckTests.PreMod016RealmPath), StandValues);

    /// <summary>The realm of the human's stand today, with the people BR-M1-28 and US-8 name.</summary>
    private static FakeKeycloakAdmin OldRealm()
    {
        var realm = FakeKeycloakAdmin.FromImport(OldImport(), KeycloakStandDriftCheckTests.PanelSecret);

        realm.AddUser("anna.admin", groups: ["/Администраторы"]);
        realm.AddUser("boris.direct", roles: ["admin"]);
        realm.AddUser("vera.staff", groups: ["/Сотрудники"]);
        realm.AddUser("misha.member", groups: ["/Сотрудники/Участники"]);
        realm.AddUser("olga.creator", groups: ["/Создатели организаций"]);
        realm.AddUser("denis.dictionaries", groups: ["/Администраторы справочников"]);
        realm.AddUser("zoya.nogroup");

        // A group the import does not declare, with members, mapping `admin` and one other role that is neither `member` nor `viewer`,
        // and a subgroup of it that maps `admin` too — "at any depth".
        var sales = realm.EnsureGroup("/Отдел продаж");
        sales.RealmRoles.UnionWith(["admin", "dictionaries-admin"]);
        realm.AddUser("pavel.sales", groups: ["/Отдел продаж"]);
        var management = realm.EnsureGroup("/Отдел продаж/Руководство");
        management.RealmRoles.Add("admin");
        realm.AddUser("lena.management", groups: ["/Отдел продаж/Руководство"]);

        // The same person direct and through a group: mapped once, and counted once.
        realm.Users["user-anna.admin"].RealmRoles.Add("admin");

        // The invariant of BR-M1-28 — every grant before any removal — checked at every removal of the run.
        realm.BeforeDelete = AssertEveryAdminHolderAlreadyHoldsInstanceAdmin;

        return realm;
    }

    private static void AssertEveryAdminHolderAlreadyHoldsInstanceAdmin(FakeKeycloakAdmin realm)
    {
        foreach (var holder in realm.HoldersOf("admin"))
        {
            Assert.True(
                realm.HoldersOf("instance-admin").Any(candidate => candidate.Id == holder.Id),
                $"A removal was attempted while `{holder.Username}` holds `admin` and not yet `instance-admin`.");
        }
    }

    private static async Task<(int Exit, string Output)> MigrateAsync(FakeKeycloakAdmin realm, string? password = null)
    {
        using var http = new HttpClient(realm, disposeHandler: false) { BaseAddress = new Uri("http://keycloak.test") };
        var output = new StringWriter();

        var exit = await KeycloakLiveRealmMigration.RunAsync(
            http, new KeycloakLiveRealmMigrationOptions { AdminPassword = password ?? FakeKeycloakAdmin.AdminPassword }, output, CancellationToken.None);

        return (exit, output.ToString());
    }

    private static async Task<IReadOnlyList<string>> DriftFindingsAsync(FakeKeycloakAdmin realm)
    {
        using var http = new HttpClient(realm, disposeHandler: false) { BaseAddress = new Uri("http://keycloak.test") };

        return await KeycloakStandDriftCheck.RunAsync(
            http,
            new KeycloakDriftCheckOptions
            {
                Import = KeycloakStandDriftCheckTests.Import(),
                AdminUsername = "admin",
                AdminPassword = FakeKeycloakAdmin.AdminPassword,
                SecretClientId = "mustask-panel",
                SecretClientSecret = KeycloakStandDriftCheckTests.PanelSecret,
            },
            CancellationToken.None);
    }

    private static HashSet<string> NamesHoldingInstanceAdmin(FakeKeycloakAdmin realm) =>
        realm.HoldersOf("instance-admin").Select(user => user.Username).ToHashSet(StringComparer.Ordinal);

    // ---- the catalog ---------------------------------------------------------------------------------------------------

    // The AppHost cannot reference the domain, so the codes it writes are held to the catalog here.
    [Fact]
    public void TheCommandsInstanceCodes_AreThePermissionCatalogs()
    {
        Assert.Equal(
            PermissionCatalog.InstanceCodes.Order(StringComparer.Ordinal),
            KeycloakLiveRealmMigration.InstanceCodes.Order(StringComparer.Ordinal));
        Assert.Equal(PermissionCatalog.DictionariesLanguagesRead, KeycloakLiveRealmMigration.DictionariesReadCode);
        Assert.Equal("instance-admin", KeycloakLiveRealmMigration.InstanceAdminRole);
    }

    // ---- US-8 "migrated in place" ---------------------------------------------------------------------------------------

    // US-8 · "The live realm of the human's stand is migrated in place": both admin holders — the one through /Администраторы and the
    // one given the role directly — hold instance-admin, and so do the members of every other group that mapped `admin`; the users of
    // the two old groups are in no group and still read the dictionaries; the old roles and groups are gone; and the stand's drift
    // check against the NEW import finds nothing — which is what makes the stand come up ready with no reset.
    [Fact]
    public async Task Migrate_InPlace_AdminHoldersHoldInstanceAdmin_OldRolesAndGroupsAreGone_TheDriftCheckFindsNothing()
    {
        var realm = OldRealm();
        Assert.NotEmpty(await DriftFindingsAsync(realm));

        var (exit, output) = await MigrateAsync(realm);

        Assert.Equal(KeycloakLiveRealmMigration.ExitDone, exit);
        Assert.True(
            new[] { "anna.admin", "boris.direct", "pavel.sales", "lena.management" }.ToHashSet().SetEquals(NamesHoldingInstanceAdmin(realm)),
            "instance-admin holders: " + string.Join(", ", NamesHoldingInstanceAdmin(realm)));
        Assert.DoesNotContain(realm.RealmRoles.Keys, name => name is "admin" or "member" or "viewer");
        Assert.Null(realm.Group("/Сотрудники"));
        Assert.Null(realm.Group("/Сотрудники/Участники"));

        // The users of the removed groups still exist, in no group, and read the dictionaries through the default roles.
        foreach (var username in new[] { "vera.staff", "misha.member" })
        {
            var user = realm.Users.Values.Single(candidate => candidate.Username == username);

            Assert.DoesNotContain(realm.AllGroups(), group => group.Members.Contains(user.Id));
        }

        Assert.Contains(
            new FakeComposite(FakeKeycloakAdmin.ResourceClient, "dictionaries.languages.read"),
            realm.RealmRoles[realm.DefaultRole].Composites);
        Assert.Equal(
            KeycloakLiveRealmMigration.InstanceCodes.Order(StringComparer.Ordinal),
            realm.RealmRoles["instance-admin"].Composites.Select(composite => composite.Name).Order(StringComparer.Ordinal));
        Assert.All(realm.RealmRoles["instance-admin"].Composites, composite => Assert.Equal(FakeKeycloakAdmin.ResourceClient, composite.Container));
        Assert.Empty(await DriftFindingsAsync(realm));
        Assert.Contains("Grants complete.", output, StringComparison.Ordinal);
    }

    // US-8 · "every grant comes before any removal": the report orders them, and the fake checked at every DELETE that every holder of
    // `admin` already held instance-admin (OldRealm installs the check).
    [Fact]
    public async Task Migrate_ReportsEveryGrantBeforeTheFirstRemoval()
    {
        var (_, output) = await MigrateAsync(OldRealm());

        var grantsComplete = output.IndexOf("Grants complete.", StringComparison.Ordinal);

        Assert.True(grantsComplete > 0);
        Assert.True(output.IndexOf("now maps `instance-admin`", StringComparison.Ordinal) is > 0 and var firstGrant && firstGrant < grantsComplete);
        Assert.True(output.IndexOf("deleted", StringComparison.Ordinal) > grantsComplete, "A removal is reported before the grants are complete.");
    }

    // US-8 · "The live-realm script changes nothing else": the realm before and after differ only by what BR-M1-28 lists —
    // entity by entity: the roles admin, member, viewer (gone), instance-admin (new), the default roles (the read code), the two
    // groups that go, /Администраторы and the undeclared groups (instance-admin in place of admin), the user given admin directly
    // (instance-admin in place of it). Everything else — clients and their secrets, every user's id, credential, attributes and
    // other mappings, every other group, the built-in roles — is untouched.
    [Fact]
    public async Task Migrate_ChangesNothingElse_TheDiffIsExactlyTheListOfBR_M1_28()
    {
        var realm = OldRealm();
        var before = realm.Snapshot();

        await MigrateAsync(realm);
        var after = realm.Snapshot();

        string Key(string line) => string.Join(' ', line.Split(' ')[..2]);
        var changed = before.Except(after).Concat(after.Except(before)).Select(Key).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            new[]
            {
                "role admin", "role member", "role viewer", "role instance-admin", "role default-roles-mustask",
                "group /Администраторы", "group /Отдел", "group /Сотрудники", "group /Сотрудники/Участники",
                "user boris.direct", "user anna.admin",
            }.ToHashSet(StringComparer.Ordinal),
            changed.Select(key => key.StartsWith("group /Отдел", StringComparison.Ordinal) ? "group /Отдел" : key).ToHashSet(StringComparer.Ordinal));

        // What the changed entities look like afterwards.
        Assert.Equal(["instance-admin"], realm.Group("/Администраторы")!.RealmRoles);
        Assert.Equal(["dictionaries-admin", "instance-admin"], realm.Group("/Отдел продаж")!.RealmRoles.Order(StringComparer.Ordinal));
        Assert.Equal(["instance-admin"], realm.Group("/Отдел продаж/Руководство")!.RealmRoles);
        Assert.Equal(["pavel.sales"], realm.Group("/Отдел продаж")!.Members.Select(id => realm.Users[id].Username));
        Assert.Equal(["instance-admin"], realm.Users["user-boris.direct"].RealmRoles);
        Assert.Equal(["instance-admin"], realm.Users["user-anna.admin"].RealmRoles);

        // The users: ids, credentials and attributes are as they were, and no user was created or deleted.
        Assert.Equal(before.Where(line => line.StartsWith("user ", StringComparison.Ordinal)).Count(), after.Count(line => line.StartsWith("user ", StringComparison.Ordinal)));
        Assert.Equal(
            before.Where(line => line.StartsWith("user zoya.nogroup", StringComparison.Ordinal)),
            after.Where(line => line.StartsWith("user zoya.nogroup", StringComparison.Ordinal)));
        Assert.All(
            realm.Users.Values,
            user =>
            {
                Assert.Equal("credential-of-" + user.Username + "-not-real", user.Credential);
                Assert.Equal("отдел-" + user.Username, user.Attributes["department"]);
                Assert.Equal("user-" + user.Username, user.Id);
            });

        // Clients, their roles and secrets; the other groups' members; the built-in roles.
        Assert.Equal(
            before.Where(line => line.StartsWith("client ", StringComparison.Ordinal)),
            after.Where(line => line.StartsWith("client ", StringComparison.Ordinal)));
        Assert.Contains(after, line => line == "group /Администраторы справочников realm=[dictionaries-admin] members=[denis.dictionaries]");
        Assert.Contains(after, line => line == "group /Создатели организаций realm=[organization-creator] members=[olga.creator]");
        Assert.Contains(after, line => line == "role offline_access composites=[]");
        Assert.Contains(after, line => line == "role uma_authorization composites=[]");
    }

    // US-8 · "The live-realm script is safe to repeat": the second run reports nothing to change and sends no mutating request.
    [Fact]
    public async Task Migrate_Twice_TheSecondRunChangesNothing_AndSendsNoMutatingRequest()
    {
        var realm = OldRealm();
        await MigrateAsync(realm);
        var snapshot = realm.Snapshot();
        var mutationsAfterFirst = realm.Mutations;

        var (exit, output) = await MigrateAsync(realm);

        Assert.Equal(KeycloakLiveRealmMigration.ExitDone, exit);
        Assert.Contains("Nothing to change.", output, StringComparison.Ordinal);
        Assert.Equal(mutationsAfterFirst, realm.Mutations);
        Assert.Equal(snapshot, realm.Snapshot());
        Assert.Empty(await DriftFindingsAsync(realm));
    }

    // The same on a realm that was never old: a stand created from the new import has nothing to migrate.
    [Fact]
    public async Task Migrate_ARealmCreatedFromTheNewImport_HasNothingToChange()
    {
        var realm = FakeKeycloakAdmin.FromImport(KeycloakStandDriftCheckTests.Import(), KeycloakStandDriftCheckTests.PanelSecret);
        realm.AddUser("anna.admin", groups: ["/Администраторы"]);
        var snapshot = realm.Snapshot();

        var (exit, output) = await MigrateAsync(realm);

        Assert.Equal(KeycloakLiveRealmMigration.ExitDone, exit);
        Assert.Contains("Nothing to change.", output, StringComparison.Ordinal);
        Assert.Equal(0, realm.Mutations);
        Assert.Equal(snapshot, realm.Snapshot());
    }

    // ---- US-8 "An interrupted run is completed by running the script again" ----------------------------------------------

    // US-8 · a run stopped after any one of its mutating requests — before it took effect, or after it did with the answer lost —
    // is completed by running the command again, and the realm then ends exactly as after an uninterrupted run; at every removal of
    // both runs every holder of `admin` already held instance-admin (the check OldRealm installs).
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migrate_InterruptedAtEveryMutatingRequest_ThenRunAgain_EndsExactlyAsAnUninterruptedRun(bool theFailedRequestTookEffect)
    {
        var reference = OldRealm();
        await MigrateAsync(reference);
        var expected = reference.Snapshot();
        var mutations = reference.Mutations;

        Assert.True(mutations >= 10, $"Only {mutations} mutating requests in a full run.");

        for (var k = 1; k <= mutations; k++)
        {
            var realm = OldRealm();
            realm.FailMutation = k;
            realm.FailedMutationTakesEffect = theFailedRequestTookEffect;

            var (interruptedExit, interruptedOutput) = await MigrateAsync(realm);

            Assert.True(interruptedExit == KeycloakLiveRealmMigration.ExitFailed, $"Mutation {k} did not fail the run: exit {interruptedExit}.");
            Assert.Contains("run the command again", interruptedOutput, StringComparison.Ordinal);

            realm.FailMutation = null;
            var (exit, _) = await MigrateAsync(realm);

            Assert.True(exit == KeycloakLiveRealmMigration.ExitDone, $"The rerun after a failure of mutation {k} exited {exit}.");
            Assert.True(expected.SequenceEqual(realm.Snapshot()), $"After a failure of mutation {k} and a rerun the realm differs from an uninterrupted run.");
        }
    }

    // ---- US-8 "An unexpected structure stops the script before any removal" ----------------------------------------------

    public static TheoryData<string> UnexpectedStructures() =>
        ["a subgroup of /Сотрудники", "a role other than viewer on /Сотрудники", "a role other than member on /Участники", "a subgroup of /Участники", "a role with admin as a composite"];

    // US-8 · a live realm whose /Сотрудники holds a subgroup other than /Участники — or whose structure the command does not know in
    // another way (OP-2) — is not touched: no write at all (exit 2), and the report names what it found.
    [Theory]
    [MemberData(nameof(UnexpectedStructures))]
    public async Task Migrate_AnUnexpectedStructure_StopsBeforeAnyChange_AndNamesIt(string structure)
    {
        var realm = OldRealm();
        string expectedMention;

        switch (structure)
        {
            case "a subgroup of /Сотрудники":
                realm.EnsureGroup("/Сотрудники/Лишняя");
                expectedMention = "/Сотрудники/Лишняя";
                break;
            case "a role other than viewer on /Сотрудники":
                realm.Group("/Сотрудники")!.RealmRoles.Add("dictionaries-admin");
                expectedMention = "`dictionaries-admin`";
                break;
            case "a role other than member on /Участники":
                realm.Group("/Сотрудники/Участники")!.RealmRoles.Add("organization-creator");
                expectedMention = "`organization-creator`";
                break;
            case "a subgroup of /Участники":
                realm.EnsureGroup("/Сотрудники/Участники/Дети");
                expectedMention = "/Сотрудники/Участники/Дети";
                break;
            default:
                realm.AddRole("auditor").Composites.Add(new FakeComposite("realm", "admin"));
                expectedMention = "`auditor`";
                break;
        }

        var before = realm.Snapshot();

        var (exit, output) = await MigrateAsync(realm);

        Assert.Equal(KeycloakLiveRealmMigration.ExitStopped, exit);
        Assert.Contains(expectedMention, output, StringComparison.Ordinal);
        Assert.Contains("Nothing was changed.", output, StringComparison.Ordinal);
        Assert.Equal(0, realm.Mutations);
        Assert.Equal(before, realm.Snapshot());
        Assert.DoesNotContain(realm.Requests, request => !request.StartsWith("GET ", StringComparison.Ordinal) && !request.Contains("openid-connect/token", StringComparison.Ordinal));
    }

    // The two structures the command does not stop on: a role that has `member` or `viewer` as a composite (the deletion ends that
    // composite, as BR-M1-28 says: "on users, groups and roles alike").
    [Fact]
    public async Task Migrate_ARoleWithMemberOrViewerAsAComposite_IsNotAStop_TheCompositeEnds()
    {
        var realm = OldRealm();
        realm.AddRole("reader").Composites.Add(new FakeComposite("realm", "viewer"));

        var (exit, _) = await MigrateAsync(realm);

        Assert.Equal(KeycloakLiveRealmMigration.ExitDone, exit);
        Assert.Empty(realm.RealmRoles["reader"].Composites);
    }

    // ---- paging ------------------------------------------------------------------------------------------------------------

    // The users who hold `admin` directly are read page by page: more than a page of them are all migrated.
    [Fact]
    public async Task Migrate_MoreThanAPageOfDirectAdminHolders_AllHoldInstanceAdminAfterwards()
    {
        var realm = OldRealm();

        for (var i = 0; i < 250; i++)
        {
            realm.AddUser($"direct-{i:D3}", roles: ["admin"]);
        }

        var (exit, _) = await MigrateAsync(realm);

        Assert.Equal(KeycloakLiveRealmMigration.ExitDone, exit);
        Assert.Equal(250, realm.Users.Values.Count(user => user.Username.StartsWith("direct-", StringComparison.Ordinal) && user.RealmRoles.SetEquals(["instance-admin"])));
        Assert.Contains(realm.Requests, request => request.Contains("/roles/admin/users?first=200", StringComparison.Ordinal));
    }

    // ---- stops that are not about the structure --------------------------------------------------------------------------

    [Fact]
    public async Task Migrate_WithAWrongAdministratorPassword_StopsBeforeAnyChange()
    {
        var realm = OldRealm();
        var before = realm.Snapshot();

        var (exit, output) = await MigrateAsync(realm, password: "not-the-password-nor-a-real-one");

        Assert.Equal(KeycloakLiveRealmMigration.ExitStopped, exit);
        Assert.Contains("refuses the administrator password", output, StringComparison.Ordinal);
        Assert.DoesNotContain("not-the-password-nor-a-real-one", output, StringComparison.Ordinal);
        Assert.Equal(0, realm.Mutations);
        Assert.Equal(before, realm.Snapshot());
    }

    [Fact]
    public async Task Migrate_WhenTheRealmDoesNotExist_StopsBeforeAnyChange()
    {
        var realm = OldRealm();
        realm.RealmExists = false;

        var (exit, output) = await MigrateAsync(realm);

        Assert.Equal(KeycloakLiveRealmMigration.ExitStopped, exit);
        Assert.Contains("does not exist", output, StringComparison.Ordinal);
        Assert.Equal(0, realm.Mutations);
    }

    // ---- US-8 "The realm scripts keep the administrator's secrets out of sight" -----------------------------------------

    // Neither the password nor the access token is in the report — in a full run, in an interrupted run and in a stopped one — and
    // the report names users by username only (no id, no credential, no attribute).
    [Theory]
    [InlineData("a full run")]
    [InlineData("an interrupted run")]
    [InlineData("a stopped run")]
    public async Task TheReport_NeverHoldsThePasswordTheAccessTokenOrAUsersCredential(string run)
    {
        var realm = OldRealm();

        switch (run)
        {
            case "an interrupted run":
                realm.FailMutation = 3;
                break;
            case "a stopped run":
                realm.EnsureGroup("/Сотрудники/Лишняя");
                break;
        }

        var (_, output) = await MigrateAsync(realm);

        Assert.DoesNotContain(FakeKeycloakAdmin.AdminPassword, output, StringComparison.Ordinal);
        Assert.DoesNotContain(FakeKeycloakAdmin.AccessToken, output, StringComparison.Ordinal);
        Assert.DoesNotContain("credential-of-", output, StringComparison.Ordinal);
        Assert.DoesNotContain("user-boris.direct", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", output, StringComparison.Ordinal);
        Assert.DoesNotContain("отдел-", output, StringComparison.Ordinal);

        if (run == "a full run")
        {
            Assert.Contains("`boris.direct`", output, StringComparison.Ordinal);
        }
    }

    // The password is only ever in the token request's form body: no admin request carries it in a path or a query.
    [Fact]
    public async Task ThePassword_IsNeverInAPathOrAQuery()
    {
        var realm = OldRealm();

        await MigrateAsync(realm);

        Assert.All(realm.Requests, request => Assert.DoesNotContain(FakeKeycloakAdmin.AdminPassword, request, StringComparison.Ordinal));
        Assert.All(realm.Requests, request => Assert.DoesNotContain(FakeKeycloakAdmin.AccessToken, request, StringComparison.Ordinal));
        Assert.Single(realm.Requests, request => request.Contains("openid-connect/token", StringComparison.Ordinal));
    }

    // ---- the command line --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task TheCommand_ReadsThePasswordFromTheEnvironment_AndMigrates()
    {
        var realm = OldRealm();
        var output = new StringWriter();
        var environment = new Dictionary<string, string?> { [KeycloakLiveRealmCommand.PasswordVariable] = FakeKeycloakAdmin.AdminPassword };

        var exit = await KeycloakLiveRealmCommand.RunAsync(
            [KeycloakLiveRealmCommand.Flag], name => environment.GetValueOrDefault(name), output, realm, CancellationToken.None);

        Assert.Equal(KeycloakLiveRealmMigration.ExitDone, exit);
        Assert.DoesNotContain(FakeKeycloakAdmin.AdminPassword, output.ToString(), StringComparison.Ordinal);
        Assert.Empty(await DriftFindingsAsync(realm));
    }

    [Fact]
    public async Task TheCommand_WithoutThePasswordVariable_StopsAndTouchesNothing()
    {
        var realm = OldRealm();
        var output = new StringWriter();

        var exit = await KeycloakLiveRealmCommand.RunAsync([KeycloakLiveRealmCommand.Flag], _ => null, output, realm, CancellationToken.None);

        Assert.Equal(KeycloakLiveRealmMigration.ExitStopped, exit);
        Assert.Contains(KeycloakLiveRealmCommand.PasswordVariable, output.ToString(), StringComparison.Ordinal);
        Assert.Empty(realm.Requests);
    }

    // A password typed as an argument would sit in the process list: it is refused, and never echoed back.
    [Fact]
    public async Task TheCommand_RefusesAnyOtherArgument_WithoutEchoingIt()
    {
        var realm = OldRealm();
        var output = new StringWriter();
        var environment = new Dictionary<string, string?> { [KeycloakLiveRealmCommand.PasswordVariable] = FakeKeycloakAdmin.AdminPassword };

        var exit = await KeycloakLiveRealmCommand.RunAsync(
            [KeycloakLiveRealmCommand.Flag, "a-password-typed-as-an-argument"], name => environment.GetValueOrDefault(name), output, realm, CancellationToken.None);

        Assert.Equal(KeycloakLiveRealmMigration.ExitStopped, exit);
        Assert.DoesNotContain("a-password-typed-as-an-argument", output.ToString(), StringComparison.Ordinal);
        Assert.Empty(realm.Requests);
    }

    [Fact]
    public void TheFlag_IsRecognizedAmongTheAppHostsArguments()
    {
        Assert.True(KeycloakLiveRealmCommand.IsRequested(["--migrate-live-realm"]));
        Assert.False(KeycloakLiveRealmCommand.IsRequested(["--stand-reset"]));
        Assert.False(KeycloakLiveRealmCommand.IsRequested([]));
    }
}
