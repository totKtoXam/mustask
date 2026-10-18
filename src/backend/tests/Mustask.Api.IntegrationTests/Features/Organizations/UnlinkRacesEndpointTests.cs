using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// The races of an unlink (feat-028 plan-backend § 7.3, C-1 to C-9; ADR-0011 echelon 3, ADR-0028 rule 8): each row in both orders with
/// no 5xx and the four invariants — no item owned by a team that is not linked to its project, no setting that names an unlinked or a
/// deleted team, no place that names a team other than the item's owner, no link that names a deleted team. The first request is held at
/// its commit gate with its locks; the second must block on one of them; then the first is released and both answers are read.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class UnlinkRacesEndpointTests(MustaskApiFactory factory)
{
    private const string TeamNotFound = "organizations.team.not_found";
    private const string TeamNotInProject = "organizations.team.not_in_project";

    private Task<Answer> UnlinkAsync(OwningTeamWorld world, Guid teamId, Guid? destination, CommitGate? gate) =>
        TeamTestData.UnlinkAsync(factory, world.OrganizationId, world.ProjectId, teamId, To(destination), gate: gate);

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

    private Task<Answer> NameDefaultAsync(OwningTeamWorld world, Guid teamId, CommitGate? gate) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Project(world.OrganizationId, world.ProjectId)}/default-team",
            new { teamId = teamId.ToString() },
            OrganizationsTokens.SignedIn(),
            gate);

    private Task<Answer> CreateBeneathAsync(OwningTeamWorld world, Item parent, string title, CommitGate? gate) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new
            {
                requestId = Guid.NewGuid().ToString(),
                typeId = world.Items.Types["US"].Id.ToString(),
                title,
                parentId = parent.Id.ToString(),
            },
            OrganizationsTokens.SignedIn(),
            gate);

    private async Task<int> CountAsync(FormattableString sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Database.SqlQuery<int>(sql).SingleAsync();
    }

    /// <summary>The four invariants of plan-backend § 7.3, over the whole organization: all four counts are zero.</summary>
    private async Task AssertInvariantsAsync(OwningTeamWorld world)
    {
        var ownedUnlinked = await CountAsync($"""
            SELECT count(*)::int AS "Value" FROM work_items AS item
            WHERE item.owning_team_id IS NOT NULL
              AND item.project_id IN (SELECT id FROM projects WHERE organization_id = {world.OrganizationId})
              AND NOT EXISTS (SELECT 1 FROM project_teams AS link WHERE link.project_id = item.project_id AND link.team_id = item.owning_team_id)
            """);
        var settingUnlinked = await CountAsync($"""
            SELECT count(*)::int AS "Value" FROM projects AS project
            WHERE project.organization_id = {world.OrganizationId} AND project.default_team_id IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM project_teams AS link WHERE link.project_id = project.id AND link.team_id = project.default_team_id)
            """);
        var linkedDeleted = await CountAsync($"""
            SELECT count(*)::int AS "Value" FROM project_teams AS link JOIN teams AS team ON team.id = link.team_id
            WHERE link.organization_id = {world.OrganizationId} AND team.status = 'Deleted'
            """);

        Assert.Equal((0, 0, 0, 0), (ownedUnlinked, settingUnlinked, linkedDeleted, await PlacesNamingAnotherTeamAsync(factory, world.ProjectId)));
    }

    // C-1, C-2, C-4, C-5 · an unlink of MARS against a save that sets MARS, a create that names MARS, a naming of MARS as the default and
    // a reorder of MARS's backlog. Binder first: the binder commits, the unlink re-reads and takes what the binder wrote with the rest
    // (the saved story, the created one, the naming cleared, the places dropped). Unlink first: the binder finds the link gone — 409
    // not_in_project for a save, a create and a naming — and stores nothing. A reorder of the team's backlog takes no link (feat-040
    // PD-8, api-contract § 8.1: the team's address names no project): the unlink first has reassigned the story it moves, so it is no
    // member of MARS's backlog now — 409 reorder_stale (was: 404 team.not_found, when the view sat under the project) — and writes nothing.
    [Theory]
    [InlineData("save", true)]
    [InlineData("save", false)]
    [InlineData("create", true)]
    [InlineData("create", false)]
    [InlineData("name", true)]
    [InlineData("name", false)]
    [InlineData("reorder", true)]
    [InlineData("reorder", false)]
    public async Task Unlink_VersusSaveCreateNameReorder_BothOrders_No5xx_InvariantsHold(string binder, bool binderFirst)
    {
        var world = await CreateWorldAsync(factory);
        var boards = BoardsViewOf(world);
        var held = await StoryAsync(world, "Занятая", world.Venus);
        var onMars = await StoryAsync(world, "На Марсе", world.Mars);
        var other = await StoryAsync(world, "На Марсе тоже", world.Mars);
        await BoardsTestData.ReorderToAsync(factory, boards, world.Mars, onMars, other);

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
            "name" => NameDefaultAsync(world, world.Mars, at),
            _ => BoardsTestData.ReorderAsync(factory, boards, world.Mars, BoardsTestData.Top(other), gate: at),
        };
        Task<Answer> Unlink(CommitGate? at) => UnlinkAsync(world, world.Mars, world.Venus, at);

        var success = binder switch
        {
            "create" => HttpStatusCode.Created,
            "name" or "reorder" => HttpStatusCode.NoContent,
            _ => HttpStatusCode.OK,
        };
        var (binderAnswer, unlink) = binderFirst
            ? await RaceAsync(factory, Bind, Unlink)
            : Flip(await RaceAsync(factory, Unlink, Bind));

        Assert.Equal(HttpStatusCode.NoContent, unlink.Status);

        if (binderFirst)
        {
            Assert.Equal(success, binderAnswer.Status);
        }
        else
        {
            var refusal = binder == "reorder" ? (HttpStatusCode.Conflict, "boards.backlog.reorder_stale") : (HttpStatusCode.Conflict, TeamNotInProject);
            Assert.Equal(refusal, (binderAnswer.Status, binderAnswer.ErrorCode));
        }

        // Taken with the rest, or refused: either way MARS owns nothing here, names nothing and has no place; its stories are VENUS's.
        Assert.Equal(0, await OwnedItemCountAsync(factory, world.Mars));
        foreach (var item in new[] { held, onMars, other })
        {
            Assert.Equal(world.Venus, (await StoredAsync(factory, world.ProjectId, item.Identifier)).TeamId);
            Assert.Equal(0, await TeamPlacesOfAsync(factory, item.Id));
        }

        Assert.Null(await DefaultSettingAsync(world));
        Assert.DoesNotContain(world.Mars, (await LinksOfProjectAsync(factory, world.ProjectId)).Select(link => link.TeamId));
        await AssertInvariantsAsync(world);
    }

    // C-3 · an unlink of MARS against a child created beneath a story MARS owns, with no team named. The child takes every link of the
    // project FOR SHARE first (D-4), so the two never hold a link of the project together. Child first: it commits owned by its parent's team
    // (MARS), and the unlink reassigns it with the rest. Unlink first: the child waits, then reads its parent's new team (VENUS) — it is
    // never refused for a team. Either way the parent and the child end VENUS's.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unlink_VersusChildCreatedBeneathAnItemOfTheTeam_BothOrders(bool childFirst)
    {
        var world = await CreateWorldAsync(factory);
        var parent = await StoryAsync(world, "Родитель", world.Mars, "FEAT");

        var (child, unlink) = childFirst
            ? await RaceAsync(factory, at => CreateBeneathAsync(world, parent, "Ребёнок", at), at => UnlinkAsync(world, world.Mars, world.Venus, at))
            : Flip(await RaceAsync(factory, at => UnlinkAsync(world, world.Mars, world.Venus, at), at => CreateBeneathAsync(world, parent, "Ребёнок", at)));

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.Created), (unlink.Status, child.Status));
        Assert.Equal(world.Venus, (await StoredAsync(factory, world.ProjectId, parent.Identifier)).TeamId);
        Assert.Equal(world.Venus, (await StoredAsync(factory, world.ProjectId, child.Body.GetProperty("identifier").GetString()!)).TeamId);
        Assert.Equal(0, await OwnedItemCountAsync(factory, world.Mars));
        await AssertInvariantsAsync(world);
    }

    // C-6 · two unlinks that name each other (MARS to VENUS, VENUS to MARS): both lock the pair in team-id order in one statement, so the
    // first applies and the second finds its destination unlinked — 409 not_in_project — in either order. No deadlock, no 5xx.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unlink_TwoThatNameEachOther_OneApplied_TheOtherRefused_NoDeadlock(bool marsFirst)
    {
        var world = await CreateWorldAsync(factory);
        var onMars = await StoryAsync(world, "На Марсе", world.Mars);
        var onVenus = await StoryAsync(world, "На Венере", world.Venus);
        var (first, second) = marsFirst ? (world.Mars, world.Venus) : (world.Venus, world.Mars);

        var (applied, refused) = await RaceAsync(
            factory, at => UnlinkAsync(world, first, second, at), at => UnlinkAsync(world, second, first, at));

        Assert.Equal(HttpStatusCode.NoContent, applied.Status);
        Assert.Equal((HttpStatusCode.Conflict, TeamNotInProject), (refused.Status, refused.ErrorCode));
        var links = (await LinksOfProjectAsync(factory, world.ProjectId)).Select(link => link.TeamId).ToList();
        Assert.DoesNotContain(first, links);
        Assert.Contains(second, links);
        Assert.Equal(second, (await StoredAsync(factory, world.ProjectId, marsFirst ? onMars.Identifier : onVenus.Identifier)).TeamId);
        await AssertInvariantsAsync(world);
    }

    // C-7 · an unlink of MARS against the deletion of MARS. Unlink first: the deletion works on what is left — MARS is no longer linked
    // to the project; its stories went to VENUS before the deletion. Deletion first: the unlink re-reads MARS deleted and answers 404
    // team.not_found; the stories went to nobody with the deletion.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unlink_VersusTheDeletionOfTheUnlinkedTeam_BothOrders(bool unlinkFirst)
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world, "На Марсе", world.Mars);
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE projects SET default_team_id = {world.Mars} WHERE id = {world.ProjectId}");

        Task<Answer> Unlink(CommitGate? at) => UnlinkAsync(world, world.Mars, world.Venus, at);
        Task<Answer> Deletion(CommitGate? at) => DeleteTeamAsync(factory, world.OrganizationId, world.Mars, gate: at);
        var (unlink, deletion) = unlinkFirst
            ? await RaceAsync(factory, Unlink, Deletion)
            : Flip(await RaceAsync(factory, Deletion, Unlink));

        Assert.Equal(HttpStatusCode.NoContent, deletion.Status);

        if (unlinkFirst)
        {
            Assert.Equal(HttpStatusCode.NoContent, unlink.Status);
            Assert.Equal(world.Venus, (await StoredAsync(factory, world.ProjectId, story.Identifier)).TeamId);
        }
        else
        {
            Assert.Equal((HttpStatusCode.NotFound, TeamNotFound), (unlink.Status, unlink.ErrorCode));
            Assert.Null((await StoredAsync(factory, world.ProjectId, story.Identifier)).TeamId);
        }

        Assert.Equal(TeamStatus.Deleted, (await StoredTeamAsync(factory, world.Mars)).Status);
        Assert.Null(await DefaultSettingAsync(world));
        await AssertInvariantsAsync(world);
    }

    // C-8 · an unlink of MARS naming VENUS against the deletion of VENUS. Unlink first: VENUS receives MARS's stories, then the deletion
    // releases them to nobody. Deletion first: the unlink re-reads VENUS deleted — 404 team.not_found — and changes nothing: MARS stays
    // linked and keeps its stories.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unlink_VersusTheDeletionOfTheDestination_BothOrders(bool unlinkFirst)
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world, "На Марсе", world.Mars);

        Task<Answer> Unlink(CommitGate? at) => UnlinkAsync(world, world.Mars, world.Venus, at);
        Task<Answer> Deletion(CommitGate? at) => DeleteTeamAsync(factory, world.OrganizationId, world.Venus, gate: at);
        var (unlink, deletion) = unlinkFirst
            ? await RaceAsync(factory, Unlink, Deletion)
            : Flip(await RaceAsync(factory, Deletion, Unlink));

        Assert.Equal(HttpStatusCode.NoContent, deletion.Status);

        if (unlinkFirst)
        {
            Assert.Equal(HttpStatusCode.NoContent, unlink.Status);
            Assert.Null((await StoredAsync(factory, world.ProjectId, story.Identifier)).TeamId);
            Assert.DoesNotContain(world.Mars, (await LinksOfProjectAsync(factory, world.ProjectId)).Select(link => link.TeamId));
        }
        else
        {
            Assert.Equal((HttpStatusCode.NotFound, TeamNotFound), (unlink.Status, unlink.ErrorCode));
            Assert.Equal(world.Mars, (await StoredAsync(factory, world.ProjectId, story.Identifier)).TeamId);
            Assert.Contains(world.Mars, (await LinksOfProjectAsync(factory, world.ProjectId)).Select(link => link.TeamId));
        }

        Assert.Equal(TeamStatus.Deleted, (await StoredTeamAsync(factory, world.Venus)).Status);
        await AssertInvariantsAsync(world);
    }

    // C-9 · an unlink of MARS against a link of the same pair that comes in while the unlink is held: the link waits on the row the unlink
    // is deleting, the unlink commits, the link is inserted anew — and nothing moves back: the stories stay VENUS's.
    [Fact]
    public async Task Unlink_VersusALinkOfTheSamePair_TheLinkIsInsertedAnew_NothingMovesBack()
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world, "На Марсе", world.Mars);

        var (unlink, link) = await RaceAsync(
            factory,
            at => UnlinkAsync(world, world.Mars, world.Venus, at),
            at => LinkTeamsAsync(factory, world.OrganizationId, world.ProjectId, new { teamIds = new[] { world.Mars.ToString() } }, gate: at));

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (unlink.Status, link.Status));
        Assert.Contains(world.Mars, (await LinksOfProjectAsync(factory, world.ProjectId)).Select(l => l.TeamId));
        Assert.Equal(world.Venus, (await StoredAsync(factory, world.ProjectId, story.Identifier)).TeamId);
        Assert.Equal(0, await OwnedItemCountAsync(factory, world.Mars));
        await AssertInvariantsAsync(world);
    }

    // D-4, bug-005 (mod-017 CR-7) · "Create_VersusTheDeletionOfTheNamedDefault_AndASecondDeletionOrAnUnlink_NoDeadlock": three requests in one
    // project — the deletion of the team the project names as its default, a creation with no team field (it must resolve the default), and
    // a second deletion (or an unlink) of another team of the project. The creation takes EVERY link of the project FOR SHARE in one statement
    // ordered by team id, so every link lock it holds is in the order of the deletion and the unlink: there is no cycle, whichever of the two
    // teams carries the lower id. The three finish — 204, 201, 204 — and none is a 5xx.
    [Theory]
    [InlineData("deletion", true)]
    [InlineData("deletion", false)]
    [InlineData("unlink", true)]
    [InlineData("unlink", false)]
    public async Task Create_VersusTheDeletionOfTheNamedDefault_AndASecondDeletionOrAnUnlink_NoDeadlock(string second, bool namedIsTheLowerId)
    {
        var world = await CreateWorldAsync(factory);
        var (named, other) = namedIsTheLowerId ? (world.Mars, world.Venus) : (world.Venus, world.Mars);
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE projects SET default_team_id = {named} WHERE id = {world.ProjectId}");
        using var gate = factory.CommitGates.Open();

        var deletion = DeleteTeamAsync(factory, world.OrganizationId, named, gate: gate);
        await Task.WhenAny(gate.Reached, deletion);
        Assert.False(deletion.IsCompleted, "The deletion finished before reaching its commit gate.");

        var creation = OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new { requestId = Guid.NewGuid().ToString(), typeId = world.Items.Types["US"].Id.ToString(), title = "Без поля команды" },
            OrganizationsTokens.SignedIn());
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, creation);
        Assert.False(creation.IsCompleted, "The creation did not wait for the deletion's lock of the default.");

        var followUp = second == "deletion"
            ? DeleteTeamAsync(factory, world.OrganizationId, other)
            : UnlinkAsync(world, other, null, null);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, followUp);

        gate.Release();
        var answers = await Task.WhenAll(deletion, creation, followUp);

        Assert.Equal(
            (HttpStatusCode.NoContent, HttpStatusCode.Created, HttpStatusCode.NoContent),
            (answers[0].Status, answers[1].Status, answers[2].Status));
        var created = await StoredAsync(factory, world.ProjectId, answers[1].Body.GetProperty("identifier").GetString()!);
        Assert.NotEqual(named, created.TeamId);
        Assert.NotEqual(other, created.TeamId);
        Assert.Null(await DefaultSettingAsync(world));
        await AssertInvariantsAsync(world);
    }

    /// <summary>The race helper answers (held, waiting); a test that held the other request first wants the pair the other way round.</summary>
    private static (Answer A, Answer B) Flip((Answer Held, Answer Waiting) answers) => (answers.Waiting, answers.Held);

    private async Task<Guid?> DefaultSettingAsync(OwningTeamWorld world) => await SettingAsync(factory, world.ProjectId);
}
