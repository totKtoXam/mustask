using System.Net;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// mod-016 US-2 · "Containment" (ADR-0027, plan-backend § 3.5 "Resolution chains", § 4.2 item 7): the scoped check admits a caller
/// to the organization of the <b>address</b>; every nested id — a project, a team, an area, a work item, and an id in a body — must
/// then lie inside that organization (and inside the project the path carries). The test fixes one user who administers both A and
/// B, and addresses A with an id that belongs to B: for every row of <see cref="OrganizationAddressedRoutes.All"/> that carries the
/// nested id, the answer is its level's «…not found» (api-contract § 0.1 row 4) and nothing of B changes. A handler that does not
/// hold its link fails here, and the check cannot be fooled by a caller with the right in the wrong organization.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class OrganizationContainmentTests(MustaskApiFactory factory)
{
    /// <summary>The ids of one organization's structure, as an address takes them.</summary>
    private sealed record Side(WorkItemsWorld World, Guid TeamId, Guid AreaId, string Identifier, Guid ItemId)
    {
        public string OrganizationId => World.OrganizationId.ToString();

        public string ProjectId => World.ProjectId.ToString();
    }

    private sealed record TwoOrganizations(string Token, Side A, Side B);

    private static readonly IReadOnlyDictionary<string, string> NotFoundOf = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["project"] = OrganizationErrorCodes.ProjectNotFound,
        ["team"] = OrganizationErrorCodes.TeamNotFound,
        ["area"] = OrganizationErrorCodes.AreaNotFound,
        ["item"] = WorkItemErrorCodes.WorkItemNotFound,
    };

    private static readonly IReadOnlyDictionary<string, string> Placeholder = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["project"] = "{projectId}",
        ["team"] = "{teamId}",
        ["area"] = "{areaId}",
        ["item"] = "{identifier}",
    };

    public static TheoryData<string, string, string> NestedIds()
    {
        var data = new TheoryData<string, string, string>();

        foreach (var route in OrganizationAddressedRoutes.All)
        {
            foreach (var (kind, placeholder) in Placeholder)
            {
                if (route.Template.Contains(placeholder, StringComparison.Ordinal))
                {
                    data.Add(route.Method, route.Template, kind);
                }
            }
        }

        return data;
    }

    /// <summary>
    /// An organization's structure; <paramref name="fillers"/> items are created first so that the two organizations' items never
    /// share an identifier (both projects are keyed MSK): B's item is MSK-US-2 and A has no item numbered 2.
    /// </summary>
    private async Task<Side> BuildAsync(int fillers)
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var team = await OrganizationsTestData.AddTeamAsync(factory, world.ProjectId, "Платформа");
        var area = await OrganizationsTestData.AddAreaAsync(factory, world.OrganizationId, null, "Платформа");

        for (var filler = 0; filler < fillers; filler++)
        {
            await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", $"Заполнитель {filler}");
        }

        var item = await WorkItemsTestData.CreateAsync(factory, world, "US", "Чужая история");
        Assert.Equal(HttpStatusCode.Created, item.Status);

        // mod-017: the item has an owning team and the project a named default team, so a refused request that changed either
        // shows in the snapshot of the state (an unowned item and an unnamed setting would hide a write of none).
        await OwningTeamTestData.SetOwningTeamAsync(factory, item.Body.GetProperty("id").GetGuid(), team.Id);
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE projects SET default_team_id = {team.Id} WHERE id = {world.ProjectId}");

        return new Side(world, team.Id, area.Id, item.Body.GetProperty("identifier").GetString()!, item.Body.GetProperty("id").GetGuid());
    }

    /// <summary>One user who administers both organizations; A and B each hold a project, a second team, an area and a work item.</summary>
    private async Task<TwoOrganizations> TwoAsync()
    {
        var a = await BuildAsync(fillers: 0);
        var b = await BuildAsync(fillers: 1);
        var user = MembershipTestData.NewUserId();
        await MembershipTestData.AddMemberAsync(factory, a.World.OrganizationId, user, OrganizationRole.Admin);
        await MembershipTestData.AddMemberAsync(factory, b.World.OrganizationId, user, OrganizationRole.Admin);

        return new TwoOrganizations(OrganizationsTokens.SignedIn(user), a, b);
    }

    /// <summary>The route's address in A, with the nested id of <paramref name="kind"/> taken from B and every other from A.</summary>
    private static string AddressOf(string template, TwoOrganizations world, string kind) =>
        template
            .Replace("{organizationId}", world.A.OrganizationId, StringComparison.Ordinal)
            .Replace("{projectId}", kind == "project" ? world.B.ProjectId : world.A.ProjectId, StringComparison.Ordinal)
            .Replace("{teamId}", (kind == "team" ? world.B.TeamId : world.A.TeamId).ToString(), StringComparison.Ordinal)
            .Replace("{areaId}", (kind == "area" ? world.B.AreaId : world.A.AreaId).ToString(), StringComparison.Ordinal)
            .Replace("{identifier}", kind == "item" ? world.B.Identifier : world.A.Identifier, StringComparison.Ordinal)
            .Replace("{userId}", "u-" + Guid.CreateVersion7().ToString("N"), StringComparison.Ordinal)
            .Replace("{level}", "stories", StringComparison.Ordinal);

    // US-2 · "the resource's organization decides": a path id of B inside A's address is refused as an id that does not exist, at
    // its own level — on every route that carries one.
    [Theory]
    [MemberData(nameof(NestedIds))]
    public async Task APathIdOfAnotherOrganization_IsRefusedAsNotFound_AtItsOwnLevel(string method, string template, string kind)
    {
        var world = await TwoAsync();

        var answer = await OrganizationsApi.SendAsync(
            factory, new HttpMethod(method), AddressOf(template, world, kind), OrganizationAddressedRoutes.Body(method), world.Token);

        Assert.True(
            answer.Status == HttpStatusCode.NotFound && answer.ErrorCode == NotFoundOf[kind],
            $"{method} {template} with the {kind} of another organization answered {(int)answer.Status} {answer.ErrorCode}, not {NotFoundOf[kind]}.");
    }

    // The refused requests change nothing of the other organization: its project, teams, areas, items, the items' owning teams, the
    // project's default team and its name are as they were.
    [Fact]
    public async Task TheRefusedRequests_ChangeNothingOfTheOtherOrganization()
    {
        var world = await TwoAsync();
        var projectBefore = await StateAsync(world.B);

        foreach (var route in OrganizationAddressedRoutes.All)
        {
            foreach (var (kind, placeholder) in Placeholder)
            {
                if (!route.Template.Contains(placeholder, StringComparison.Ordinal) || route.AcceptsDeactivated)
                {
                    continue;
                }

                await OrganizationsApi.SendAsync(
                    factory, new HttpMethod(route.Method), AddressOf(route.Template, world, kind), OrganizationAddressedRoutes.Body(route.Method), world.Token);
            }
        }

        Assert.Equal(projectBefore, await StateAsync(world.B));
    }

    private async Task<string> StateAsync(Side side)
    {
        using var scope = factory.Services.CreateScope();
        var structure = scope.ServiceProvider.GetRequiredService<IOrganizationStructure>();
        var organization = await structure.FindOrganizationAsync(side.World.OrganizationId, CancellationToken.None);
        var project = await structure.FindProjectAsync(side.World.OrganizationId, side.World.ProjectId, CancellationToken.None);
        var team = await structure.FindTeamAsync(side.World.OrganizationId, side.TeamId, CancellationToken.None);
        var tree = await OrganizationsTestData.ReadTreeAsync(factory, side.World.OrganizationId);
        var items = await WorkItemsTestData.StoredAsync(factory, side.World.ProjectId);
        var teams = await OwningTeamTestData.TeamStateAsync(factory, side.World.ProjectId);

        // feat-028: the links of the project and of the team — who made them and when — and the organization's teams are part of the state.
        var projectLinks = (await TeamTestData.LinksOfProjectAsync(factory, side.World.ProjectId)).Select(link => $"{link.TeamId} {link.LinkedBy} {link.LinkedAt:O}");
        var teamLinks = (await TeamTestData.LinksOfTeamAsync(factory, side.TeamId)).Select(link => $"{link.ProjectId} {link.LinkedBy} {link.LinkedAt:O}");
        var organizationTeams = (await TeamTestData.StoredTeamsAsync(factory, side.World.OrganizationId))
            .OrderBy(stored => stored.Id)
            .Select(stored => $"{stored.Id} {stored.Name} {stored.Status} {stored.Description}");
        var memberships = (await TeamTestData.TeamMemberIdsAsync(factory, side.TeamId));

        return string.Join(
            " | ",
            organization!.Name,
            organization.Status,
            project!.Name,
            project.IsReachable,
            team!.Name,
            team.Status,
            string.Join("; ", tree.PreOrder()),
            string.Join("; ", items.Select(item => $"{item.Number} {item.Title} {item.StateId} {item.ChangedAt:O}")),
            teams,
            string.Join("; ", projectLinks),
            string.Join("; ", teamLinks),
            string.Join("; ", organizationTeams),
            string.Join("; ", memberships));
    }

    // feat-028 US-8 · "a foreign id answers as one that names nothing" (the ten rows, bytes compared): an administrator of A and of B names B's
    // team as an unlink's destination, as a project's link, as a project's initial team; B's project as a team's link and as a new team's
    // project; a person who belongs to B only as a new team's member. Every answer is the answer for an id that does not exist, status,
    // code and text alike, and nothing of B changes. (The owner and the default are in the test above and in the one below.)
    [Fact]
    public async Task AForeignIdInAnOrganizationLevelBody_AnswersExactlyAsAnUnknownOne_NothingChanges()
    {
        var world = await TwoAsync();
        var a = world.A;
        var stranger = await MembershipTestData.NewNamedMemberAsync(factory, world.B.World.OrganizationId, OrganizationRole.Member, "Олег", "Из Б");
        var project = $"/api/organizations/{a.OrganizationId}/projects/{a.ProjectId}";
        var organization = $"/api/organizations/{a.OrganizationId}";
        var before = await StateAsync(world.B);
        var aBefore = await StateAsync(a);
        var unknown = Guid.CreateVersion7().ToString();

        async Task<Answer> SendAsync(HttpMethod method, string path, object body) =>
            await OrganizationsApi.SendAsync(factory, method, path, body, world.Token);

        var cases = new (string Name, Answer Foreign, Answer Unknown)[]
        {
            (
                "an unlink's destination",
                await SendAsync(HttpMethod.Post, $"{project}/teams/{a.TeamId}/unlink", new { destination = new { teamId = world.B.TeamId.ToString() } }),
                await SendAsync(HttpMethod.Post, $"{project}/teams/{a.TeamId}/unlink", new { destination = new { teamId = unknown } })),
            (
                "a project's link",
                await SendAsync(HttpMethod.Post, $"{project}/teams", new { teamIds = new[] { world.B.TeamId.ToString() } }),
                await SendAsync(HttpMethod.Post, $"{project}/teams", new { teamIds = new[] { unknown } })),
            (
                "a new project's team",
                await SendAsync(HttpMethod.Post, $"{organization}/projects", new { name = "Проект с чужой командой", key = "FRGN", teamIds = new[] { world.B.TeamId.ToString() } }),
                await SendAsync(HttpMethod.Post, $"{organization}/projects", new { name = "Проект с чужой командой", key = "FRGN", teamIds = new[] { unknown } })),
            (
                "a team's link",
                await SendAsync(HttpMethod.Post, $"{organization}/teams/{a.TeamId}/projects", new { projectIds = new[] { world.B.World.ProjectId.ToString() } }),
                await SendAsync(HttpMethod.Post, $"{organization}/teams/{a.TeamId}/projects", new { projectIds = new[] { unknown } })),
            (
                "a new team's project",
                await SendAsync(HttpMethod.Post, $"{organization}/teams", new { name = "Команда с чужим проектом", projectIds = new[] { world.B.World.ProjectId.ToString() } }),
                await SendAsync(HttpMethod.Post, $"{organization}/teams", new { name = "Команда с чужим проектом", projectIds = new[] { unknown } })),
            (
                "a new team's member",
                await SendAsync(HttpMethod.Post, $"{organization}/teams", new { name = "Команда с чужим человеком", memberIds = new[] { stranger.Id } }),
                await SendAsync(HttpMethod.Post, $"{organization}/teams", new { name = "Команда с чужим человеком", memberIds = new[] { "nobody-" + Guid.CreateVersion7().ToString("N") } })),
        };

        foreach (var (name, foreign, unnamed) in cases)
        {
            Assert.True(
                foreign.Status is HttpStatusCode.NotFound or HttpStatusCode.Conflict,
                $"{name}: a foreign id answered {(int)foreign.Status} {foreign.ErrorCode}.");
            Assert.Equal((unnamed.Status, unnamed.ErrorCode, unnamed.Detail), (foreign.Status, foreign.ErrorCode, foreign.Detail));
        }

        Assert.Equal(before, await StateAsync(world.B));
        Assert.Equal(aBefore, await StateAsync(a));
    }

    // US-2 · "An area's or a team's body id outside the address is refused" (mod-002 BR-M1-22, mod-017 BR-OT-16): an administrator
    // of A and of B moves an area of A under an area of B, creates one under an area of B, and names a team of B the default team
    // of the project of A — refused as for an id that does not exist, and nothing changes. The owner and default-area requests of
    // mod-002 are gone (BR-OT-10); `PUT …/default-team` takes their place. Rewritten (D-16): the test keeps its name.
    [Fact]
    public async Task AnAreaOrTeamIdOfAnotherOrganizationInABody_IsRefusedAsNotFound_NothingChanges()
    {
        var world = await TwoAsync();
        var a = world.A;
        var project = $"/api/organizations/{a.OrganizationId}/projects/{a.ProjectId}";
        var treeBefore = (await OrganizationsTestData.ReadTreeAsync(factory, a.World.OrganizationId)).PreOrder().ToList();
        var teamsBefore = await OwningTeamTestData.TeamStateAsync(factory, a.World.ProjectId);

        var parent = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Put, $"/api/organizations/{a.OrganizationId}/areas/{a.AreaId}/parent", new { parentId = world.B.AreaId.ToString() }, world.Token);
        var created = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            $"/api/organizations/{a.OrganizationId}/areas",
            new { parentId = world.B.AreaId.ToString(), name = OrganizationsTestData.UniqueName("Область") },
            world.Token);
        var defaultTeam = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Put, $"{project}/default-team", new { teamId = world.B.TeamId.ToString() }, world.Token);
        var unknownDefaultTeam = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Put, $"{project}/default-team", new { teamId = Guid.CreateVersion7().ToString() }, world.Token);

        Assert.Equal((HttpStatusCode.NotFound, OrganizationErrorCodes.AreaNotFound), (parent.Status, parent.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, OrganizationErrorCodes.AreaNotFound), (created.Status, created.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, OrganizationErrorCodes.TeamNotFound), (defaultTeam.Status, defaultTeam.ErrorCode));
        // mod-017 BR-OT-16: B's team answers exactly as an id that names nothing, status and body alike.
        Assert.Equal((unknownDefaultTeam.Status, unknownDefaultTeam.ErrorCode, unknownDefaultTeam.Detail), (defaultTeam.Status, defaultTeam.ErrorCode, defaultTeam.Detail));
        Assert.Equal(treeBefore, (await OrganizationsTestData.ReadTreeAsync(factory, a.World.OrganizationId)).PreOrder().ToList());
        Assert.Equal(teamsBefore, await OwningTeamTestData.TeamStateAsync(factory, a.World.ProjectId));
    }

    // US-2 · "A work item's body id outside the address is refused" (mod-003 BR-M3-23, feat-001 BR-H-1, mod-017 BR-OT-16): a member
    // of A and of B saves a work item of A with a team or a parent of B (the item has no area since mod-018: an areaId is ignored), or creates one in A for a team of B or beneath a
    // parent of B — refused as for an id that does not exist, and nothing changes.
    [Fact]
    public async Task AWorkItemBodyIdOfAnotherOrganization_IsRefusedAsNotFound_NothingChanges()
    {
        var world = await TwoAsync();
        var a = world.A;
        var own = await WorkItemsTestData.CreateItemAsync(factory, a.World, "TASK", "Своя задача");
        var storedBefore = await WorkItemsTestData.StoredAsync(factory, a.World.ProjectId);
        var teamsBefore = await OwningTeamTestData.TeamStateAsync(factory, a.World.ProjectId);
        var typeId = a.World.Types["TASK"].Id.ToString();

        var saveParent = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Patch, WorkItemsTestData.Item(a.World, own), new { parent = new { parentId = world.B.ItemId.ToString() } }, world.Token);
        var setParent = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Put, $"{WorkItemsTestData.Item(a.World, own)}/parent", new { parentId = world.B.ItemId.ToString() }, world.Token);
        var createFromTeam = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(a.World),
            new { requestId = Guid.NewGuid().ToString(), typeId, title = "Из чужой команды", team = new { teamId = world.B.TeamId.ToString() } },
            world.Token);
        var saveTeam = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Patch, WorkItemsTestData.Item(a.World, own), new { team = new { teamId = world.B.TeamId.ToString() } }, world.Token);
        var unknownTeam = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Patch, WorkItemsTestData.Item(a.World, own), new { team = new { teamId = Guid.CreateVersion7().ToString() } }, world.Token);
        var createBeneath = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(a.World),
            new { requestId = Guid.NewGuid().ToString(), typeId, title = "Под чужим родителем", parentId = world.B.ItemId.ToString() },
            world.Token);

        Assert.Equal((HttpStatusCode.NotFound, WorkItemErrorCodes.ParentNotFound), (saveParent.Status, saveParent.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, WorkItemErrorCodes.ParentNotFound), (setParent.Status, setParent.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, OrganizationErrorCodes.TeamNotFound), (createFromTeam.Status, createFromTeam.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, OrganizationErrorCodes.TeamNotFound), (saveTeam.Status, saveTeam.ErrorCode));
        // mod-017 BR-OT-16: B's team answers exactly as an id that names nothing, status and body alike.
        Assert.Equal((unknownTeam.Status, unknownTeam.ErrorCode, unknownTeam.Detail), (saveTeam.Status, saveTeam.ErrorCode, saveTeam.Detail));
        Assert.Equal((HttpStatusCode.NotFound, WorkItemErrorCodes.ParentNotFound), (createBeneath.Status, createBeneath.ErrorCode));
        Assert.Equal(storedBefore, await WorkItemsTestData.StoredAsync(factory, a.World.ProjectId));
        Assert.Equal(teamsBefore, await OwningTeamTestData.TeamStateAsync(factory, a.World.ProjectId));
    }
}
