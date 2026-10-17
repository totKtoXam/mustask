using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/backlogs/{level}</c> (feat-039 api-contract § 3.1):
/// a project's backlog of one level over every item of the project, whatever the team — US-1. The project's teams are
/// the world's first team «Мастаск» (the root area) and «Платформа», which stand in for US-1's MARS and VENUS.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetProjectBacklogEndpointTests(MustaskApiFactory factory)
{
    /// <summary>US-1's data: S1 and S2 stand on «Мастаск»'s backlog, S3 on «Платформа»'s; nothing has been reordered.</summary>
    private async Task<(BoardsWorld World, Item S1, Item S2, Item S3)> ProjectAsync()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var s1 = await BoardsTestData.CreateAsync(factory, world, "US", "Первая", world.RootAreaId);
        var s2 = await BoardsTestData.CreateAsync(factory, world, "US", "Вторая", world.RootAreaId);
        var s3 = await BoardsTestData.CreateAsync(factory, world, "US", "Третья", world.PlatformAreaId);

        return (world, s1, s2, s3);
    }

    private static string[] Ids(params Item[] items) => items.Select(item => item.Identifier).ToArray();

    // --- Contents ---------------------------------------------------------------------------------------------------

    // US-1 · "A project's backlog holds every item of the project, from every team" (BR-MX-17, BR-MX-16, BR-PB-2): S1, S2, S3
    // with «Порядок» 1..3 and nothing of another project; each team's backlog still holds its own; no item twice.
    [Fact]
    public async Task Get_HoldsEveryItemOfTheProject_FromEveryTeam_AndNothingOfAnotherProject()
    {
        var (world, s1, s2, s3) = await ProjectAsync();
        var other = await WorkItemsTestData.AddProjectAsync(factory, world.Items, "OTH", "Другой проект");
        Assert.Equal(HttpStatusCode.Created, (await WorkItemsTestData.CreateAsync(factory, other, "US", "Чужая история")).Status);

        var rows = await ProjectBoardsTestData.RowsAsync(factory, world);

        Assert.Equal(Ids(s1, s2, s3), rows);
        Assert.Equal(rows.Length, rows.Distinct().Count());
        Assert.Equal(Ids(s1, s2), await BoardsTestData.RowsAsync(factory, world, world.ProjectTeamId));
        Assert.Equal(Ids(s3), await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // US-1 · same: an item in an area beneath a team's area (the inherited owner) is on the project's backlog too, and so is one
    // in the root, whoever the owner.
    [Fact]
    public async Task Get_AreasWithAndWithoutAnExplicitOwner_AllOfTheProjectCount()
    {
        var (world, s1, s2, s3) = await ProjectAsync();
        var inApi = await BoardsTestData.CreateAsync(factory, world, "US", "В API", world.ApiAreaId);
        var inReports = await BoardsTestData.CreateAsync(factory, world, "US", "В отчётах", world.ReportsAreaId);

        Assert.Equal(Ids(s1, s2, s3, inApi, inReports), await ProjectBoardsTestData.RowsAsync(factory, world));
    }

    // US-1 · "A view holds its level's items in every state but Removed" (BR-MX-17, mod-006 BR-M4-23): a closed story stays;
    // the «Фичи» backlog holds the feature alone and «Эпики» the epic alone; a story in the Removed category is on no level.
    [Fact]
    public async Task Get_HoldsTheLevelsItemsInEveryStateButRemoved()
    {
        var (world, s1, s2, s3) = await ProjectAsync();
        var closed = await BoardsTestData.CreateAsync(factory, world, "US", "Закрытая", world.PlatformAreaId);
        await BoardsTestData.SetStateAsync(factory, world, closed, "US", "Закрыта");
        var removed = await BoardsTestData.CreateAsync(factory, world, "US", "Удалённая");
        await LinksTestData.DeleteAsync(factory, world.Items, removed);
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича", world.PlatformAreaId);
        var epic = await BoardsTestData.CreateAsync(factory, world, "EPIC", "Эпик");
        var task = await BoardsTestData.CreateAsync(factory, world, "TASK", "Задача", world.PlatformAreaId);

        Assert.Equal(Ids(s1, s2, s3, closed), await ProjectBoardsTestData.RowsAsync(factory, world));
        Assert.Equal(Ids(feature), await ProjectBoardsTestData.RowsAsync(factory, world, "features"));
        Assert.Equal(Ids(epic), await ProjectBoardsTestData.RowsAsync(factory, world, "epics"));

        foreach (var level in new[] { "epics", "features", "stories" })
        {
            var rows = await ProjectBoardsTestData.RowsAsync(factory, world, level);
            Assert.DoesNotContain(removed.Identifier, rows);
            Assert.DoesNotContain(task.Identifier, rows);
        }
    }

    // US-1 · "Children stand beneath their parent whatever the team" (feat-027 BR-T-4..-6): T1 on «Мастаск»'s surfaces and T2 on
    // «Платформа»'s both stand beneath S1, B1 beneath S3 and not among the top-level rows; «Порядок» numbers the top-level rows
    // only; «Мастаск»'s own backlog still nests only T1 beneath S1.
    [Fact]
    public async Task Get_ChildrenStandBeneathTheirParent_WhateverTheirTeam()
    {
        var (world, s1, s2, s3) = await ProjectAsync();
        var t1 = await BoardsTestData.CreateAsync(factory, world, "TASK", "Первая задача", world.RootAreaId);
        var t2 = await BoardsTestData.CreateAsync(factory, world, "TASK", "Вторая задача", world.PlatformAreaId);
        var b1 = await BoardsTestData.CreateAsync(factory, world, "BUG", "Баг", world.PlatformAreaId);
        await LinksTestData.LinkAsync(factory, world.Items, t1, s1);
        await LinksTestData.LinkAsync(factory, world.Items, t2, s1);
        await LinksTestData.LinkAsync(factory, world.Items, b1, s3);

        Assert.Equal($"{s1.Identifier}[{t1.Identifier} {t2.Identifier}] {s2.Identifier} {s3.Identifier}[{b1.Identifier}]", await ProjectBoardsTestData.ShapeAsync(factory, world));
        Assert.Equal(Ids(s1, s2, s3), await ProjectBoardsTestData.RowsAsync(factory, world));
        Assert.Equal($"{s1.Identifier}[{t1.Identifier}] {s2.Identifier}", await BacklogTreeTestData.ShapeAsync(factory, world, world.ProjectTeamId, "stories"));
        Assert.Equal($"{s3.Identifier}[{b1.Identifier}]", await BacklogTreeTestData.ShapeAsync(factory, world, world.PlatformId, "stories"));
    }

    // US-1 · same: a feature of another team under a feature stands beneath it only, and a story of a higher parent is a top-level
    // row of its own level; the «Эпики» page nests the whole chain with its tasks (BR-PB-3).
    [Fact]
    public async Task Get_ASameLevelChildOfAnotherTeam_StandsBeneathItsParentOnly_AndEpicsNestTheChain()
    {
        var (world, s1, _, _) = await ProjectAsync();
        var epic = await BoardsTestData.CreateAsync(factory, world, "EPIC", "Эпик");
        var parent = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Родитель");
        var child = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Дочерняя", world.PlatformAreaId);
        var task = await BoardsTestData.CreateAsync(factory, world, "TASK", "Задача", world.PlatformAreaId);
        await LinksTestData.LinkAsync(factory, world.Items, parent, epic);
        await LinksTestData.LinkAsync(factory, world.Items, child, parent);
        await LinksTestData.LinkAsync(factory, world.Items, s1, child);
        await LinksTestData.LinkAsync(factory, world.Items, task, s1);

        Assert.Equal(
            $"{parent.Identifier}[{child.Identifier}[{s1.Identifier}[{task.Identifier}]]]",
            await ProjectBoardsTestData.ShapeAsync(factory, world, "features"));
        Assert.Equal(
            $"{epic.Identifier}[{parent.Identifier}[{child.Identifier}[{s1.Identifier}[{task.Identifier}]]]]",
            await ProjectBoardsTestData.ShapeAsync(factory, world, "epics"));
        Assert.Equal(Ids(parent), await ProjectBoardsTestData.RowsAsync(factory, world, "features"));
    }

    // US-1 · "The head": the answer names the project and the level; there is no team in it.
    [Fact]
    public async Task Get_AnswersTheProjectAndTheLevel_AndNoTeam()
    {
        var (world, s1, _, _) = await ProjectAsync();

        var answer = await ProjectBoardsTestData.GetBacklogAsync(factory, world);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var project = answer.Body.GetProperty("project");
        Assert.Equal((world.ProjectId, "Мастаск"), (project.GetProperty("id").GetGuid(), project.GetProperty("name").GetString()));
        var level = answer.Body.GetProperty("level");
        Assert.Equal(("stories", "Истории"), (level.GetProperty("code").GetString(), level.GetProperty("name").GetString()));
        Assert.Equal(["US", "BUG"], level.GetProperty("types").EnumerateArray().Select(type => type.GetProperty("key").GetString()));
        Assert.False(answer.Body.TryGetProperty("team", out _));
        var row = answer.Body.GetProperty("items")[0];
        Assert.Equal(s1.Identifier, row.GetProperty("identifier").GetString());
        Assert.Equal(1, row.GetProperty("order").GetInt32());
        Assert.True(row.TryGetProperty("priority", out _));
    }

    // US-1 · "The rows are drawn as the team backlog draws them": the row carries the planning values and the parent as feat-027 has them.
    [Fact]
    public async Task Get_RowsCarryTheParentAndThePlanningValues()
    {
        var (world, _, _, _) = await ProjectAsync();
        var epic = await BoardsTestData.CreateAsync(factory, world, "EPIC", "Эпик");
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича", world.PlatformAreaId);
        await LinksTestData.LinkAsync(factory, world.Items, feature, epic);

        var answer = await ProjectBoardsTestData.GetBacklogAsync(factory, world, "epics");

        var row = answer.Body.GetProperty("items")[0];
        var child = row.GetProperty("children")[0];
        Assert.Equal(feature.Identifier, child.GetProperty("identifier").GetString());
        Assert.Equal(epic.Identifier, BacklogTreeTestData.ParentOf(child));
        Assert.False(child.TryGetProperty("order", out _));
        Assert.True(row.TryGetProperty("effort", out _));
        Assert.True(row.TryGetProperty("businessValue", out _));
        Assert.True(row.TryGetProperty("valueArea", out _));
    }

    // US-1 · "An empty project backlog": an empty list, not an error.
    [Fact]
    public async Task Get_NoItemAtTheLevel_IsAnEmptyList()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);

        var answer = await ProjectBoardsTestData.GetBacklogAsync(factory, world);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(0, answer.Body.GetProperty("items").GetArrayLength());
    }

    // --- The order --------------------------------------------------------------------------------------------------

    // US-2 · "New and unplaced items follow the placed ones" (BR-PB-4, BR-MX-18): S3 placed first by a reorder; S6 (created 10:00),
    // S7 and S8 (both 10:05, S7 with the lower number) follow; S9, created last by number but earliest by time, stands first among
    // the unplaced. Two reads agree.
    [Fact]
    public async Task Get_UnplacedItems_FollowThePlacedOnes_ByCreationTimeThenNumber()
    {
        var (world, s1, s2, s3) = await ProjectAsync();

        // The reorder places every item the backlog holds at that moment: S3, S1, S2. The four created afterwards are unplaced.
        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Top(s3));
        var s6 = await BoardsTestData.CreateAsync(factory, world, "US", "Шестая");
        var s7 = await BoardsTestData.CreateAsync(factory, world, "US", "Седьмая");
        var s8 = await BoardsTestData.CreateAsync(factory, world, "US", "Восьмая", world.PlatformAreaId);
        var s9 = await BoardsTestData.CreateAsync(factory, world, "US", "Девятая");
        var ten = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, s6, ten);
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, s7, ten.AddMinutes(5));
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, s8, ten.AddMinutes(5));
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, s9, ten.AddHours(-3));

        var first = await ProjectBoardsTestData.RowsAsync(factory, world);
        var second = await ProjectBoardsTestData.RowsAsync(factory, world);

        Assert.Equal(Ids(s3, s1, s2, s9, s6, s7, s8), first);
        Assert.Equal(first, second);
    }

    // US-2 · same, on a backlog that has never been reordered: every item is unplaced, and the order is creation time then number,
    // not the number alone (BR-MX-18) — S3, created earliest, leads although it has the highest number.
    [Fact]
    public async Task Get_NeverReordered_ListsByCreationTimeThenNumber()
    {
        var (world, s1, s2, s3) = await ProjectAsync();
        var ten = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, s1, ten.AddMinutes(5));
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, s2, ten.AddMinutes(5));
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, s3, ten);

        Assert.Equal(Ids(s3, s1, s2), await ProjectBoardsTestData.RowsAsync(factory, world));
        Assert.Equal(Ids(s3, s1, s2), await ProjectBoardsTestData.RowsAsync(factory, world));
    }

    // feat-027 BR-T-6 on the project: the «Задачи» level has no project order, so tasks beneath a story stand by number even when
    // their creation times say otherwise.
    [Fact]
    public async Task Get_TasksBeneathAStory_StandByNumber_NotByCreationTime()
    {
        var (world, s1, _, _) = await ProjectAsync();
        var first = await BoardsTestData.CreateAsync(factory, world, "TASK", "Первая");
        var second = await BoardsTestData.CreateAsync(factory, world, "TASK", "Вторая");
        await LinksTestData.LinkAsync(factory, world.Items, first, s1);
        await LinksTestData.LinkAsync(factory, world.Items, second, s1);
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, first, new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, second, new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero));

        var shape = await ProjectBoardsTestData.ShapeAsync(factory, world);

        Assert.StartsWith($"{s1.Identifier}[{first.Identifier} {second.Identifier}]", shape);
    }

    // --- Who reads, and what is not found ---------------------------------------------------------------------------

    // US-1 · "Who reads" (BR-PB-5): a viewer reads the same rows as a member; a person outside the organization gets the
    // organization's not-found and no row; no token, 401.
    [Fact]
    public async Task Get_ByRole_ViewerReadsTheSameRows_OutsiderAndAnonymousAreRefused()
    {
        var (world, s1, s2, s3) = await ProjectAsync();
        var viewer = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);

        var asViewer = await ProjectBoardsTestData.GetBacklogAsync(factory, world, token: viewer.Token);
        var asMember = await ProjectBoardsTestData.GetBacklogAsync(factory, world, token: member.Token);
        var asOutsider = await ProjectBoardsTestData.GetBacklogAsync(factory, world, token: MembershipTestData.Outsider());
        var anonymous = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, ProjectBoardsTestData.Backlog(world, "stories"));

        Assert.Equal(HttpStatusCode.OK, asViewer.Status);
        Assert.Equal(asMember.Body.GetRawText(), asViewer.Body.GetRawText());
        Assert.Equal(Ids(s1, s2, s3), asViewer.Body.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("identifier").GetString()));
        BoardsTestData.AssertRefused(asOutsider, HttpStatusCode.NotFound, "organizations.organization.not_found");
        Assert.Equal("Организация не найдена.", asOutsider.Detail);
        Assert.False(asOutsider.Body.TryGetProperty("items", out _));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
        Assert.Equal("http.unauthorized", anonymous.ErrorCode);
    }

    // US-1 · "A missing address is told from the outside in" (BR-MX-1, mod-006 api-contract § 0.3): the project before the level,
    // and no team can be missing; a level the process lacks, or the tasks and sprints segments, answer the level's code.
    [Theory]
    [InlineData("organization-unknown", "organizations.organization.not_found", "Организация не найдена.")]
    [InlineData("project-malformed", "organizations.project.not_found", "Проект не найден.")]
    [InlineData("project-unknown", "organizations.project.not_found", "Проект не найден.")]
    [InlineData("project-of-another-organization", "organizations.project.not_found", "Проект не найден.")]
    [InlineData("project-deactivated", "organizations.project.not_found", "Проект не найден.")]
    [InlineData("project-and-level-both-wrong", "organizations.project.not_found", "Проект не найден.")]
    [InlineData("level-tasks", "boards.level.not_found", "Уровень бэклога не найден.")]
    [InlineData("level-sprints", "boards.level.not_found", "Уровень бэклога не найден.")]
    [InlineData("level-capitalized", "boards.level.not_found", "Уровень бэклога не найден.")]
    public async Task Get_ContextNotFound_AnswersItsLevelsCode(string part, string code, string detail)
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, await NotFoundAddressAsync(factory, world, part, "backlogs"), token: OrganizationsTokens.SignedIn());

        BoardsTestData.AssertRefused(answer, HttpStatusCode.NotFound, code);
        Assert.Equal(detail, answer.Detail);
    }

    /// <summary>The address of US-1's missing-address outline, for <c>backlogs</c> or <c>boards</c> (no team in it).</summary>
    internal static async Task<string> NotFoundAddressAsync(MustaskApiFactory factory, BoardsWorld world, string part, string surface)
    {
        string Address(object organization, object project, string level) =>
            $"/api/organizations/{organization}/projects/{project}/{surface}/{level}";

        switch (part)
        {
            case "organization-unknown":
                return Address(Guid.CreateVersion7(), world.ProjectId, "stories");
            case "project-malformed":
                return Address(world.OrganizationId, "not-a-project", "stories");
            case "project-unknown":
                return Address(world.OrganizationId, Guid.CreateVersion7(), "stories");
            case "project-of-another-organization":
                var foreign = await BoardsTestData.CreateWorldAsync(factory);
                return Address(world.OrganizationId, foreign.ProjectId, "stories");
            case "project-deactivated":
                await OrganizationsTestData.SetProjectStatusAsync(factory, world.OrganizationId, world.ProjectId, ProjectStatus.Deactivated);
                return Address(world.OrganizationId, world.ProjectId, "stories");
            case "project-and-level-both-wrong":
                return Address(world.OrganizationId, Guid.CreateVersion7(), "tasks");
            case "level-tasks":
                return Address(world.OrganizationId, world.ProjectId, "tasks");
            case "level-sprints":
                return Address(world.OrganizationId, world.ProjectId, "sprints");
            case "level-capitalized":
                return Address(world.OrganizationId, world.ProjectId, "Stories");
            default:
                throw new ArgumentOutOfRangeException(nameof(part), part, "Unknown part.");
        }
    }

    // ADR-0026 / US-2 · "A deactivated project keeps its order": the backlog answers «Проект не найден.» while the project is
    // deactivated and reads the same rows, in the same order, once it is restored.
    [Fact]
    public async Task Get_DeactivatedProject_IsNotFound_AndBackWithItsOrderOnRestore()
    {
        var (world, s1, s2, s3) = await ProjectAsync();
        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Top(s3));
        Assert.Equal(Ids(s3, s1, s2), await ProjectBoardsTestData.RowsAsync(factory, world));

        await OrganizationsTestData.SetProjectStatusAsync(factory, world.OrganizationId, world.ProjectId, ProjectStatus.Deactivated);
        BoardsTestData.AssertRefused(await ProjectBoardsTestData.GetBacklogAsync(factory, world), HttpStatusCode.NotFound, "organizations.project.not_found");

        await OrganizationsTestData.SetProjectStatusAsync(factory, world.OrganizationId, world.ProjectId, ProjectStatus.Active);
        Assert.Equal(Ids(s3, s1, s2), await ProjectBoardsTestData.RowsAsync(factory, world));
    }

    // BR-PB-8 · "Team views are unchanged": a read of the project's backlog writes nothing — no place of either order, no item.
    [Fact]
    public async Task Get_WritesNothing()
    {
        var (world, _, _, _) = await ProjectAsync();
        var storedBefore = await WorkItemsTestData.StoredAsync(factory, world.ProjectId);

        await ProjectBoardsTestData.GetBacklogAsync(factory, world);
        await ProjectBoardsTestData.GetBoardAsync(factory, world);

        Assert.Empty(await ProjectBoardsTestData.PlacesAsync(factory, world));
        Assert.Empty(await ProjectBoardsTestData.TeamPlacesAsync(factory, world));
        Assert.Equal(storedBefore, await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
    }
}
