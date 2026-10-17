using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// mod-017 US-6, BR-OT-10 through the real host and a real PostgreSQL: an area owns nothing and decides no team. The two routes
/// that wrote the ownership are gone, a new item takes the root and a child its parent's area, and an area edit — a rename, a
/// move, an item's own area on its form — moves no item, changes no team and no «Изменён». Every test builds its own world
/// (<see cref="OwningTeamTestData"/>): the database is shared and never emptied.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class AreasOwnNothingEndpointTests(MustaskApiFactory factory)
{
    private string Project(OwningTeamWorld world) => OrganizationsApi.Project(world.OrganizationId, world.ProjectId);

    private Task<Answer> PutAsync(string path, object body) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Put, path, body, OrganizationsTokens.SignedIn());

    // US-6 · "the owner and default-area writes are gone": both routes answer 404 as any route that does not exist, with a body
    // that would once have been valid, and nothing changes. mod-017 api-contract § 3 (the two routes are removed).
    [Theory]
    [InlineData("default-area")]
    [InlineData("owner")]
    public async Task RetiredRoutes_Answer404_NothingChanges(string which)
    {
        var world = await CreateWorldAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, world.ProjectId, world.Items.RootAreaId, "Отчёты");
        var treeBefore = (await OrganizationsTestData.ReadTreeAsync(factory, world.ProjectId)).PreOrder().ToList();

        var answer = which == "default-area"
            ? await PutAsync($"{Project(world)}/teams/{world.Mars}/default-area", new { areaId = reports.Id.ToString() })
            : await PutAsync($"{Project(world)}/areas/{reports.Id}/owner", new { ownerTeamId = world.Mars.ToString() });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal(treeBefore, (await OrganizationsTestData.ReadTreeAsync(factory, world.ProjectId)).PreOrder().ToList());
        Assert.Equal(
            HttpStatusCode.OK,
            (await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{Project(world)}/teams/{world.Mars}", token: OrganizationsTokens.SignedIn())).Status);
    }

    // US-6 · "moving or renaming an area moves no item": an item in the area keeps its team, its area and its «Изменён» through
    // a rename and a move of the area, and shows the new path. (BR-OT-10; the move and the rename write the area row alone.)
    [Fact]
    public async Task MoveAndRename_MoveNoItem_KeepTeamAndChangedAt()
    {
        var world = await CreateWorldAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, world.ProjectId, world.Items.RootAreaId, "Отчёты");
        var archive = await OrganizationsTestData.AddAreaAsync(factory, world.ProjectId, world.Items.RootAreaId, "Архив");
        var item = await OwningTeamTestData.CreateAsync(factory, world.Items, "TASK", "Задача");
        Assert.Equal(HttpStatusCode.OK, (await WorkItemsTestData.SaveAsync(factory, world.Items, item.Identifier, new { areaId = reports.Id.ToString() })).Status);
        await SetOwningTeamAsync(factory, item.Id, world.Mars);
        var before = await StoredAsync(factory, world.ProjectId, item.Identifier);

        var renamed = await PutAsync($"{Project(world)}/areas/{reports.Id}/name", new { name = "Сводки" });
        var moved = await PutAsync($"{Project(world)}/areas/{reports.Id}/parent", new { parentId = archive.Id.ToString() });

        Assert.Equal(HttpStatusCode.OK, renamed.Status);
        Assert.Equal(HttpStatusCode.OK, moved.Status);
        Assert.Equal(before, await StoredAsync(factory, world.ProjectId, item.Identifier));
        var read = await WorkItemsTestData.ReadAsync(factory, world.Items, item.Identifier);
        Assert.Equal("Архив / Сводки", read.GetProperty("area").GetProperty("path").GetString());
        Assert.Equal(world.Mars, TeamOf(read)!.Value.Id);
    }

    // US-6 · "the item's area is a classification path": a new item takes the root whatever its team; a child takes its parent's
    // area. (BR-OT-10: the area is not the team's; the two are independent.)
    [Fact]
    public async Task NewItem_TakesRoot_ChildTakesParentsArea()
    {
        var world = await CreateWorldAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, world.ProjectId, world.Items.RootAreaId, "Отчёты");
        var parent = await OwningTeamTestData.CreateAsync(factory, world.Items, "US", "Родитель");
        Assert.Equal(world.Items.RootAreaId, (await StoredAsync(factory, world.ProjectId, parent.Identifier)).AreaId);
        Assert.Equal(HttpStatusCode.OK, (await WorkItemsTestData.SaveAsync(factory, world.Items, parent.Identifier, new { areaId = reports.Id.ToString() })).Status);

        var child = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new
            {
                requestId = Guid.NewGuid().ToString(),
                typeId = world.Items.Types["TASK"].Id.ToString(),
                title = "Дочерняя",
                parentId = parent.Id.ToString(),
            },
            OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.Created, child.Status);
        Assert.Equal(reports.Id, child.Body.GetProperty("area").GetProperty("id").GetGuid());
    }

    // US-6 · "an item's own area changes no team": a save that moves a child to another area leaves its team, whoever owned
    // the area it left and the one it entered. Replaces Save_AreaRenamedMovedAndItsTeamDeleted's team half; mod-017 BR-OT-10.
    [Fact]
    public async Task Save_ChildArea_ChangesNoTeam()
    {
        var world = await CreateWorldAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, world.ProjectId, world.Items.RootAreaId, "Отчёты");
        var item = await OwningTeamTestData.CreateAsync(factory, world.Items, "TASK", "Задача");
        await SetOwningTeamAsync(factory, item.Id, world.Venus);

        var answer = await WorkItemsTestData.SaveAsync(factory, world.Items, item.Identifier, new { areaId = reports.Id.ToString() });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(world.Venus, TeamOf(answer.Body)!.Value.Id);
        var stored = await StoredAsync(factory, world.ProjectId, item.Identifier);
        Assert.Equal((reports.Id, world.Venus), (stored.AreaId, stored.TeamId));
    }

    // mod-017 US-1 · "a person moving moves no item": adding a person to a team, and removing them, change no item — its owning team,
    // its assignee and its «Изменён» are what they were. (BR-OT-1: the team of an item is its own, not the team's members'.)
    [Fact]
    public async Task Membership_Moves_MoveNoItem()
    {
        var world = await CreateWorldAsync(factory);
        var item = await OwningTeamTestData.CreateAsync(factory, world.Items, "TASK", "Задача");
        await SetOwningTeamAsync(factory, item.Id, world.Mars);
        var member = await MembershipTestData.NewNamedMemberAsync(
            factory, world.OrganizationId, Mustask.Domain.Identity.OrganizationRole.Member, "Вера", "Новикова");
        var before = await StoredAsync(factory, world.ProjectId, item.Identifier);
        var members = $"{Project(world)}/teams/{world.Mars}/members";

        var added = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, members, new { userId = member.Id }, OrganizationsTokens.SignedIn());
        var afterAdd = await StoredAsync(factory, world.ProjectId, item.Identifier);
        var removed = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Delete, $"{members}/{Uri.EscapeDataString(member.Id)}", token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.Created, added.Status);
        Assert.Equal(HttpStatusCode.NoContent, removed.Status);
        Assert.Equal(before, afterAdd);
        Assert.Equal(before, await StoredAsync(factory, world.ProjectId, item.Identifier));
    }

    // US-6 · "no answer names a team default area": deleting an area does not mention a team, whatever its name (BR-OT-10).
    [Fact]
    public async Task Delete_NoAnswerNamesATeamDefault()
    {
        var world = await CreateWorldAsync(factory);
        var leaf = await OrganizationsTestData.AddAreaAsync(factory, world.ProjectId, world.Items.RootAreaId, "MARS");

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Delete, $"{Project(world)}/areas/{leaf.Id}", token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Null(answer.ErrorCode);
    }
}
