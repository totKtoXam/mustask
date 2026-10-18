using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Organizations;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// The data of feat-040's team views (spec § Stories, "the common cast"), built from the mod-017 world: the organization with
/// the active projects Альфа (ALP), Бета (BET), Гамма (GAM) and Дельта (DLT); the teams MARS (linked to the first three), VENUS
/// (Альфа only) and JUPITER (linked to Бета, which the world gives it, and unlinked here from nothing: it is the team the
/// stories use as "another team"); a team linked to no project (<see cref="Lonely"/>); and the other organization «Другая» with
/// its project and SATURN. Items are created through mod-003's create, so what a test reads is what the API wrote; their
/// creation times can be set with SQL (<see cref="ProjectBoardsTestData.SetCreatedAtAsync"/>).
/// </summary>
internal static class TeamAcrossProjectsTestData
{
    /// <summary>The cast: <see cref="Alpha"/>, <see cref="Beta"/>, <see cref="Gamma"/> and <see cref="Delta"/> are the views of the four projects.</summary>
    internal sealed record Cast(OwningTeamWorld World, BoardsWorld Alpha, BoardsWorld Beta, BoardsWorld Gamma, BoardsWorld Delta, Guid Lonely)
    {
        public Guid OrganizationId => World.OrganizationId;

        public Guid Mars => World.Mars;

        public Guid Venus => World.Venus;

        public Guid Jupiter => World.OtherProjectTeam;
    }

    /// <summary>MARS linked to Альфа, Бета and Гамма; VENUS to Альфа; Дельта linked to no team; a team linked to no project.</summary>
    public static async Task<Cast> CreateAsync(MustaskApiFactory factory)
    {
        var world = await CreateWorldAsync(factory);
        await OrganizationsTestData.LinkAsync(factory, world.OtherProjectId, world.Mars);

        var gamma = await OrganizationsTestData.CreateBareProjectAsync(factory, world.OrganizationId, "Гамма", "GAM");
        await OrganizationsTestData.LinkAsync(factory, gamma.Project.Id, world.Mars);
        var delta = await OrganizationsTestData.CreateBareProjectAsync(factory, world.OrganizationId, "Дельта", "DLT");
        var lonely = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, world.OrganizationId, "ЮПИТЕР-ОДИН");

