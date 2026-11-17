using System.Net;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using static Mustask.Api.IntegrationTests.Features.Boards.TeamAcrossProjectsTestData;
using static Mustask.Api.IntegrationTests.Features.Iterations.IterationsTestData;

namespace Mustask.Api.IntegrationTests.Features.Iterations;

/// <summary>
/// feat-062 · mod-007's owed sprint clauses (the sprint's counts and outcome read only items of projects the viewer sees) and
/// doc-016 BR-TD-17 on the body ids that reach a sprint. A team Member of MARS who cannot see «Гамма» reads and closes a sprint that
/// holds items of Альфа and of Гамма; every answer is equal in shape (<see cref="SightTwin"/>) to the one the same viewer of a twin
/// organization gets for a sprint that holds the Альфа items alone. The close moves the hidden items too, and says nothing of them.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SightInSprintsTests(MustaskApiFactory factory)
{
    private sealed record World(Cast Cast, string Viewer, Guid Closed, Guid Active, Guid Next, Item[] Hidden);

    /// <summary>
    /// A Closed sprint S0 (A1, with G1 of the hidden project), an Active one (A2, A3 and the two items S0 moved, with G2 and G3), a
    /// Planned one; the viewer holds the team's Member grant. Hidden items exist only when <paramref name="withHidden"/>.
    /// </summary>
    private async Task<World> BuildAsync(bool withHidden)
    {
        var cast = await CreateAsync(factory);
        var closed = await ActiveAsync(factory, cast.Alpha, cast.Mars, "S0", "2026-10-05", "2026-10-16");
        var active = await NewAsync(factory, cast.Alpha, cast.Mars, "S1", "2026-11-02", "2026-11-13");
        var next = await NewAsync(factory, cast.Alpha, cast.Mars, "S2");

        var a1 = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        Assert.Equal(HttpStatusCode.OK, (await SetSprintAsync(factory, cast.Alpha, a1, closed)).Status);
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_items SET assignee_id = 'visible-person', story_points = 3 WHERE id = {a1.Id}");

        var hidden = new List<Item>();

        if (withHidden)
        {
            var g1 = await StoryAsync(factory, cast.Gamma, "G1", cast.Mars);
            Assert.Equal(HttpStatusCode.OK, (await SetSprintAsync(factory, cast.Gamma, g1, closed)).Status);
            await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_items SET assignee_id = 'hidden-person', story_points = 8 WHERE id = {g1.Id}");
            hidden.Add(g1);
        }

        // The close of S0 moves its items into S1 (the sprint that is then started).
        var closing = await CloseAsync(factory, cast.Alpha, cast.Mars, closed, active);
        Assert.Equal(HttpStatusCode.OK, closing.Status);
        Assert.Equal(HttpStatusCode.OK, (await StartAsync(factory, cast.Alpha, cast.Mars, active)).Status);

        var a2 = await StoryAsync(factory, cast.Alpha, "A2", cast.Mars);
        Assert.Equal(HttpStatusCode.OK, (await SetSprintAsync(factory, cast.Alpha, a2, active)).Status);

        if (withHidden)
        {
            foreach (var title in new[] { "G2", "G3" })
            {
                var item = await StoryAsync(factory, cast.Gamma, title, cast.Mars);
                Assert.Equal(HttpStatusCode.OK, (await SetSprintAsync(factory, cast.Gamma, item, active)).Status);
                hidden.Add(item);
            }
        }

        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE projects SET visible_to_organization = false WHERE id = {cast.Gamma.ProjectId}");
        var viewer = await HolderAsync(factory, cast.Alpha, cast.Mars, GrantRole.Member, OrganizationRole.Member);

        return new World(cast, viewer.Token, closed, active, next, [.. hidden]);
    }

    private async Task<Dictionary<string, string>> ReadAllAsync(World world, string token)
    {
        var reads = new Dictionary<string, string>();
        var sprints = Sprints(world.Cast.Alpha, world.Cast.Mars);

        async Task Read(string name, string path) =>
            reads[name] = SightTwin.Shape(await SendAsync(factory, HttpMethod.Get, path, null, token));

        await Read("list", sprints);
        await Read("closed sprint", Sprint(world.Cast.Alpha, world.Cast.Mars, world.Closed));
        await Read("closed dynamics", Sprint(world.Cast.Alpha, world.Cast.Mars, world.Closed) + "/dynamics");
        await Read("closed participants", Sprint(world.Cast.Alpha, world.Cast.Mars, world.Closed) + "/participants");
        await Read("closed items", Sprint(world.Cast.Alpha, world.Cast.Mars, world.Closed) + "/items");
        await Read("active sprint", Sprint(world.Cast.Alpha, world.Cast.Mars, world.Active));
        await Read("close preview", Sprint(world.Cast.Alpha, world.Cast.Mars, world.Active) + "/close-preview");

        return reads;
    }

    private static void AssertEqualShapes(Dictionary<string, string> expected, Dictionary<string, string> actual)
    {
        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());

        foreach (var (surface, shape) in expected)
        {
            Assert.True(shape == actual[surface], $"{surface}:\nthe twin   {shape}\nthe viewer {actual[surface]}");
        }
    }

    // mod-007 · the counts, totals, outcome and close dialog of a viewer who cannot see Гамма equal those of the twin without it.
    [Fact]
    public async Task EverySprintRead_ForAViewerWhoCannotSeeAProject_IsIdenticalToTheTwinWithoutIt()
    {
        var real = await BuildAsync(withHidden: true);
        var twin = await BuildAsync(withHidden: false);

        var seen = await ReadAllAsync(real, real.Viewer);
        AssertEqualShapes(await ReadAllAsync(twin, twin.Viewer), seen);

        foreach (var shape in seen.Values)
        {
            Assert.DoesNotContain("Гамма", shape);
            Assert.DoesNotContain("GAM", shape);
            Assert.DoesNotContain("hidden-person", shape);
        }

        // The administrator's close preview counts the hidden items, so the narrowing is the viewer's.
        var asAdmin = await SendAsync(factory, HttpMethod.Get, Sprint(real.Cast.Alpha, real.Cast.Mars, real.Active) + "/close-preview");
        Assert.NotEqual(seen["close preview"], SightTwin.Shape(asAdmin));
    }

    // mod-007 · a close by the viewer moves the hidden items too and discloses nothing; the reads after it are still the twin's.
    [Fact]
    public async Task ClosingASprint_ByAViewerWhoCannotSeeAProject_MovesTheHiddenItemsToo_AndAnswersAsTheTwin()
    {
        var real = await BuildAsync(withHidden: true);
        var twin = await BuildAsync(withHidden: false);

        var closedReal = await CloseAsync(factory, real.Cast.Alpha, real.Cast.Mars, real.Active, real.Next, real.Viewer);
        var closedTwin = await CloseAsync(factory, twin.Cast.Alpha, twin.Cast.Mars, twin.Active, twin.Next, twin.Viewer);

        Assert.Equal(HttpStatusCode.OK, closedTwin.Status);
        Assert.Equal(SightTwin.Shape(closedTwin), SightTwin.Shape(closedReal));

        foreach (var item in real.Hidden)
        {
            Assert.Equal(real.Next, await StoredSprintAsync(factory, item));
        }

        AssertEqualShapes(await ReadAllAsync(twin, twin.Viewer), await ReadAllAsync(real, real.Viewer));
    }

    // mod-007 · the refusal to remove a sprint that holds items says the same whether or not a hidden item is among them.
    [Fact]
    public async Task RemovingASprint_HoldingHiddenItems_RefusesAsTheTwinDoes()
    {
        var real = await BuildAsync(withHidden: true);
        var twin = await BuildAsync(withHidden: false);

        var refusedReal = await SendAsync(factory, HttpMethod.Delete, Sprint(real.Cast.Alpha, real.Cast.Mars, real.Active), null, real.Viewer);
        var refusedTwin = await SendAsync(factory, HttpMethod.Delete, Sprint(twin.Cast.Alpha, twin.Cast.Mars, twin.Active), null, twin.Viewer);

        Assert.True(refusedTwin.Status != HttpStatusCode.NoContent, $"{refusedTwin.Status}");
        Assert.Equal(SightTwin.Refusal(refusedTwin), SightTwin.Refusal(refusedReal));
    }

    /// <summary>MARS with an item in «Альфа», a sprint of a team only the hidden project knows, and the team itself.</summary>
    private async Task<(World World, Item Item, Guid HiddenTeam, Guid HiddenTeamSprint, string Token)> HiddenTeamAsync()
    {
        var world = await BuildAsync(withHidden: false);
        var cast = world.Cast;
        var team = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, cast.OrganizationId, "ТАЙНАЯ");
        await OrganizationsTestData.LinkAsync(factory, cast.Gamma.ProjectId, team.Id);
        var sprint = await NewAsync(factory, cast.Gamma, team.Id, "Тайный");
        var item = await StoryAsync(factory, cast.Alpha, "Z1", cast.Mars);

        return (world, item, team.Id, sprint, world.Viewer);
    }

    // BR-TD-17 · the team in the body of a create or a save: a team the caller cannot see answers as one that does not exist.
    [Fact]
    public async Task ATeamInTheBodyOfACreateOrASave_ThatTheCallerCannotSee_AnswersAsAnUnknownOne_NotAs409()
    {
        var (world, item, hiddenTeam, _, _) = await HiddenTeamAsync();
        var cast = world.Cast;
        var member = await MembershipTestData.NewMemberAsync(factory, cast.OrganizationId, OrganizationRole.Member);

        Task<Answer> Create(Guid team) => OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(cast.Alpha.Items),
            new
            {
                requestId = Guid.NewGuid().ToString(),
                typeId = cast.Alpha.Items.Types["US"].Id.ToString(),
                title = "Новая",
                team = new { teamId = team.ToString() },
            },
            member.Token);

        Task<Answer> Save(Guid team) => OrganizationsApi.SendAsync(
            factory, HttpMethod.Patch, WorkItemsTestData.Item(cast.Alpha.Items, item.Identifier), new { team = new { teamId = team.ToString() } }, member.Token);

        var unknown = Guid.CreateVersion7();

        foreach (var send in new Func<Guid, Task<Answer>>[] { Create, Save })
        {
            var hidden = await send(hiddenTeam);
            var missing = await send(unknown);

            Assert.Equal(HttpStatusCode.NotFound, missing.Status);
            Assert.Equal(SightTwin.Refusal(missing), SightTwin.Refusal(hidden));
        }

        // The control: a team the caller sees that is not linked to the project is still the 409 of feat-028.
        var visibleUnlinked = await Create(cast.Jupiter);
        var visibleUnlinkedSave = await Save(cast.Jupiter);
        Assert.Equal(HttpStatusCode.Conflict, visibleUnlinked.Status);
        Assert.Equal(HttpStatusCode.Conflict, visibleUnlinkedSave.Status);
    }

    // BR-TD-17 · the sprint in the body of a save, and the destination of a close: the sprint of a team the caller cannot see is missing.
    [Fact]
    public async Task ASprintOfATeamTheCallerCannotSee_InTheBodyOfASaveOrAClose_AnswersAsAnUnknownOne()
    {
        var (world, item, _, hiddenSprint, token) = await HiddenTeamAsync();
        var cast = world.Cast;
        var manager = await HolderAsync(factory, cast.Alpha, cast.Mars, GrantRole.Manager, OrganizationRole.Member);
        var unknown = Guid.CreateVersion7();

        Task<Answer> Save(Guid sprint) => OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Patch,
            WorkItemsTestData.Item(cast.Alpha.Items, item.Identifier),
            new { sprint = new { sprintId = sprint.ToString() } },
            manager.Token);

        var savedHidden = await Save(hiddenSprint);
        var savedMissing = await Save(unknown);

        Assert.Equal(HttpStatusCode.NotFound, savedMissing.Status);
        Assert.Equal(SightTwin.Refusal(savedMissing), SightTwin.Refusal(savedHidden));

        var closedHidden = await CloseAsync(factory, cast.Alpha, cast.Mars, world.Active, hiddenSprint, manager.Token);
        var closedMissing = await CloseAsync(factory, cast.Alpha, cast.Mars, world.Active, unknown, manager.Token);

        Assert.NotEqual(HttpStatusCode.OK, closedMissing.Status);
        Assert.Equal(SightTwin.Refusal(closedMissing), SightTwin.Refusal(closedHidden));
        _ = token;
    }
}
