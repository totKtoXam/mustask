using System.Net;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// A team's deletion over what it owns (feat-028 US-5, BR-OL-9, BR-OL-10, SC-09; mod-017 US-7, BR-OT-12, BR-OT-13; api-contract § 2.1): the items it
/// owns go to «Без команды» with the operation's time, its places, its links and a default setting that named it go, its memberships end,
/// its name is free; there is no last-team guard (BR-OL-10); and the writes that name the team — a save, a creation, the default naming, a reorder,
/// a child created beneath one of its items — either commit first and are taken with the rest, or come after and are refused,
/// in both orders, with no server error, no item owned by a deleted team, no setting naming one and no place naming a team that
/// does not own its item (plan-backend § 7.3). Every test builds its own world (<see cref="OwningTeamTestData"/>).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DeleteOwnedItemsEndpointTests(MustaskApiFactory factory)
{
    private const string TeamNotFound = "organizations.team.not_found";

    private string TeamPath(OwningTeamWorld world, Guid teamId) => Team(world.OrganizationId, teamId);

    private Task<Answer> DeleteAsync(OwningTeamWorld world, Guid teamId, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Delete, TeamPath(world, teamId), token: OrganizationsTokens.SignedIn(), gate: gate);

    private async Task<Item> StoryAsync(OwningTeamWorld world, string title, Guid? team, string typeKey = "US")
    {
        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new
            {
                requestId = Guid.NewGuid().ToString(),
                typeId = world.Items.Types[typeKey].Id.ToString(),
                title,
                team = new { teamId = team?.ToString() },
            },
            OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
    }

    private Task NameDefaultAsync(OwningTeamWorld world, Guid? teamId) =>
        WorkItemsTestData.ExecuteAsync(factory, $"UPDATE projects SET default_team_id = {teamId} WHERE id = {world.ProjectId}");

    private async Task<Guid?> DefaultSettingAsync(OwningTeamWorld world)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Projects
            .AsNoTracking()
            .Where(project => project.Id == world.ProjectId)
            .Select(project => project.DefaultTeamId)
            .SingleAsync();
    }

    /// <summary>Nothing owned by a deleted team, no setting naming one, no place naming a team that does not own its item (BR-OT-13).</summary>
    private async Task AssertInvariantsAsync(OwningTeamWorld world)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        var ownedByDeleted = await context.Database
            .SqlQuery<int>(
                $"""
                 SELECT count(*)::int AS "Value"
                 FROM work_items AS item JOIN teams AS team ON team.id = item.owning_team_id
                 WHERE item.project_id = {world.ProjectId} AND team.status = 'Deleted'
                 """)
            .SingleAsync();
        var namedDeleted = await context.Database
            .SqlQuery<int>(
                $"""
                 SELECT count(*)::int AS "Value"
                 FROM projects AS project JOIN teams AS team ON team.id = project.default_team_id
                 WHERE project.id = {world.ProjectId} AND team.status = 'Deleted'
                 """)
            .SingleAsync();

        var linkedDeleted = await context.Database
            .SqlQuery<int>(
                $"""
                 SELECT count(*)::int AS "Value"
                 FROM project_teams AS link JOIN teams AS team ON team.id = link.team_id
                 WHERE link.organization_id = {world.OrganizationId} AND team.status = 'Deleted'
                 """)
            .SingleAsync();

        Assert.Equal((0, 0, 0, 0), (ownedByDeleted, namedDeleted, linkedDeleted, await PlacesNamingAnotherTeamAsync(factory, world.ProjectId)));
    }

    // US-7 · "confirming sends the items to none": every item the team owns, in any state, has no owning team and shows the
    // operation's time; nothing else of it changes, its project place stays; the team's places go; a setting that named it is
    // cleared; its members' memberships end; its name is free; its address answers «Команда не найдена.»; other teams' items
    // and the areas are untouched (BR-OT-12).
    [Fact]
    public async Task Delete_ReassignsEverythingTheTeamOwns_ClearsSetting_EndsMemberships_FreesName_AddressAnswers404()
    {
        var world = await CreateWorldAsync(factory);
        var boards = BoardsViewOf(world);
        var open = await StoryAsync(world, "Открытая", world.Mars);
        var closed = await StoryAsync(world, "Закрытая", world.Mars);
        var removed = await StoryAsync(world, "Удалённая", world.Mars);
        var venus = await StoryAsync(world, "Венера", world.Venus);
        var type = world.Items.Types["US"];
        await WorkItemsTestData.SaveAsync(factory, world.Items, closed.Identifier, new { stateId = type.StateId(StateCategory.Completed).ToString() });
        await LinksTestData.DeleteAsync(factory, world.Items, removed);
        await BoardsTestData.ReorderToAsync(factory, boards, world.Mars, closed, open);
        await ProjectBoardsTestData.ReorderToAsync(factory, boards, open, closed, venus);
        await NameDefaultAsync(world, world.Mars);
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);
        await MembershipTestData.AddTeamMemberAsync(factory, world.OrganizationId, world.Mars, member.Id);
        await MembershipTestData.AddTeamMemberAsync(factory, world.OrganizationId, world.Venus, member.Id);
        var before = await StoredAsync(factory, world.ProjectId, open.Identifier);
        var venusBefore = await StoredAsync(factory, world.ProjectId, venus.Identifier);
        var areasBefore = await OrganizationsTestData.ReadTreeAsync(factory, world.ProjectId);
        var started = DateTimeOffset.UtcNow;

        var answer = await DeleteAsync(world, world.Mars);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        foreach (var item in new[] { open, closed, removed })
        {
            var stored = await StoredAsync(factory, world.ProjectId, item.Identifier);
            Assert.Null(stored.TeamId);
            Assert.True(stored.ChangedAt >= started.AddSeconds(-1), "«Изменён» does not show the operation's time.");
            Assert.Equal(0, await TeamPlacesOfAsync(factory, item.Id));
        }

        var after = await StoredAsync(factory, world.ProjectId, open.Identifier);
        Assert.Equal((before.AreaId, before.AssigneeId, before.StateId), (after.AreaId, after.AssigneeId, after.StateId));
        Assert.Equal(1, await ProjectPlacesOfAsync(factory, open.Id));
        Assert.Equal(venusBefore, await StoredAsync(factory, world.ProjectId, venus.Identifier));
        Assert.Null(await DefaultSettingAsync(world));
        Assert.Empty(await MembershipTestData.TeamMemberIdsAsync(factory, world.Mars));
        Assert.Equal([member.Id], await MembershipTestData.TeamMemberIdsAsync(factory, world.Venus));
        Assert.Equal(areasBefore.PreOrder(), (await OrganizationsTestData.ReadTreeAsync(factory, world.ProjectId)).PreOrder());
        Assert.Equal(TeamStatus.Deleted, (await StoredTeamAsync(world.Mars)).Status);

        var recreated = await CreateTeamAsync(factory, world.OrganizationId, new { name = "MARS" });
        var gone = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, TeamPath(world, world.Mars), token: OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.Created, recreated.Status);
        Assert.Equal((HttpStatusCode.NotFound, TeamNotFound), (gone.Status, gone.ErrorCode));
        await AssertInvariantsAsync(world);
    }

    private Task<Domain.Organizations.Team> StoredTeamAsync(Guid teamId) => TeamTestData.StoredTeamAsync(factory, teamId);

    // feat-028 BR-OL-10 · "any team may be deleted": no team is "the one the project was created with", because a project is created
    // with none (BR-OL-2). Retires Delete_TheTeamCreatedWithTheProject_IsAllowedWhenAnotherRemains (mod-017 BR-OT-11; doc-012 P-1).
    // Deleting a team while another stays linked is pinned by Delete_NamedTeam_ClearsTheSetting_AnotherTeamKeepsIt, and deleting a
    // project's last team by Delete_TheProjectsLastTeam_IsAllowed_ProjectWorksOn.

    // US-3 · "losing the named team": deleting the named team clears the setting; deleting another keeps it.
    [Fact]
    public async Task Delete_NamedTeam_ClearsTheSetting_AnotherTeamKeepsIt()
    {
        var world = await CreateWorldAsync(factory);
        await NameDefaultAsync(world, world.Venus);

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(world, world.Mars)).Status);
        Assert.Equal(world.Venus, await DefaultSettingAsync(world));

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(world, world.Venus)).Status);
        Assert.Null(await DefaultSettingAsync(world));
    }

    // US-5 · "a project's last team can be deleted" (BR-OL-10; retires Delete_LastTeam_Returns409_ChangesNothing): the only team of a
    // project goes, nothing is refused, its item is left without a team and the project works on.
    [Fact]
    public async Task Delete_TheProjectsLastTeam_IsAllowed_ProjectWorksOn()
    {
        var world = await CreateWorldAsync(factory);
        var single = await OrganizationsTestData.CreateProjectAsync(factory, world.OrganizationId, "Одна", "ONE");
        var items = world.Items with { ProjectId = single.Project.Id, RootAreaId = single.Root.Id, Key = "ONE" };
        var story = await WorkItemsTestData.CreateAsync(factory, items, "US", "История");
        Assert.Equal(HttpStatusCode.Created, story.Status);
        var identifier = story.Body.GetProperty("identifier").GetString()!;
        Assert.Equal(single.Team.Id, (await StoredAsync(factory, single.Project.Id, identifier)).TeamId);

        var answer = await DeleteAsync(world, single.Team.Id);
        var next = await WorkItemsTestData.CreateAsync(factory, items, "US", "Следующая");

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(TeamStatus.Deleted, (await StoredTeamAsync(single.Team.Id)).Status);
        Assert.Null((await StoredAsync(factory, single.Project.Id, identifier)).TeamId);
        Assert.Empty(await LinksOfProjectAsync(factory, single.Project.Id));
        Assert.Equal(HttpStatusCode.Created, next.Status);
        Assert.Null((await StoredAsync(factory, single.Project.Id, next.Body.GetProperty("identifier").GetString()!)).TeamId);
    }

    // US-5 · "two deletions emptying a project" (C-14; retires Delete_TwoLastTeamsAtOnce_OneDoneOneRefused): the guard is gone, so
    // two deletions of the two teams of one project are both done — they hold disjoint rows and neither waits for the other.
    [Fact]
    public async Task Delete_TwoTeamsThatEmptyAProject_AtOnce_BothDone()
    {
        var world = await CreateWorldAsync(factory);
        var pair = await OrganizationsTestData.CreateProjectAsync(factory, world.OrganizationId, "Пара", "PAR");
        var second = await AddTeamAsync(factory, pair.Project.Id, "SECOND");

        var answers = await Task.WhenAll(DeleteAsync(world, pair.Team.Id), DeleteAsync(world, second));

        Assert.All(answers, answer => Assert.Equal(HttpStatusCode.NoContent, answer.Status));
        Assert.Empty(await LinksOfProjectAsync(factory, pair.Project.Id));
        Assert.Equal(TeamStatus.Deleted, (await StoredTeamAsync(pair.Team.Id)).Status);
        Assert.Equal(TeamStatus.Deleted, (await StoredTeamAsync(second)).Status);
    }

    // US-5 · "two deletions of one team" (C-14): the team row serializes them; the second answers team.not_found.
    [Fact]
    public async Task Delete_TheSameTeamTwiceAtOnce_OneDone_OneTeamNotFound()
    {
        var world = await CreateWorldAsync(factory);

        var (first, second) = await RaceAsync(at => DeleteAsync(world, world.Mars, at), at => DeleteAsync(world, world.Mars, at));

        Assert.Equal(HttpStatusCode.NoContent, first.Status);
        Assert.Equal((HttpStatusCode.NotFound, TeamNotFound), (second.Status, second.ErrorCode));
    }

    // US-5 · "the deletion reaches a deactivated project" (BR-OL-9; retires Delete_InDeactivatedProject_Returns404_ThenRestoreKeepsEverything):
    // the team is linked to a project that is deactivated; the items it owns there and the setting that names it are reassigned and
    // cleared all the same, and the restore finds them clean.
    [Fact]
    public async Task Delete_DeactivatedProjectsItemsAndSetting_AreReassigned_RestoreFindsThemClean()
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world, "История", world.Mars);
        await NameDefaultAsync(world, world.Mars);
        await OrganizationsTestData.SetProjectStatusAsync(factory, world.OrganizationId, world.ProjectId, ProjectStatus.Deactivated);

        var deleted = await DeleteAsync(world, world.Mars);
        await OrganizationsTestData.SetProjectStatusAsync(factory, world.OrganizationId, world.ProjectId, ProjectStatus.Active);

        Assert.Equal(HttpStatusCode.NoContent, deleted.Status);
        Assert.Equal(TeamStatus.Deleted, (await StoredTeamAsync(world.Mars)).Status);
        Assert.Null((await StoredAsync(factory, world.ProjectId, story.Identifier)).TeamId);
        Assert.Null(await DefaultSettingAsync(world));
        await AssertInvariantsAsync(world);
    }

    // US-5 · "a team linked to several projects" (BR-OL-9): the items it owns in EVERY linked project are reassigned, each project's
    // setting that named it is cleared, and every link is removed.
    [Fact]
    public async Task Delete_ReassignsInEveryLinkedProject_ClearsEverySetting_RemovesEveryLink()
    {
        var world = await CreateWorldAsync(factory);
        await OrganizationsTestData.LinkAsync(factory, world.OtherProjectId, world.Mars);
        var alpha = await StoryAsync(world, "Альфа", world.Mars);
        var beta = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items with { ProjectId = world.OtherProjectId, Key = "BET" }),
            new
            {
                requestId = Guid.NewGuid().ToString(),
                typeId = world.Items.Types["US"].Id.ToString(),
                title = "Бета",
                team = new { teamId = world.Mars.ToString() },
            },
            OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.Created, beta.Status);
        await NameDefaultAsync(world, world.Mars);
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE projects SET default_team_id = {world.Mars} WHERE id = {world.OtherProjectId}");

        var answer = await DeleteAsync(world, world.Mars);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Null((await StoredAsync(factory, world.ProjectId, alpha.Identifier)).TeamId);
        Assert.Null((await StoredAsync(factory, world.OtherProjectId, beta.Body.GetProperty("identifier").GetString()!)).TeamId);
        Assert.Null(await DefaultSettingAsync(world));
        Assert.Null(await SettingAsync(factory, world.OtherProjectId));
        Assert.Empty(await LinksOfTeamAsync(factory, world.Mars));
    }

    // --- Races (BR-OT-13) --------------------------------------------------------------------------------------------

    // The binders of a team, in both orders. "Binder first": the binder is held at its commit with the team locked, the deletion
    // waits, then takes what the binder wrote. "Deletion first": the deletion is held at its commit with every team of the
    // project locked, the binder waits, then reads the team deleted and is refused team_not_found.
    [Theory]
    [InlineData("save", true)]
    [InlineData("save", false)]
    [InlineData("create", true)]
    [InlineData("create", false)]
    [InlineData("name", true)]
    [InlineData("name", false)]
    [InlineData("reorder", true)]
    [InlineData("reorder", false)]
    public async Task Delete_VersusSaveCreateNameReorder_BothOrders_NoItemOwnedByADeletedTeam_No5xx(string binder, bool binderFirst)
    {
        var world = await CreateWorldAsync(factory);
        var boards = BoardsViewOf(world);
        var held = await StoryAsync(world, "Занятая", world.Venus);
        var onMars = await StoryAsync(world, "На Марсе", world.Mars);
        var other = await StoryAsync(world, "На Марсе тоже", world.Mars);
        await BoardsTestData.ReorderToAsync(factory, boards, world.Mars, onMars, other);
        using var gate = factory.CommitGates.Open();

        Task<Answer> Bind(CommitGate? at) => binder switch
        {
            "save" => WorkItemsTestData.SaveAsync(factory, world.Items, held.Identifier, new { team = new { teamId = world.Mars.ToString() } }, at),
            "create" => OrganizationsApi.SendAsync(
                factory,
                HttpMethod.Post,
                WorkItemsTestData.List(world.Items),
                new
                {
                    requestId = Guid.NewGuid().ToString(),
                    typeId = world.Items.Types["US"].Id.ToString(),
                    title = "Новая",
                    team = new { teamId = world.Mars.ToString() },
                },
                OrganizationsTokens.SignedIn(),
                at),
            "name" => OrganizationsApi.SendAsync(
                factory,
                HttpMethod.Put,
                $"{OrganizationsApi.Project(world.OrganizationId, world.ProjectId)}/default-team",
                new { teamId = world.Mars.ToString() },
                OrganizationsTokens.SignedIn(),
                at),
            _ => BoardsTestData.ReorderAsync(factory, boards, world.Mars, BoardsTestData.Top(other), gate: at),
        };

        var success = binder switch
        {
            "create" => HttpStatusCode.Created,
            "name" or "reorder" => HttpStatusCode.NoContent,
            _ => HttpStatusCode.OK,
        };

        Task<Answer> first;
        Task<Answer> second;

        if (binderFirst)
        {
            first = Bind(gate);
            await Task.WhenAny(gate.Reached, first);
            Assert.False(first.IsCompleted, "The binder finished before reaching its commit gate.");
            second = DeleteAsync(world, world.Mars);
        }
        else
        {
            first = DeleteAsync(world, world.Mars, gate);
            await Task.WhenAny(gate.Reached, first);
            Assert.False(first.IsCompleted, "The deletion finished before reaching its commit gate.");
            second = Bind(null);
        }

        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "The second request did not wait for the first one's lock of the team.");

        gate.Release();
        var (a, b) = (await first, await second);
        var (binderAnswer, deletion) = binderFirst ? (a, b) : (b, a);

        Assert.Equal(HttpStatusCode.NoContent, deletion.Status);

        if (binderFirst)
        {
            Assert.Equal(success, binderAnswer.Status);
        }
        else
        {
            Assert.Equal((HttpStatusCode.NotFound, TeamNotFound), (binderAnswer.Status, binderAnswer.ErrorCode));
        }

        // Taken with the rest, or refused: either way nothing is left behind, and what the team owned had gone to none.
        Assert.NotEqual(world.Mars, (await StoredAsync(factory, world.ProjectId, held.Identifier)).TeamId);
        Assert.Null((await StoredAsync(factory, world.ProjectId, onMars.Identifier)).TeamId);
        Assert.Null(await DefaultSettingAsync(world));
        await AssertInvariantsAsync(world);
    }

    // US-7 · "a child beneath a story of the team" (BR-OT-13 (2)): a child created beneath an item of the team while the team is
    // deleted is never refused for the team (it names none) — if it commits first the deletion reassigns it with the rest, if it
    // comes after it takes its parent's team as stored then (none) — in both orders, with no deadlock. With a FOR UPDATE lock on
    // the team this pair deadlocks (plan-backend D-3): the child's owner key takes a key share of the team, the deletion waits for
    // the parent the creation holds.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_VersusChildCreation_BothOrders(bool creationFirst)
    {
        var world = await CreateWorldAsync(factory);
        var parent = await StoryAsync(world, "Родитель", world.Mars, "FEAT");
        using var gate = factory.CommitGates.Open();

        Task<Answer> Create(CommitGate? at) => OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new
            {
                requestId = Guid.NewGuid().ToString(),
                typeId = world.Items.Types["US"].Id.ToString(),
                title = "Ребёнок",
                parentId = parent.Id.ToString(),
                team = new { teamId = world.Mars.ToString() },
            },
            OrganizationsTokens.SignedIn(),
            at);

        Task<Answer> first;
        Task<Answer> second;

        if (creationFirst)
        {
            first = Create(gate);
            await Task.WhenAny(gate.Reached, first);
            Assert.False(first.IsCompleted, "The creation finished before reaching its commit gate.");
            second = DeleteAsync(world, world.Mars);
        }
        else
        {
            first = DeleteAsync(world, world.Mars, gate);
            await Task.WhenAny(gate.Reached, first);
            Assert.False(first.IsCompleted, "The deletion finished before reaching its commit gate.");
            second = Create(null);
        }

        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "The second request did not wait for the first one.");

        gate.Release();
        var (a, b) = (await first, await second);
        var (creation, deletion) = creationFirst ? (a, b) : (b, a);

        Assert.Equal(HttpStatusCode.NoContent, deletion.Status);
        Assert.Equal(HttpStatusCode.Created, creation.Status);
        Assert.Null((await StoredAsync(factory, world.ProjectId, creation.Body.GetProperty("identifier").GetString()!)).TeamId);
        Assert.Null((await StoredAsync(factory, world.ProjectId, parent.Identifier)).TeamId);
        await AssertInvariantsAsync(world);
    }

    // BR-OT-13 · the default lock of a creation: the project default team is deleted while a creation with no team field is held at
    // its commit — the deletion waits, then reassigns the new item with the rest; or it commits first and the creation, which
    // waited on the team's row, reads the default as it stands after the deletion (cleared) and takes none.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_VersusCreationOnTheDefault_BothOrders_NeverOwnedByADeletedTeam(bool creationFirst)
    {
        var world = await CreateWorldAsync(factory);
        await NameDefaultAsync(world, world.Mars);
        using var gate = factory.CommitGates.Open();

        Task<Answer> Create(CommitGate? at) => OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new { requestId = Guid.NewGuid().ToString(), typeId = world.Items.Types["US"].Id.ToString(), title = "По умолчанию" },
            OrganizationsTokens.SignedIn(),
            at);

        Task<Answer> first;
        Task<Answer> second;

        if (creationFirst)
        {
            first = Create(gate);
            await Task.WhenAny(gate.Reached, first);
            Assert.False(first.IsCompleted, "The creation finished before reaching its commit gate.");
            second = DeleteAsync(world, world.Mars);
        }
        else
        {
            first = DeleteAsync(world, world.Mars, gate);
            await Task.WhenAny(gate.Reached, first);
            Assert.False(first.IsCompleted, "The deletion finished before reaching its commit gate.");
            second = Create(null);
        }

        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "The second request did not wait for the first one.");

        gate.Release();
        var (a, b) = (await first, await second);
        var (creation, deletion) = creationFirst ? (a, b) : (b, a);

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.Created), (deletion.Status, creation.Status));
        Assert.Null((await StoredAsync(factory, world.ProjectId, creation.Body.GetProperty("identifier").GetString()!)).TeamId);
        await AssertInvariantsAsync(world);
    }

    // --- Races in a project that names no default (BR-OT-4, BR-OT-13 (2); QA D-1, code review CR-1, security S-1) ---------
    //
    // A project of two linked teams and no default named has no default until one of the teams goes. A creation that may take the
    // default (no team sent, or a child) must still queue against the deletion — which holds the team's row and its links — and
    // read the default, and its parent, as they stand after it; with no lock the child kept the default it read before the wait.

    /// <summary>
    /// A project of its own team and DOOMED (<see cref="OwningTeamWorld.Mars"/> here) and no default named, in a new organization:
    /// <see cref="OwningTeamWorld.OwnTeam"/> is the team that remains when DOOMED is deleted.
    /// </summary>
    private async Task<OwningTeamWorld> TwoTeamWorldAsync()
    {
        var world = await CreateWorldAsync(factory);
        var two = await OrganizationsTestData.CreateProjectAsync(factory, world.OrganizationId, "Две команды", "TWO");
        var doomed = await AddTeamAsync(factory, two.Project.Id, "DOOMED");

        return world with
        {
            Items = world.Items with { ProjectId = two.Project.Id, RootAreaId = two.Root.Id, Key = "TWO" },
            OwnTeam = two.Team.Id,
            Mars = doomed,
        };
    }

    private Task<Answer> CreateBeneathAsync(OwningTeamWorld world, Item parent, string typeKey, string title, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new
            {
                requestId = Guid.NewGuid().ToString(),
                typeId = world.Items.Types[typeKey].Id.ToString(),
                title,
                parentId = parent.Id.ToString(),
            },
            OrganizationsTokens.SignedIn(),
            gate);

    private Task<Answer> CreateWithNoTeamFieldAsync(OwningTeamWorld world, string title, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new { requestId = Guid.NewGuid().ToString(), typeId = world.Items.Types["US"].Id.ToString(), title },
            OrganizationsTokens.SignedIn(),
            gate);

    /// <summary>The first request held at its commit, the second queued behind its locks, then both released and answered.</summary>
    private async Task<(Answer First, Answer Second)> RaceAsync(Func<CommitGate?, Task<Answer>> first, Func<CommitGate?, Task<Answer>> second)
    {
        using var gate = factory.CommitGates.Open();

        var firstTask = first(gate);
        await Task.WhenAny(gate.Reached, firstTask);
        Assert.False(firstTask.IsCompleted, "The first request finished before reaching its commit gate.");

        var secondTask = second(null);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, secondTask);
        Assert.False(secondTask.IsCompleted, "The second request did not wait for the first one.");

        gate.Release();

        return (await firstTask, await secondTask);
    }

    // BR-OT-13 (2) · a child beneath an item of the team, in both orders, when no default resolves: the creation first — it holds
    // the project's teams, the deletion waits, then takes the child with the rest (no team); the deletion first — the child waits,
    // reads its parent released and the default as it stands after the deletion, the one team left (BR-OT-4).
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_VersusChildCreation_TwoTeamsNoNamedDefault_BothOrders(bool creationFirst)
    {
        var world = await TwoTeamWorldAsync();
        var parent = await StoryAsync(world, "Родитель", world.Mars, "FEAT");
        Assert.Null(await DefaultTeamAsync(factory, world.ProjectId));

        Answer creation;
        Answer deletion;

        if (creationFirst)
        {
            (creation, deletion) = await RaceAsync(at => CreateBeneathAsync(world, parent, "US", "Ребёнок", at), at => DeleteAsync(world, world.Mars, at));
        }
        else
        {
            (deletion, creation) = await RaceAsync(at => DeleteAsync(world, world.Mars, at), at => CreateBeneathAsync(world, parent, "US", "Ребёнок", at));
        }

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.Created), (deletion.Status, creation.Status));
        Assert.Null((await StoredAsync(factory, world.ProjectId, parent.Identifier)).TeamId);
        Guid? expected = creationFirst ? null : world.OwnTeam;
        Assert.Equal(expected, (await StoredAsync(factory, world.ProjectId, creation.Body.GetProperty("identifier").GetString()!)).TeamId);
        await AssertInvariantsAsync(world);
    }

    // BR-OT-3 · the same without a parent: an item created with no team field while no default resolves takes the default as it
    // stands when it is saved — none if it commits before the deletion, the team that remains if it comes after.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_VersusCreationWithNoTeamField_TwoTeamsNoNamedDefault_BothOrders(bool creationFirst)
    {
        var world = await TwoTeamWorldAsync();

        Answer creation;
        Answer deletion;

        if (creationFirst)
        {
            (creation, deletion) = await RaceAsync(at => CreateWithNoTeamFieldAsync(world, "Без поля", at), at => DeleteAsync(world, world.Mars, at));
        }
        else
        {
            (deletion, creation) = await RaceAsync(at => DeleteAsync(world, world.Mars, at), at => CreateWithNoTeamFieldAsync(world, "Без поля", at));
        }

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.Created), (deletion.Status, creation.Status));
        Guid? expected = creationFirst ? null : world.OwnTeam;
        Assert.Equal(expected, (await StoredAsync(factory, world.ProjectId, creation.Body.GetProperty("identifier").GetString()!)).TeamId);
        await AssertInvariantsAsync(world);
    }

    // S-1 outcome 1 · a grandchild beneath a child that was created while the team was being deleted. The child is held at its
    // commit, the deletion queues behind the teams the child holds and is itself held at its commit once the child has
    // committed (it has released the child with the rest); the grandchild then waits for the deletion, reads the child released
    // and takes the default as it stands: nothing is left owned by the deleted team. (The interleaving the security pass wrote
    // down — the grandchild holding the child when the release starts — cannot be forced with commit gates; this test pins what
    // rules it out: a creation that may default always queues against the deletion.)
    [Fact]
    public async Task Delete_VersusChildThenGrandchildCreation_TwoTeamsNoNamedDefault_NothingStaysOnTheDeletedTeam()
    {
        var world = await TwoTeamWorldAsync();
        var parent = await StoryAsync(world, "Родитель", world.Mars, "FEAT");
        using var childGate = factory.CommitGates.Open();
        using var deletionGate = factory.CommitGates.Open();

        var childTask = CreateBeneathAsync(world, parent, "US", "Ребёнок", childGate);
        await Task.WhenAny(childGate.Reached, childTask);
        Assert.False(childTask.IsCompleted, "The child finished before reaching its commit gate.");

        var deletionTask = DeleteAsync(world, world.Mars, deletionGate);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, deletionTask);
        Assert.False(deletionTask.IsCompleted, "The deletion did not wait for the teams the child holds.");

        childGate.Release();
        var child = await childTask;
        Assert.Equal(HttpStatusCode.Created, child.Status);
        await Task.WhenAny(deletionGate.Reached, deletionTask);
        Assert.False(deletionTask.IsCompleted, "The deletion finished before reaching its commit gate.");

        var childItem = new Item(child.Body.GetProperty("id").GetGuid(), child.Body.GetProperty("identifier").GetString()!);
        Assert.Equal(world.Mars, (await StoredAsync(factory, world.ProjectId, childItem.Identifier)).TeamId);
        var grandchildTask = CreateBeneathAsync(world, childItem, "TASK", "Внук");
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, grandchildTask);
        Assert.False(grandchildTask.IsCompleted, "The grandchild did not wait for the deletion.");

        deletionGate.Release();
        var (deletion, grandchild) = (await deletionTask, await grandchildTask);

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.Created), (deletion.Status, grandchild.Status));
        Assert.Null((await StoredAsync(factory, world.ProjectId, parent.Identifier)).TeamId);
        Assert.Null((await StoredAsync(factory, world.ProjectId, childItem.Identifier)).TeamId);
        Assert.Equal(
            world.OwnTeam,
            (await StoredAsync(factory, world.ProjectId, grandchild.Body.GetProperty("identifier").GetString()!)).TeamId);
        await AssertInvariantsAsync(world);
    }
}