        return new Cast(
            world,
            BoardsViewOf(world),
            ViewOf(world, world.OtherProjectId, "BET", Guid.Empty),
            ViewOf(world, gamma.Project.Id, "GAM", gamma.Root.Id),
            ViewOf(world, delta.Project.Id, "DLT", delta.Root.Id),
            lonely.Id);
    }

    /// <summary>
    /// The boards' view of one of the world's projects: the same organization and types, that project's id, key and root (the views
    /// read none, so «Бета»'s is empty).
    /// </summary>
    private static BoardsWorld ViewOf(OwningTeamWorld world, Guid projectId, string key, Guid rootId) =>
        new(world.Items with { ProjectId = projectId, RootAreaId = rootId, Key = key }, Guid.Empty, world.Mars, Guid.Empty, Guid.Empty, Guid.Empty);

    /// <summary>An item of <paramref name="typeKey"/> created in the view's project, owned by <paramref name="team"/> (or by none).</summary>
    public static async Task<Item> CreateAsync(
        MustaskApiFactory factory, BoardsWorld view, string title, Guid? team, string typeKey = "US", string? parentId = null)
    {
        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(view.Items),
            new
            {
                requestId = Guid.NewGuid().ToString(),
                typeId = view.Items.Types[typeKey].Id.ToString(),
                title,
                parentId,
                team = new { teamId = team?.ToString() },
            },
            OrganizationsTokens.SignedIn());
        Assert.True(answer.Status == HttpStatusCode.Created, $"The creation answered {answer.Status} {answer.ErrorCode}.");

        return new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
    }

    /// <summary>A story (see <see cref="CreateAsync(MustaskApiFactory, BoardsWorld, string, Guid?, string, string?)"/>).</summary>
    public static Task<Item> StoryAsync(MustaskApiFactory factory, BoardsWorld view, string title, Guid? team) =>
        CreateAsync(factory, view, title, team);

    /// <summary>The team's backlog (stories) or board rows' identifiers, with the filter's query string when given.</summary>
    public static async Task<string[]> RowsAsync(MustaskApiFactory factory, Cast cast, Guid teamId, string level = "stories", string? query = null, string? token = null)
    {
        var answer = await GetBacklogAsync(factory, cast, teamId, level, query, token);
        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var items = answer.Body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(Enumerable.Range(1, items.Count), items.Select(item => item.GetProperty("order").GetInt32()));

        return items.Select(item => item.GetProperty("identifier").GetString()!).ToArray();
    }

    /// <summary>Every card of the team's board in column order, then card order, by identifier.</summary>
    public static async Task<string[]> CardsAsync(MustaskApiFactory factory, Cast cast, Guid teamId, string level = "stories", string? query = null, string? token = null)
    {
        var answer = await GetBoardAsync(factory, cast, teamId, level, query, token);
        Assert.Equal(HttpStatusCode.OK, answer.Status);

        return answer.Body.GetProperty("columns").EnumerateArray()
            .SelectMany(column => column.GetProperty("items").EnumerateArray())
            .Select(card => card.GetProperty("identifier").GetString()!)
            .ToArray();
    }

    public static Task<Answer> GetBacklogAsync(MustaskApiFactory factory, Cast cast, Guid teamId, string level = "stories", string? query = null, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, $"{Team(cast, teamId)}/backlogs/{level}{(query is null ? string.Empty : "?" + query)}", token: token ?? OrganizationsTokens.SignedIn());

    public static Task<Answer> GetBoardAsync(MustaskApiFactory factory, Cast cast, Guid teamId, string level = "stories", string? query = null, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, $"{Team(cast, teamId)}/boards/{level}{(query is null ? string.Empty : "?" + query)}", token: token ?? OrganizationsTokens.SignedIn());

    public static Task<Answer> ReorderAsync(
        MustaskApiFactory factory, Cast cast, Guid teamId, object body, string level = "stories", CommitGate? gate = null, string? token = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{Team(cast, teamId)}/backlogs/{level}/reorder", body, token ?? OrganizationsTokens.SignedIn(), gate);

    public static string Team(Cast cast, Guid teamId) => TeamTestData.Team(cast.OrganizationId, teamId);

    /// <summary>The query string of the project filter: <c>projectId=…&amp;projectId=…</c>.</summary>
    public static string Filter(params object[] projects) => string.Join("&", projects.Select(project => $"projectId={project}"));

    /// <summary>Puts the backlog in exactly <paramref name="order"/> by moving each item to the bottom in turn (the team's reorder).</summary>
    public static async Task OrderAsync(MustaskApiFactory factory, Cast cast, Guid teamId, params Item[] order)
    {
        foreach (var item in order)
        {
            var answer = await ReorderAsync(factory, cast, teamId, BoardsTestData.Bottom(item));
            Assert.True(answer.Status == HttpStatusCode.NoContent, $"The reorder answered {answer.Status} {answer.ErrorCode}.");
        }
    }

    /// <summary>The <c>projects</c> of a team read, as <c>id name</c> pairs in the order received.</summary>
    public static string[] ProjectsOf(Answer answer) =>
        answer.Body.GetProperty("projects").EnumerateArray()
            .Select(project => $"{project.GetProperty("id").GetGuid()} {project.GetProperty("name").GetString()}")
            .ToArray();

    public static JsonElement Row(Answer backlog, string identifier) =>
        BacklogTreeTestData.AllRows(backlog.Body.GetProperty("items").EnumerateArray()).Single(row => row.GetProperty("identifier").GetString() == identifier);

    /// <summary>The ids of a project on the wire, <c>id name</c>.</summary>
    public static string ProjectOf(JsonElement rowOrCard) =>
        $"{rowOrCard.GetProperty("project").GetProperty("id").GetGuid()} {rowOrCard.GetProperty("project").GetProperty("name").GetString()}";

    /// <summary>The project's wire form, <c>id name</c>, for a project of the cast.</summary>
    public static string Wire(BoardsWorld view, string name) => $"{view.ProjectId} {name}";

    public static Task DeactivateAsync(MustaskApiFactory factory, Cast cast, BoardsWorld view) =>
        OrganizationsTestData.SetProjectStatusAsync(factory, cast.OrganizationId, view.ProjectId, ProjectStatus.Deactivated);

    public static Task RestoreAsync(MustaskApiFactory factory, Cast cast, BoardsWorld view) =>
        OrganizationsTestData.SetProjectStatusAsync(factory, cast.OrganizationId, view.ProjectId, ProjectStatus.Active);
}
