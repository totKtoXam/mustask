using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure.Tests.Process;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// mod-016 plan-backend § 3.4, § 3.7 against a real PostgreSQL: the member lists name people from the user projection in a fixed
/// number of statements whatever the size (NFR Volumes; past 500 ids the lookups split into batches and the lists still answer),
/// in the order of api-contract § 0.5, and the count of known active administrators (BR-M1-4, BR-M1-32).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class MemberReadModelTests(OrganizationsDatabaseFixture database)
{
    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    // US-1 · "Past 500 ids the list and the member searches still answer": 501 members, adders who are no longer members; all
    // listed with names; one statement for the memberships and ceil(ids / 500) for the people.
    [Fact]
    public async Task List_501Members_NamesEveryoneIncludingAddersWhoAreNoLongerMembers_InThreeStatements()
    {
        var structure = await database.CreateStructureAsync();
        var tag = Tag();
        var users = Enumerable.Range(0, 501).Select(i => ($"m-{tag}-{i:D3}", "Участник", $"{i:D3}", "", true)).ToList();
        users.AddRange(
            [($"former-{tag}-1", "Бывший", "Первый", "", true), ($"former-{tag}-2", "Бывший", "Второй", "", false)]);
        await database.SeedUsersAsync([.. users]);

        // 501 memberships by a few administrators who are no longer members (their ids are not among the members).
        await database.ExecuteAsync(
            "INSERT INTO organization_memberships (organization_id, user_id, role, added_by, added_at) "
            + "SELECT @org, 'm-' || @tag || '-' || lpad(i::text, 3, '0'), 'member', "
            + "CASE WHEN i % 2 = 0 THEN 'former-' || @tag || '-1' ELSE 'former-' || @tag || '-2' END, now() FROM generate_series(0, 500) i",
            ("org", structure.Organization.Id),
            ("tag", tag));

        var counter = new CommandCountingInterceptor();
        var members = await ReadAsync<IMemberReadModel, IReadOnlyList<MemberView>>(
            counter, readModel => readModel.ListAsync(structure.Organization.Id, CancellationToken.None));

        Assert.Equal(501, members.Count);
        Assert.All(members, member => Assert.Equal(UserRefStates.Active, member.User.State));
        Assert.All(members, member => Assert.NotNull(member.AddedBy.DisplayName));
        Assert.Contains(members, member => member.AddedBy.State == UserRefStates.Inactive);

        // The memberships (1) + 503 distinct ids = 2 lookups of at most 500.
        Assert.Equal(3, counter.Commands.Count);
    }

    // api-contract § 0.5: ordered by display name under the ru collation at primary strength, then by id by code point; the
    // users the projection does not know are last, by id, with no name and no e-mail.
    [Fact]
    public async Task List_OrdersByName_RuPrimaryStrength_ThenId_UnknownLastById()
    {
        var structure = await database.CreateStructureAsync();
        var tag = Tag();
        await database.SeedUsersAsync(
            ($"b-{tag}", "Борис", "Борисов", "boris@example.org", true),
            ($"e-{tag}", "Елена", "Ёлкина", "", true),
            ($"a2-{tag}", "анна", "Иванова", "", true),
            ($"a1-{tag}", "Анна", "Иванова", "", true),
            ($"off-{tag}", "Олег", "Отключённый", "", false));

        foreach (var id in new[] { $"b-{tag}", $"e-{tag}", $"a2-{tag}", $"a1-{tag}", $"off-{tag}", $"z-ghost-{tag}", $"a-ghost-{tag}" })
        {
            await database.AddMemberAsync(structure.Organization.Id, id);
        }

        var members = await ReadAsync<IMemberReadModel, IReadOnlyList<MemberView>>(
            null, readModel => readModel.ListAsync(structure.Organization.Id, CancellationToken.None));

        Assert.Equal(
            [$"a1-{tag}", $"a2-{tag}", $"b-{tag}", $"e-{tag}", $"off-{tag}", $"a-ghost-{tag}", $"z-ghost-{tag}"],
            members.Select(member => member.User.Id));
        Assert.Equal(
            [UserRefStates.Active, UserRefStates.Active, UserRefStates.Active, UserRefStates.Active, UserRefStates.Inactive, UserRefStates.Unknown, UserRefStates.Unknown],
            members.Select(member => member.User.State));

        var ghost = members[^1].User;
        Assert.Equal((null, null), (ghost.DisplayName, ghost.Email));
        Assert.Equal("boris@example.org", members.Single(member => member.User.Id == $"b-{tag}").User.Email);
        Assert.Equal("", members.Single(member => member.User.Id == $"e-{tag}").User.Email);
    }

    // api-contract § 1.2: «Роль изменена» names who changed the role, as «Добавлен» names who added.
    [Fact]
    public async Task Find_ShowsWhoChangedTheRoleAndWhen_AndNullBeforeAnyChange()
    {
        var structure = await database.CreateStructureAsync();
        var tag = Tag();
        await database.SeedUsersAsync(($"u-{tag}", "Вера", "Новикова", "", true), ($"adm-{tag}", "Анна", "Админова", "", true));
        await database.AddMemberAsync(structure.Organization.Id, $"u-{tag}", OrganizationRole.Member, addedBy: $"adm-{tag}");
        await database.AddMemberAsync(structure.Organization.Id, $"v-{tag}", OrganizationRole.Viewer, addedBy: $"adm-{tag}");
        await database.ExecuteAsync(
            "UPDATE organization_memberships SET role = 'viewer', role_changed_by = @by, role_changed_at = @at "
            + "WHERE organization_id = @org AND user_id = @user",
            ("by", $"adm-{tag}"),
            ("at", OrganizationsDatabaseFixture.Now.AddDays(1)),
            ("org", structure.Organization.Id),
            ("user", $"u-{tag}"));

        var changed = await ReadAsync<IMemberReadModel, MemberView?>(
            null, readModel => readModel.FindAsync(structure.Organization.Id, $"u-{tag}", CancellationToken.None));
        var unchanged = await ReadAsync<IMemberReadModel, MemberView?>(
            null, readModel => readModel.FindAsync(structure.Organization.Id, $"v-{tag}", CancellationToken.None));
        var gone = await ReadAsync<IMemberReadModel, MemberView?>(
            null, readModel => readModel.FindAsync(structure.Organization.Id, "nobody", CancellationToken.None));

        Assert.Equal("viewer", changed!.Role);
        Assert.Equal("Анна Админова", changed.RoleChangedBy!.DisplayName);
        Assert.Equal(OrganizationsDatabaseFixture.Now.AddDays(1), changed.RoleChangedAt);
        Assert.Equal("Анна Админова", changed.AddedBy.DisplayName);
        Assert.Null(unchanged!.RoleChangedBy);
        Assert.Null(unchanged.RoleChangedAt);
        Assert.Null(gone);
    }

    // US-3 · the team's members as people: only that team's, ordered, the inactive and the unknown kept.
    [Fact]
    public async Task ListTeamMembers_OnlyThatTeams_Ordered_InactiveAndUnknownKept()
    {
        var structure = await database.CreateStructureAsync();
        var tag = Tag();
        await database.SeedUsersAsync(
            ($"v-{tag}", "Вера", "Новикова", "vera@example.org", true),
            ($"o-{tag}", "Олег", "Иванов", "", false),
            ($"x-{tag}", "Иван", "Посторонний", "", true));

        foreach (var id in new[] { $"v-{tag}", $"o-{tag}", $"x-{tag}", $"ghost-{tag}" })
        {
            await database.AddMemberAsync(structure.Organization.Id, id);
        }

        foreach (var id in new[] { $"v-{tag}", $"o-{tag}", $"ghost-{tag}" })
        {
            await database.AddTeamMemberAsync(structure.Organization.Id, structure.SecondTeam.Id, id);
        }

        await database.AddTeamMemberAsync(structure.Organization.Id, structure.Project.Team.Id, $"x-{tag}");

        var members = await ReadAsync<IMemberReadModel, IReadOnlyList<UserRefView>>(
            null, readModel => readModel.ListTeamMembersAsync(structure.SecondTeam.Id, CancellationToken.None));

        Assert.Equal(
            [
                new UserRefView($"v-{tag}", "Вера Новикова", "vera@example.org", UserRefStates.Active),
                new UserRefView($"o-{tag}", "Олег Иванов", "", UserRefStates.Inactive),
                new UserRefView($"ghost-{tag}", null, null, UserRefStates.Unknown),
            ],
            members);
    }

    // A team with no members is an empty list, in one statement.
    [Fact]
    public async Task ListTeamMembers_ATeamWithNoMembers_IsEmpty_OneStatement()
    {
        var structure = await database.CreateStructureAsync();
        var counter = new CommandCountingInterceptor();

        var members = await ReadAsync<IMemberReadModel, IReadOnlyList<UserRefView>>(
            counter, readModel => readModel.ListTeamMembersAsync(structure.SecondTeam.Id, CancellationToken.None));

        Assert.Empty(members);
        Assert.Single(counter.Commands);
    }

    [Fact]
    public async Task ListMemberIds_AndIsMember_AreThisOrganizationsOnly()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        var tag = Tag();
        await database.AddMemberAsync(structure.Organization.Id, $"in-{tag}");
        await database.AddMemberAsync(other.Organization.Id, $"out-{tag}");

        var ids = await ReadAsync<IMemberReadModel, IReadOnlyList<string>>(
            null, readModel => readModel.ListMemberIdsAsync(structure.Organization.Id, CancellationToken.None));
        var isIn = await ReadAsync<IMemberReadModel, bool>(
            null, readModel => readModel.IsMemberAsync(structure.Organization.Id, $"in-{tag}", CancellationToken.None));
        var isOut = await ReadAsync<IMemberReadModel, bool>(
            null, readModel => readModel.IsMemberAsync(structure.Organization.Id, $"out-{tag}", CancellationToken.None));

        Assert.Equal([$"in-{tag}"], ids);
        Assert.True(isIn);
        Assert.False(isOut);
    }

    // BR-M1-4, BR-M1-32: only an administrator the projection holds as active counts; an inactive, an unknown one and a
    // plain member do not; the excluded user is not counted either.
    [Fact]
    public async Task CountOtherKnownActiveAdministrators_CountsOnlyKnownActiveAdministrators_NotTheExcludedOne()
    {
        var structure = await database.CreateStructureAsync();
        var tag = Tag();
        await database.SeedUsersAsync(
            ($"a1-{tag}", "Анна", "Первая", "", true),
            ($"a2-{tag}", "Борис", "Второй", "", true),
            ($"off-{tag}", "Олег", "Отключённый", "", false),
            ($"m-{tag}", "Вера", "Участница", "", true));
        await database.AddMemberAsync(structure.Organization.Id, $"a1-{tag}", OrganizationRole.Admin);
        await database.AddMemberAsync(structure.Organization.Id, $"a2-{tag}", OrganizationRole.Admin);
        await database.AddMemberAsync(structure.Organization.Id, $"off-{tag}", OrganizationRole.Admin);
        await database.AddMemberAsync(structure.Organization.Id, $"ghost-{tag}", OrganizationRole.Admin);
        await database.AddMemberAsync(structure.Organization.Id, $"m-{tag}");

        var exceptFirst = await ReadAsync<IMemberReadModel, int>(
            null, readModel => readModel.CountOtherKnownActiveAdministratorsAsync(structure.Organization.Id, $"a1-{tag}", CancellationToken.None));
        var exceptGhost = await ReadAsync<IMemberReadModel, int>(
            null, readModel => readModel.CountOtherKnownActiveAdministratorsAsync(structure.Organization.Id, $"ghost-{tag}", CancellationToken.None));
        var exceptMember = await ReadAsync<IMemberReadModel, int>(
            null, readModel => readModel.CountOtherKnownActiveAdministratorsAsync(structure.Organization.Id, $"m-{tag}", CancellationToken.None));

        Assert.Equal(1, exceptFirst);
        Assert.Equal(2, exceptGhost);
        Assert.Equal(2, exceptMember);
    }

    [Fact]
    public async Task CountOtherKnownActiveAdministrators_NoOtherAdministrator_IsZero_WithoutAskingTheProjection()
    {
        var structure = await database.CreateStructureAsync();
        var tag = Tag();
        await database.AddMemberAsync(structure.Organization.Id, $"sole-{tag}", OrganizationRole.Admin);
        var counter = new CommandCountingInterceptor();

        var count = await ReadAsync<IMemberReadModel, int>(
            counter,
            readModel => readModel.CountOtherKnownActiveAdministratorsAsync(structure.Organization.Id, $"sole-{tag}", CancellationToken.None));

        Assert.Equal(0, count);
        Assert.Single(counter.Commands);
    }

    private async Task<TResult> ReadAsync<TReadModel, TResult>(IInterceptor? interceptor, Func<TReadModel, Task<TResult>> read)
        where TReadModel : notnull
    {
        await using var provider = interceptor is null ? database.BuildServices() : database.BuildServices(interceptor);
        await using var scope = provider.CreateAsyncScope();

        return await read(scope.ServiceProvider.GetRequiredService<TReadModel>());
    }
}
