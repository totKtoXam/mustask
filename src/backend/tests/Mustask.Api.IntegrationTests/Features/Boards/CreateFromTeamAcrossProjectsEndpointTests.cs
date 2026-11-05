using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using static Mustask.Api.IntegrationTests.Features.Boards.TeamAcrossProjectsTestData;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// Creating from a team view, the project chosen first (feat-040 api-contract § 7; US-5; BR-TS-9, BR-TS-13): the create command is
/// mod-003's <c>POST …/projects/{chosen project}/workitems</c> with <c>team</c>, the project taken from the team view's
/// <c>projects</c>. The item is created in the chosen project with its next number, owned by the team shown or by none, and
/// unplaced on both backlogs; an unlinked team (409), a deactivated project (404) and a deleted team (404) refuse it, create
/// nothing and consume no number; a creation against an unlink ends in one order with no server error. (The single-project forms
/// are <c>CreateOwningTeamEndpointTests</c>' and <c>CreateFromTeamEndpointTests</c>'.)
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class CreateFromTeamAcrossProjectsEndpointTests(MustaskApiFactory factory)
{
    private static Item Created(Answer answer)
    {
        Assert.True(answer.Status == HttpStatusCode.Created, $"The creation answered {answer.Status} {answer.ErrorCode}.");

        return new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
    }

    private Task<Answer> PostAsync(BoardsWorld view, Guid? team, string title = "Новая", CommitGate? gate = null, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(view.Items),
            new
            {
                requestId = Guid.NewGuid().ToString(),
                typeId = view.Items.Types["US"].Id.ToString(),
                title,
                team = new { teamId = team?.ToString() },
            },
            token ?? OrganizationsTokens.SignedIn(),
            gate);

    private async Task<int> StoredCountAsync(BoardsWorld view) => (await WorkItemsTestData.StoredAsync(factory, view.ProjectId)).Count;

    // US-5 · "The item is created in the chosen project, owned by the team" (BR-TS-9; mod-017 BR-OT-6): Бета holds BET-US-1..3 when
    // Max chooses Бета on MARS's backlog; BET-US-4 is created in Бета, owned by MARS, unplaced on MARS's backlog and on Бета's, below
    // every placed item, and shows on MARS's backlog and board with «Проект» «Бета». The answer carries the item's `project`.
    [Fact]
    public async Task Create_InTheChosenProject_OwnedByTheTeam_Unplaced_OnBothBacklogs()
    {
        var cast = await CreateAsync(factory);
        var b1 = await StoryAsync(factory, cast.Beta, "B1", cast.Mars);
        var b2 = await StoryAsync(factory, cast.Beta, "B2", cast.Mars);
        var b3 = await StoryAsync(factory, cast.Beta, "B3", cast.Mars);
        var a1 = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        await OrderAsync(factory, cast, cast.Mars, b1, a1, b2, b3);
        await ProjectBoardsTestData.MoveAsync(factory, cast.Beta, BoardsTestData.Top(b3));
        var projectOrder = await ProjectBoardsTestData.RowsAsync(factory, cast.Beta);

        var answer = await PostAsync(cast.Beta, cast.Mars, "Четвёртая");

        var created = Created(answer);
        Assert.Equal("BET-US-4", created.Identifier);
        Assert.Equal((cast.Beta.ProjectId, "Бета"), (answer.Body.GetProperty("project").GetProperty("id").GetGuid(), answer.Body.GetProperty("project").GetProperty("name").GetString()));
        Assert.Equal(cast.Beta.ProjectId, answer.Body.GetProperty("projectId").GetGuid());
        Assert.Equal(cast.Mars, answer.Body.GetProperty("team").GetProperty("id").GetGuid());
        Assert.Equal(IdsOf(b1, a1, b2, b3, created), await RowsAsync(factory, cast, cast.Mars));
        Assert.Equal(IdsOf(b3, b1, b2, created), await ProjectBoardsTestData.RowsAsync(factory, cast.Beta));
        Assert.Equal(projectOrder, (await ProjectBoardsTestData.RowsAsync(factory, cast.Beta)).Take(projectOrder.Length));
        Assert.Equal(0, await TeamPlacesOfAsync(factory, created.Id));
        Assert.Equal(0, await ProjectPlacesOfAsync(factory, created.Id));
        Assert.Contains(created.Identifier, await CardsAsync(factory, cast, cast.Mars));
        Assert.Equal(Wire(cast.Beta, "Бета"), ProjectOf(Row(await GetBacklogAsync(factory, cast, cast.Mars), created.Identifier)));
    }

    private static string[] IdsOf(params Item[] items) => items.Select(item => item.Identifier).ToArray();

    // US-5 · "«Команда» offers the chosen project's teams and «Без команды»" (BR-TS-9; mod-017 BR-OT-3): Альфа offers MARS and VENUS,
    // Бета MARS and JUPITER but not VENUS (which is not linked to it); «Без команды» creates the item with no owning team — it is on
    // the project's views and on no team's.
    [Fact]
    public async Task Create_TheTeamsOfferedAreTheChosenProjects_AndNoTeamIsAnOption()
    {
        var cast = await CreateAsync(factory);
        var inAlpha = await TeamTestData.GetAsync(factory, OrganizationsApi.Teams(cast.OrganizationId, cast.Alpha.ProjectId));
        var inBeta = await TeamTestData.GetAsync(factory, OrganizationsApi.Teams(cast.OrganizationId, cast.Beta.ProjectId));

        var unowned = Created(await PostAsync(cast.Beta, null, "Без команды"));

        Assert.Contains(cast.Mars, TeamTestData.Ids(inAlpha.Body));
        Assert.Contains(cast.Venus, TeamTestData.Ids(inAlpha.Body));
        Assert.Contains(cast.Mars, TeamTestData.Ids(inBeta.Body));
        Assert.Contains(cast.Jupiter, TeamTestData.Ids(inBeta.Body));
        Assert.DoesNotContain(cast.Venus, TeamTestData.Ids(inBeta.Body));
        Assert.Contains(unowned.Identifier, await ProjectBoardsTestData.RowsAsync(factory, cast.Beta));
        Assert.DoesNotContain(unowned.Identifier, await RowsAsync(factory, cast, cast.Mars));
        Assert.DoesNotContain(unowned.Identifier, await RowsAsync(factory, cast, cast.Jupiter));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, (await WorkItemsTestData.GetAsync(factory, cast.Beta.Items, unowned.Identifier)).Body.GetProperty("team").ValueKind);
    }

    // US-5 · "The team is unlinked ... before the save" (BR-TS-9; BR-MX-9): Ann unlinks Бета from MARS, Max presses «Создать» — 409
    // «Команда не участвует в проекте.», no item, no number consumed (the next creation takes the next number).
    [Fact]
    public async Task Create_TheTeamUnlinkedMeanwhile_Is409TeamNotInProject_NothingCreated_NoNumberConsumed()
    {
        var cast = await CreateAsync(factory);
        await StoryAsync(factory, cast.Beta, "B1", cast.Mars);
        Assert.Equal(HttpStatusCode.NoContent, (await TeamTestData.UnlinkAsync(factory, cast.OrganizationId, cast.Beta.ProjectId, cast.Mars, TeamTestData.To(null))).Status);
        var before = await StoredCountAsync(cast.Beta);

        var refused = await PostAsync(cast.Beta, cast.Mars);
        var next = Created(await PostAsync(cast.Beta, null));

        BoardsTestData.AssertRefused(refused, HttpStatusCode.Conflict, "organizations.team.not_in_project");
        Assert.Equal("Команда не участвует в проекте.", refused.Detail);
        Assert.Equal(before + 1, await StoredCountAsync(cast.Beta));
        Assert.Equal("BET-US-2", next.Identifier);
    }

    // US-5 · "... or the project deactivated": «Проект не найден.», no item, no number consumed; after the restore the numbering goes on.
    [Fact]
    public async Task Create_TheProjectDeactivatedMeanwhile_Is404ProjectNotFound_NothingCreated_NoNumberConsumed()
    {
        var cast = await CreateAsync(factory);
        await StoryAsync(factory, cast.Beta, "B1", cast.Mars);
        await DeactivateAsync(factory, cast, cast.Beta);

        var refused = await PostAsync(cast.Beta, cast.Mars);
        await RestoreAsync(factory, cast, cast.Beta);
        var next = Created(await PostAsync(cast.Beta, cast.Mars));

        BoardsTestData.AssertRefused(refused, HttpStatusCode.NotFound, "organizations.project.not_found");
        Assert.Equal("Проект не найден.", refused.Detail);
        Assert.Equal("BET-US-2", next.Identifier);
        Assert.Equal(2, await StoredCountAsync(cast.Beta));
    }

    // US-5 · "... or the team deleted": «Команда не найдена.», no item, no number consumed.
    [Fact]
    public async Task Create_TheTeamDeletedMeanwhile_Is404TeamNotFound_NothingCreated_NoNumberConsumed()
    {
        var cast = await CreateAsync(factory);
        await StoryAsync(factory, cast.Beta, "B1", cast.Mars);
        Assert.Equal(HttpStatusCode.NoContent, (await TeamTestData.DeleteTeamAsync(factory, cast.OrganizationId, cast.Mars)).Status);

        var refused = await PostAsync(cast.Beta, cast.Mars);
        var next = Created(await PostAsync(cast.Beta, null));

        BoardsTestData.AssertRefused(refused, HttpStatusCode.NotFound, "organizations.team.not_found");
        Assert.Equal("Команда не найдена.", refused.Detail);
        Assert.Equal("BET-US-2", next.Identifier);
    }

    // US-5 · "A save that commits before the unlink is reassigned with the rest" (BR-TS-9; feat-028 BR-OL-8): the creation first, held
    // at its commit — the unlink waits, then makes the new item «Без команды»; the unlink first — the creation is refused 409. Either
    // way no request ends in a server error and no item is owned by a team not linked to its project.
    [Fact]
    public async Task Race_TheCreationFirst_ThenTheUnlink_ReassignsTheNewItem()
    {
        var cast = await CreateAsync(factory);
        await StoryAsync(factory, cast.Beta, "B1", cast.Mars);

        var (creation, unlink) = await TeamTestData.RaceAsync(
            factory,
            gate => PostAsync(cast.Beta, cast.Mars, "Гонка", gate),
            _ => TeamTestData.UnlinkAsync(factory, cast.OrganizationId, cast.Beta.ProjectId, cast.Mars, TeamTestData.To(null)));

        var created = Created(creation);
        Assert.Equal(HttpStatusCode.NoContent, unlink.Status);
        Assert.Null((await StoredAsync(factory, cast.Beta.ProjectId, created.Identifier)).TeamId);
        Assert.Equal(0, await PlacesNamingAnotherTeamAsync(factory, cast.Beta.ProjectId));
    }

    [Fact]
    public async Task Race_TheUnlinkFirst_ThenTheCreation_Is409()
    {
        var cast = await CreateAsync(factory);
        await StoryAsync(factory, cast.Beta, "B1", cast.Mars);

        var (unlink, creation) = await TeamTestData.RaceAsync(
            factory,
            gate => TeamTestData.UnlinkAsync(factory, cast.OrganizationId, cast.Beta.ProjectId, cast.Mars, TeamTestData.To(null), gate: gate),
            _ => PostAsync(cast.Beta, cast.Mars, "Гонка"));

        Assert.Equal(HttpStatusCode.NoContent, unlink.Status);
        BoardsTestData.AssertRefused(creation, HttpStatusCode.Conflict, "organizations.team.not_in_project");
        Assert.Equal(1, await StoredCountAsync(cast.Beta));
    }

    // US-5 · "«Читатель» ... her direct creation in Бета naming MARS is refused with 403": nothing is created and no number consumed;
    // «Участник» may create.
    [Fact]
    public async Task Create_RolesViewerIs403_MemberCreates()
    {
        var cast = await CreateAsync(factory);
        var vera = await MembershipTestData.NewMemberAsync(factory, cast.OrganizationId, OrganizationRole.Viewer);
        var max = await MembershipTestData.NewMemberAsync(factory, cast.OrganizationId, OrganizationRole.Member);

        var viewer = await PostAsync(cast.Beta, cast.Mars, token: vera.Token);
        var member = await PostAsync(cast.Beta, cast.Mars, token: max.Token);

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (viewer.Status, viewer.ErrorCode));
        Assert.Equal("BET-US-1", Created(member).Identifier);
        Assert.Equal(1, await StoredCountAsync(cast.Beta));
    }

    // US-5 · "Ann unlinks Gamma from MARS while Max's dialog is open: the next time Max opens the dialog, «Проект» offers Alpha and
    // Beta only": the team views' `projects` (the options) follow the links at the next read.
    [Fact]
    public async Task ProjectsOffered_FollowTheLinksAtTheNextRead()
    {
        var cast = await CreateAsync(factory);
        Assert.Equal(3, (await GetBacklogAsync(factory, cast, cast.Mars)).Body.GetProperty("projects").GetArrayLength());

        await TeamTestData.UnlinkAsync(factory, cast.OrganizationId, cast.Gamma.ProjectId, cast.Mars, TeamTestData.To(null));

        Assert.Equal([Wire(cast.Alpha, "Альфа"), Wire(cast.Beta, "Бета")], ProjectsOf(await GetBacklogAsync(factory, cast, cast.Mars)));
        Assert.Equal([Wire(cast.Alpha, "Альфа"), Wire(cast.Beta, "Бета")], ProjectsOf(await GetBoardAsync(factory, cast, cast.Mars)));
    }
}
