using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// <c>POST …/workitems</c> with the owning team (mod-017 api-contract §§ 0.2, 2.2; US-2, BR-OT-2, BR-OT-3) through the real host
/// and a real PostgreSQL. A creation sends the team the dialog shows, or none and takes the project default team as stored when
/// it is saved; a creation that names a parent reads no team at all. Every test builds its own world
/// (<see cref="OwningTeamTestData"/>): the database is shared and never emptied.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class CreateOwningTeamEndpointTests(MustaskApiFactory factory)
{
    private const string TeamNotFound = "organizations.team.not_found";
    private const string TeamNotInProject = "organizations.team.not_in_project";

    private static object Story(OwningTeamWorld world, string title, object? team = null, Guid? requestId = null, string? parentId = null) =>
        new
        {
            requestId = (requestId ?? Guid.NewGuid()).ToString(),
            typeId = world.Items.Types["US"].Id.ToString(),
            title,
            team,
            parentId,
        };

    private Task<Answer> CreateAsync(OwningTeamWorld world, object body, string? token = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, WorkItemsTestData.List(world.Items), body, token ?? OrganizationsTokens.SignedIn(), gate);

    private static object Team(Guid? teamId) => new { teamId = teamId?.ToString() };

    private static Guid? TeamIdOf(Answer answer) => TeamOf(answer.Body)?.Id;

    private Task NameDefaultAsync(OwningTeamWorld world, Guid? teamId) =>
        WorkItemsTestData.ExecuteAsync(factory, $"UPDATE projects SET default_team_id = {teamId} WHERE id = {world.ProjectId}");

    // US-2 · outline "a project's default": no team field takes the project default team as it stands when the item is saved
    // (BR-OT-4) — the named team; the project's only team when none is named; none when there are several and none is named.
    [Fact]
    public async Task Create_NoTeamField_TakesTheProjectDefault_Named()
    {
        var world = await CreateWorldAsync(factory);
        await NameDefaultAsync(world, world.Mars);

        var answer = await CreateAsync(world, Story(world, "Вход по ссылке"));

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal((world.Mars, "MARS"), TeamOf(answer.Body));
        Assert.Equal(world.Mars, (await StoredAsync(factory, world.ProjectId, answer.Body.GetProperty("identifier").GetString()!)).TeamId);
    }

    [Fact]
    public async Task Create_NoTeamField_TakesTheProjectDefault_TheOnlyTeam()
    {
        var world = await CreateWorldAsync(factory);
        var single = await OrganizationsTestData.CreateProjectAsync(factory, world.OrganizationId, "Одна", "ONE");
        var types = world.Items.Types;
        var items = world.Items with { ProjectId = single.Project.Id, RootAreaId = single.Root.Id, Key = "ONE", Types = types };

        var answer = await WorkItemsTestData.CreateAsync(factory, items, "US", "Единственная");

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(single.FirstTeam.Id, TeamIdOf(answer));
    }

    [Fact]
    public async Task Create_NoTeamField_TwoTeamsAndNoneNamed_IsNone()
    {
        var world = await CreateWorldAsync(factory);

        var answer = await CreateAsync(world, Story(world, "Без умолчания"));

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Null(TeamOf(answer.Body));
    }

    // US-3 · "losing the named team": a named team that was deleted is no longer named, so the project falls back to its only
    // team, or to none.
    [Fact]
    public async Task Create_NoTeamField_NamedThenDeleted_FallsBackToNone()
    {
        var world = await CreateWorldAsync(factory);
        var gone = await AddTeamAsync(factory, world.ProjectId, "PLUTO");
        await NameDefaultAsync(world, gone);
        await OrganizationsTestData.DeleteTeamAsync(factory, world.ProjectId, gone);

        var answer = await CreateAsync(world, Story(world, "После удаления"));

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Null(TeamOf(answer.Body));
    }

    // US-2 · "the dialog says where": the item's area is the project's root, whatever the team (BR-OT-10, OQ-4 A).
    [Fact]
    public async Task Create_NewItem_TakesTheRootArea_WhateverTheTeam()
    {
        var world = await CreateWorldAsync(factory);

        var withTeam = await CreateAsync(world, Story(world, "С командой", Team(world.Venus)));
        var without = await CreateAsync(world, Story(world, "Без команды", Team(null)));

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (withTeam.Status, without.Status));
        Assert.Equal(
            (world.Items.RootAreaId, world.Items.RootAreaId),
            (withTeam.Body.GetProperty("area").GetProperty("id").GetGuid(), without.Body.GetProperty("area").GetProperty("id").GetGuid()));
        Assert.Equal("Альфа", withTeam.Body.GetProperty("area").GetProperty("path").GetString());
    }

    // US-2 · "the dialog creates with the team it shows": a default named while the dialog was open does not change the
    // team the dialog sends.
    [Fact]
    public async Task Create_DefaultNamedWhileTheDialogIsOpen_KeepsTheSentTeam()
    {
        var world = await CreateWorldAsync(factory);
        await NameDefaultAsync(world, world.Mars);
        await NameDefaultAsync(world, world.Venus);

        var answer = await CreateAsync(world, Story(world, "Показанная", Team(world.Mars)));

        Assert.Equal(world.Mars, TeamIdOf(answer));
    }

    // US-2 · "a row's add and «Добавить задачу» send no team": a field left out takes the default as stored when the item is
    // saved, not as it was when the page was read.
    [Fact]
    public async Task Create_FieldOmitted_TakesTheDefaultAsStoredAtSave()
    {
        var world = await CreateWorldAsync(factory);
        await NameDefaultAsync(world, world.Mars);
        await NameDefaultAsync(world, world.Venus);

        var answer = await CreateAsync(world, Story(world, "По умолчанию"));

        Assert.Equal(world.Venus, TeamIdOf(answer));
    }

    // US-2 · "from a team's surface": owned by the team, the last row of both backlogs, and no place written on either order.
    [Fact]
    public async Task Create_FromTeamSurface_OwnedByTheTeam_LastRowOfBothBacklogs_NoPlaceWritten()
    {
        var world = await CreateWorldAsync(factory);
        var boards = BoardsViewOf(world);
        var earlier = await CreateAsync(world, Story(world, "Ранняя", Team(world.Mars)));
        var earlierId = earlier.Body.GetProperty("identifier").GetString()!;

        var answer = await CreateAsync(world, Story(world, "Новая", Team(world.Mars)));

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        var created = answer.Body.GetProperty("identifier").GetString()!;
        Assert.Equal([earlierId, created], await BoardsTestData.RowsAsync(factory, boards, world.Mars));
        Assert.Equal([earlierId, created], await ProjectBoardsTestData.RowsAsync(factory, boards));
        Assert.Equal(0, await TeamPlacesOfAsync(factory, answer.Body.GetProperty("id").GetGuid()));
        Assert.Equal(0, await ProjectPlacesOfAsync(factory, answer.Body.GetProperty("id").GetGuid()));
    }

    // US-2 · "the dialog may change it": a team surface's dialog may send another team of the project, or none.
    [Fact]
    public async Task Create_FromTeamSurface_OtherTeamOrNone()
    {
        var world = await CreateWorldAsync(factory);
        var boards = BoardsViewOf(world);

        var other = await CreateAsync(world, Story(world, "Другой", Team(world.Venus)));
        var none = await CreateAsync(world, Story(world, "Без", Team(null)));

        Assert.Equal(world.Venus, TeamIdOf(other));
        Assert.Null(TeamOf(none.Body));
        Assert.Equal([other.Body.GetProperty("identifier").GetString()!], await BoardsTestData.RowsAsync(factory, boards, world.Venus));
        Assert.Empty(await BoardsTestData.RowsAsync(factory, boards, world.Mars));
    }

    // US-2 · outline "a direct creation naming a team", the four refusals: another project's team is 409, a deleted, a
    // deactivated project's, an unknown and a malformed one 404; nothing is created and no number is consumed.
    [Theory]
    [InlineData("otherProject", HttpStatusCode.Conflict, TeamNotInProject)]
    [InlineData("deleted", HttpStatusCode.NotFound, TeamNotFound)]
    [InlineData("deactivatedProject", HttpStatusCode.NotFound, TeamNotFound)]
    [InlineData("unknown", HttpStatusCode.NotFound, TeamNotFound)]
    [InlineData("malformed", HttpStatusCode.NotFound, TeamNotFound)]
    [InlineData("foreign", HttpStatusCode.NotFound, TeamNotFound)]
    public async Task Create_TeamRefusals_NothingCreated_NoNumberConsumed(string which, HttpStatusCode status, string code)
    {
        var world = await CreateWorldAsync(factory);
        var named = which switch
        {
            "otherProject" => world.OtherProjectTeam.ToString(),
            "deleted" => world.DeletedTeam.ToString(),
            "deactivatedProject" => world.DeactivatedProjectTeam.ToString(),
            "foreign" => world.OtherOrganizationTeam.ToString(),
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => "not-an-id",
        };

        var refused = await CreateAsync(world, Story(world, "Отказ", new { teamId = named }));

        Assert.Equal((status, code), (refused.Status, refused.ErrorCode));
        Assert.Empty(await WorkItemsTestData.StoredAsync(factory, world.ProjectId));

        var next = await CreateAsync(world, Story(world, "Следующая"));
        Assert.Equal("ALP-US-1", next.Body.GetProperty("identifier").GetString());
    }

    // BR-OT-16 · another organization's team answers exactly as an unknown id.
    [Fact]
    public async Task Create_ForeignTeam_AnswersExactlyAsUnknown()
    {
        var world = await CreateWorldAsync(factory);

        var foreign = await CreateAsync(world, Story(world, "Чужая", Team(world.OtherOrganizationTeam)));
        var unknown = await CreateAsync(world, Story(world, "Неизвестная", Team(Guid.CreateVersion7())));

        Assert.Equal((unknown.Status, unknown.ErrorCode, unknown.Detail, unknown.MediaType), (foreign.Status, foreign.ErrorCode, foreign.Detail, foreign.MediaType));
    }

    // api-contract § 0.4: a repeated submission answers 200 with its item as it is now, before the team is looked at — so a
    // replay carrying a team that is not a team any more still answers its item.
    [Fact]
    public async Task Create_Replay_AnswersTheItem_BeforeTheTeamIsJudged()
    {
        var world = await CreateWorldAsync(factory);
        var temporary = await AddTeamAsync(factory, world.ProjectId, "CERES");
        var requestId = Guid.NewGuid();
        var first = await CreateAsync(world, Story(world, "Один раз", Team(temporary), requestId));
        Assert.Equal(HttpStatusCode.Created, first.Status);
        await OrganizationsTestData.DeleteTeamAsync(factory, world.ProjectId, temporary);

        var replay = await CreateAsync(world, Story(world, "Один раз", Team(temporary), requestId));

        Assert.Equal(HttpStatusCode.OK, replay.Status);
        Assert.Equal(first.Body.GetProperty("id").GetGuid(), replay.Body.GetProperty("id").GetGuid());
    }

    // US-2 · "who creates": «Читатель» holds no workitems.workitem.write.
    [Fact]
    public async Task Create_Reader_Returns403_CreatesNothing()
    {
        var world = await CreateWorldAsync(factory);
        var reader = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);

        var answer = await CreateAsync(world, Story(world, "Не создам", Team(world.Mars)), reader.Token);

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (answer.Status, answer.ErrorCode));
        Assert.Empty(await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
    }

    // --- A child -----------------------------------------------------------------------------------------------------

    private async Task<(Guid Id, string Identifier)> ParentAsync(OwningTeamWorld world, Guid? team)
    {
        var answer = await CreateAsync(world, new
        {
            requestId = Guid.NewGuid().ToString(),
            typeId = world.Items.Types["FEAT"].Id.ToString(),
            title = "Родитель",
            team = Team(team),
        });
        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return (answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
    }

    // US-2 · "a child starts with its parent's team": the parent's team as stored; if the parent has none, the project default
    // team; else none — whatever team the body also carries, on any surface (BR-OT-3).
    [Fact]
    public async Task Create_Child_TakesParentTeam_ElseProjectDefault_OnAnySurface()
    {
        var world = await CreateWorldAsync(factory);
        var owned = await ParentAsync(world, world.Mars);
        var unowned = await ParentAsync(world, null);

        var underOwned = await CreateAsync(world, Story(world, "Под командой", Team(world.Venus), parentId: owned.Id.ToString()));
        var noDefault = await CreateAsync(world, Story(world, "Без умолчания", parentId: unowned.Id.ToString()));
        await NameDefaultAsync(world, world.Venus);
        var withDefault = await CreateAsync(world, Story(world, "С умолчанием", parentId: unowned.Id.ToString()));

        Assert.Equal(world.Mars, TeamIdOf(underOwned));
        Assert.Null(TeamOf(noDefault.Body));
        Assert.Equal(world.Venus, TeamIdOf(withDefault));
    }

    // US-2 · "a child is never refused for a team": the team of a child's creation is not read, so any value in the body —
    // another project's team, a deleted one, a malformed one — refuses nothing.
    [Theory]
    [InlineData("otherProject")]
    [InlineData("deleted")]
    [InlineData("malformed")]
    public async Task Create_ChildWithATeamInTheBody_TeamIsNotRead(string which)
    {
        var world = await CreateWorldAsync(factory);
        var parent = await ParentAsync(world, world.Mars);
        var named = which switch
        {
            "otherProject" => world.OtherProjectTeam.ToString(),
            "deleted" => world.DeletedTeam.ToString(),
            _ => "not-an-id",
        };

        var answer = await CreateAsync(world, Story(world, "Ребёнок", new { teamId = named }, parentId: parent.Id.ToString()));

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(world.Mars, TeamIdOf(answer));
    }

    // US-2 · "the parent's team is read when the child is saved", the save first (gate): a save moving the parent to VENUS holds at
    // its commit; the child waits on the parent and takes the team the save committed.
    [Fact]
    public async Task Create_ChildWhileParentTeamChanges_TakesTheTeamAsStored_SaveFirst()
    {
        var world = await CreateWorldAsync(factory);
        var parent = await ParentAsync(world, world.Mars);
        using var gate = factory.CommitGates.Open();

        var save = WorkItemsTestData.SaveAsync(factory, world.Items, parent.Identifier, new { team = Team(world.Venus) }, gate);
        await Task.WhenAny(gate.Reached, save);
        Assert.False(save.IsCompleted, "The save finished before reaching its commit gate.");

        var creation = CreateAsync(world, Story(world, "Ребёнок", parentId: parent.Id.ToString()));
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, creation);
        Assert.False(creation.IsCompleted, "The creation did not wait on the parent.");

        gate.Release();
        Assert.Equal(HttpStatusCode.OK, (await save).Status);
        var answer = await creation;

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(world.Venus, TeamIdOf(answer));
    }

    // US-2 · the creation first (gate): the child is saved with the team the parent has then; the save that comes after moves
    // the parent alone — no child follows its parent's team (BR-OT-1).
    [Fact]
    public async Task Create_ChildWhileParentTeamChanges_TakesTheTeamAsStored_CreationFirst()
    {
        var world = await CreateWorldAsync(factory);
        var parent = await ParentAsync(world, world.Mars);
        using var gate = factory.CommitGates.Open();

        var creation = CreateAsync(world, Story(world, "Ребёнок", parentId: parent.Id.ToString()), gate: gate);
        await Task.WhenAny(gate.Reached, creation);
        Assert.False(creation.IsCompleted, "The creation finished before reaching its commit gate.");

        var save = WorkItemsTestData.SaveAsync(factory, world.Items, parent.Identifier, new { team = Team(world.Venus) });
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, save);
        Assert.False(save.IsCompleted, "The save did not wait on the creation's lock of the parent.");

        gate.Release();
        var answer = await creation;
        Assert.Equal(HttpStatusCode.OK, (await save).Status);

        Assert.Equal(world.Mars, TeamIdOf(answer));
        Assert.Equal(world.Venus, (await StoredAsync(factory, world.ProjectId, parent.Identifier)).TeamId);
        Assert.Equal(world.Mars, (await StoredAsync(factory, world.ProjectId, answer.Body.GetProperty("identifier").GetString()!)).TeamId);
    }

    // US-2 · "a created item is unplaced on both orders" (BR-OT-6).
    [Fact]
    public async Task Create_IsUnplacedOnBothOrders()
    {
        var world = await CreateWorldAsync(factory);

        var answer = await CreateAsync(world, Story(world, "Новая", Team(world.Mars)));

        var id = answer.Body.GetProperty("id").GetGuid();
        Assert.Equal((0, 0), (await TeamPlacesOfAsync(factory, id), await ProjectPlacesOfAsync(factory, id)));
        Assert.Equal(JsonValueKind.Object, answer.Body.GetProperty("team").ValueKind);
    }
}
