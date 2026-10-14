using System.Net;
using System.Text.Json;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}</c>
/// (api-contract § 3.6, US-7) through the real host and a real PostgreSQL. Every
/// test builds its own organization (<see cref="OrganizationsTestData"/>): the
/// database is shared and never emptied. Reads of other lanes' endpoints (the team
/// list, the team, the rename, the default area, the area owner) stand in for
/// "from now on every request answers not found"; they answer once those lanes'
/// handlers are in.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DeleteTeamEndpointTests(MustaskApiFactory factory)
{
    private const string TeamNotFoundCode = "organizations.team.not_found";

    private Task<Answer> DeleteAsync(
        Guid organizationId, string projectId, string teamId, string? token = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Delete,
            $"{OrganizationsApi.Organization(organizationId)}/projects/{Uri.EscapeDataString(projectId)}/teams/{Uri.EscapeDataString(teamId)}",
            token: token ?? OrganizationsTokens.SignedIn(),
            gate: gate);

    private Task<Answer> DeleteAsync(OrganizationStructure structure, Guid teamId, CommitGate? gate = null) =>
        DeleteAsync(structure.OrganizationId, structure.ProjectId.ToString(), teamId.ToString(), gate: gate);

    private Task<Answer> GetAsync(string path) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: OrganizationsTokens.SignedIn());

    private Task<Answer> ReownAsync(OrganizationStructure structure, Guid areaId, Guid ownerTeamId, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Areas(structure.OrganizationId, structure.ProjectId)}/{areaId}/owner",
            new { ownerTeamId = ownerTeamId.ToString() },
            OrganizationsTokens.SignedIn(),
            gate);

    private async Task<TeamInfo> StoredTeamAsync(Guid teamId)
    {
        using var scope = factory.Services.CreateScope();
        var team = await scope.ServiceProvider.GetRequiredService<IOrganizationStructure>().FindTeamAsync(teamId, CancellationToken.None);

        return team ?? throw new InvalidOperationException($"The team {teamId} was never stored.");
    }

    private static Dictionary<Guid, (Guid? ParentId, string Name, string Path)> Places(AreaTree tree) =>
        tree.PreOrder().ToDictionary(node => node.Id, node => (node.ParentId, node.Name, tree.PathOf(node.Id)));

    // US-7 · "A team is deleted after a confirmation, and its areas pass to the first team": «Платформа» owns the areas
    // «Платформа» and «Платформа / API» explicitly and «Платформа / API / Шлюз» by inheritance. They pass to the first team
    // «Пример проекта» with their names and places, and «Шлюз» resolves to it by inheritance (BR-M1-18). The areas of
    // another team are not touched, and nothing is removed.
    [Fact]
    public async Task Delete_ReownsExplicitAreasToFirstTeam_InheritedFollow_NamesAndPlacesKept()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var platform = structure.Platform;
        var api = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, platform.Area.Id, "API", platform.Team.Id);
        var gateway = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, api.Id, "Шлюз");
        var data = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Данные");
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты", data.Team.Id);
        var before = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);

        var answer = await DeleteAsync(structure, platform.Team.Id);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Null(answer.MediaType);
        Assert.Equal(JsonValueKind.Undefined, answer.Body.ValueKind);

        var after = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        var firstTeamId = structure.FirstTeamId;
        Assert.Equal(firstTeamId, after[platform.Area.Id].OwnerTeamId);
        Assert.Equal(firstTeamId, after[api.Id].OwnerTeamId);
        Assert.Null(after[gateway.Id].OwnerTeamId);
        Assert.Equal(new ResolvedOwner(firstTeamId, false), after.ResolveOwner(platform.Area.Id));
        Assert.Equal(new ResolvedOwner(firstTeamId, true), after.ResolveOwner(gateway.Id));
        Assert.Equal("Платформа / API / Шлюз", after.PathOf(gateway.Id));
        Assert.Equal(Places(before), Places(after));

        // The deleted team's default area loses the mark, the first team's default stays the root, and no area is left behind.
        Assert.Null(after[platform.Area.Id].DefaultOfTeamId);
        Assert.Equal(firstTeamId, after.Root.DefaultOfTeamId);
        Assert.DoesNotContain(after.PreOrder(), node => node.OwnerTeamId == platform.Team.Id || node.DefaultOfTeamId == platform.Team.Id);

        // The other team keeps its area, its explicit area and its default.
        Assert.Equal(data.Team.Id, after[reports.Id].OwnerTeamId);
        Assert.Equal(data.Team.Id, after[data.Area.Id].OwnerTeamId);
        Assert.Equal(data.Team.Id, after[data.Area.Id].DefaultOfTeamId);
    }

    // US-7 · "A team is deleted ... no picker offers it": «Платформа» is no longer among the project's teams, the first team
    // stays first, and the other team is still listed.
    [Fact]
    public async Task Delete_TeamLeavesTheList()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var data = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Данные");
        var teamsPath = OrganizationsApi.Teams(structure.OrganizationId, structure.ProjectId);

        var listed = await GetAsync(teamsPath);
        var answer = await DeleteAsync(structure, structure.Platform.Team.Id);
        var remaining = await GetAsync(teamsPath);

        Assert.Equal(HttpStatusCode.OK, listed.Status);
        Assert.Equal(
            new[] { structure.FirstTeamId, data.Team.Id, structure.Platform.Team.Id }.Order(),
            listed.Body.EnumerateArray().Select(team => team.GetProperty("id").GetGuid()).Order());
        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(HttpStatusCode.OK, remaining.Status);
        Assert.Equal(
            [structure.FirstTeamId, data.Team.Id],
            remaining.Body.EnumerateArray().Select(team => team.GetProperty("id").GetGuid()));
    }

    // US-7 · "The deleted team's name is free, but its area's name is not": the team's name is free among the active teams,
    // and the area «Платформа» is still a child of the root, so the name stays taken at that level (BR-M1-9). The add itself
    // is AddTeam's lane.
    [Fact]
    public async Task Delete_FreesTheTeamName_TheAreaKeepsItsOwn()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await DeleteAsync(structure, structure.Platform.Team.Id);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);

        using var scope = factory.Services.CreateScope();
        var teams = scope.ServiceProvider.GetRequiredService<ITeamRepository>();
        var areas = scope.ServiceProvider.GetRequiredService<IAreaRepository>();
        var platform = OrganizationsTestData.Name("Платформа");
        Assert.False(await teams.IsActiveNameTakenAsync(structure.ProjectId, platform.Normalized, exceptId: null, CancellationToken.None));
        Assert.True(await areas.IsSiblingNameTakenAsync(structure.RootId, platform.Normalized, exceptId: null, CancellationToken.None));
    }

    // US-7 · "The first team cannot be deleted": a direct request is refused with «Нельзя удалить команду, которой принадлежит
    // корневая область проекта», and nothing changes.
    [Fact]
    public async Task Delete_FirstTeam_Returns409FirstTeamCannotBeDeleted_ChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var before = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);

        var answer = await DeleteAsync(structure, structure.FirstTeamId);

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.team.first_team_cannot_be_deleted", answer.ErrorCode);
        Assert.Equal("Нельзя удалить команду, которой принадлежит корневая область проекта", answer.Detail);

        var first = await StoredTeamAsync(structure.FirstTeamId);
        Assert.Equal(TeamStatus.Active, first.Status);
        Assert.Equal(structure.RootId, first.DefaultAreaId);
        Assert.Equal(Places(before), Places(await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)));
        Assert.Equal(TeamStatus.Active, (await StoredTeamAsync(structure.Platform.Team.Id)).Status);
    }

    // US-1 · "A visitor who is not signed in sees nothing".
    [Fact]
    public async Task Delete_NoToken_Returns401_DeletesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Delete,
            $"{OrganizationsApi.Teams(structure.OrganizationId, structure.ProjectId)}/{structure.Platform.Team.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal(TeamStatus.Active, (await StoredTeamAsync(structure.Platform.Team.Id)).Status);
    }

    // US-3 · "A project id names no project of the organization" (BR-M1-20, BR-M1-22): malformed, unknown, or of another
    // organization, the project level answers, and no team is deleted, the other organization's included.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task Delete_ProjectNotOfOrganization_Returns404ProjectNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var (projectId, teamId) = how switch
        {
            "malformed" => ("PRM", structure.Platform.Team.Id),
            "unknown" => (Guid.CreateVersion7().ToString(), structure.Platform.Team.Id),
            _ => (other.ProjectId.ToString(), other.Platform.Team.Id),
        };

        var answer = await DeleteAsync(structure.OrganizationId, projectId, teamId.ToString());

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal("Проект не найден.", answer.Detail);
        Assert.Equal(TeamStatus.Active, (await StoredTeamAsync(structure.Platform.Team.Id)).Status);
        Assert.Equal(TeamStatus.Active, (await StoredTeamAsync(other.Platform.Team.Id)).Status);
    }

    // US-3 · "A project is deactivated": a write under it answers project.not_found and changes nothing (ADR-0026 rule 5).
    [Fact]
    public async Task Delete_UnderDeactivatedProject_Returns404AndChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);

        var answer = await DeleteAsync(structure, structure.Platform.Team.Id);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal(TeamStatus.Active, (await StoredTeamAsync(structure.Platform.Team.Id)).Status);
        Assert.Equal(structure.Platform.Team.Id, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId))[structure.Platform.Area.Id].OwnerTeamId);
    }

    // US-4 · "A team id names no team of the project" (BR-M1-22) and US-7 · "A deleted team answers not found": malformed,
    // unknown, a team of another project (its first team included: the address answers before the first-team rule,
    // api-contract § 0.4), a team of another organization, or one already deleted. Nothing changes.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("first team of another project")]
    [InlineData("team of another project")]
    [InlineData("team of another organization")]
    [InlineData("deleted")]
    public async Task Delete_TeamNotAnActiveTeamOfTheProject_Returns404TeamNotFound_ChangesNothing(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var secondProject = await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");
        var secondTeam = await OrganizationsTestData.AddTeamAsync(factory, secondProject.Project.Id, "Ядро");
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var deleted = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Старая команда");
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, deleted.Team.Id);
        var teamId = how switch
        {
            "malformed" => structure.Platform.Team.Id.ToString("N"),
            "unknown" => Guid.CreateVersion7().ToString(),
            "first team of another project" => secondProject.FirstTeam.Id.ToString(),
            "team of another project" => secondTeam.Team.Id.ToString(),
            "team of another organization" => other.Platform.Team.Id.ToString(),
            _ => deleted.Team.Id.ToString(),
        };
        var before = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);

        var answer = await DeleteAsync(structure.OrganizationId, structure.ProjectId.ToString(), teamId);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal(TeamNotFoundCode, answer.ErrorCode);
        Assert.Equal("Команда не найдена.", answer.Detail);
        Assert.Equal(TeamStatus.Active, (await StoredTeamAsync(structure.Platform.Team.Id)).Status);
        Assert.Equal(TeamStatus.Active, (await StoredTeamAsync(secondProject.FirstTeam.Id)).Status);
        Assert.Equal(TeamStatus.Active, (await StoredTeamAsync(secondTeam.Team.Id)).Status);
        Assert.Equal(TeamStatus.Active, (await StoredTeamAsync(other.Platform.Team.Id)).Status);
        Assert.Equal(Places(before), Places(await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)));
        Assert.Equal(before.PreOrder(), (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).PreOrder());
    }

    // US-7 · "A deleted team answers not found": «Платформа» is deleted, then its address is opened, a request renames it,
    // changes its default area, deletes it again or makes it an area's owner. Every answer is team.not_found, and nothing
    // changes: the name stays, and the area stays with the first team.
    [Theory]
    [InlineData("read")]
    [InlineData("rename")]
    [InlineData("default")]
    [InlineData("delete")]
    [InlineData("owner")]
    public async Task Delete_ThenEveryTeamOperationAndOwnerBinding_Returns404TeamNotFound(string operation)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var teamId = structure.Platform.Team.Id;
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты", structure.FirstTeamId);
        var teamPath = $"{OrganizationsApi.Teams(structure.OrganizationId, structure.ProjectId)}/{teamId}";
        var deletion = await DeleteAsync(structure, teamId);
        Assert.Equal(HttpStatusCode.NoContent, deletion.Status);

        var answer = operation switch
        {
            "read" => await GetAsync(teamPath),
            "rename" => await OrganizationsApi.SendAsync(factory, HttpMethod.Put, $"{teamPath}/name", new { name = "Новое имя" }, OrganizationsTokens.SignedIn()),
            "default" => await OrganizationsApi.SendAsync(factory, HttpMethod.Put, $"{teamPath}/default-area", new { areaId = reports.Id.ToString() }, OrganizationsTokens.SignedIn()),
            "delete" => await DeleteAsync(structure, teamId),
            _ => await ReownAsync(structure, reports.Id, teamId),
        };

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal(TeamNotFoundCode, answer.ErrorCode);
        Assert.Equal("Команда не найдена.", answer.Detail);

        var stored = await StoredTeamAsync(teamId);
        Assert.Equal("Платформа", stored.Name);
        Assert.Equal(TeamStatus.Deleted, stored.Status);
        Assert.Equal(structure.FirstTeamId, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId))[reports.Id].OwnerTeamId);
    }

    // US-7 · "Other modules read the deleted status": the list of active teams leaves «Платформа» out, and asking for it by its
    // identity says it is deleted, never active and never missing; its area now answers the first team as its owner (US-6
    // "other modules get the same answer as the tree").
    [Fact]
    public async Task Delete_Seam_FindTeamSaysDeleted_ListActiveTeamsOmitsIt()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var data = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Данные");
        var platform = structure.Platform;

        var answer = await DeleteAsync(structure, platform.Team.Id);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);

        using var scope = factory.Services.CreateScope();
        var seam = scope.ServiceProvider.GetRequiredService<IOrganizationStructure>();

        var deleted = await seam.FindTeamAsync(platform.Team.Id, CancellationToken.None);
        Assert.NotNull(deleted);
        Assert.Equal(TeamStatus.Deleted, deleted.Status);
        Assert.Null(deleted.DefaultAreaId);
        Assert.False(deleted.IsFirstTeam);
        Assert.False(deleted.IsReachable);
        Assert.Equal(structure.ProjectId, deleted.ProjectId);
        Assert.Equal(structure.OrganizationId, deleted.OrganizationId);

        var active = await seam.ListActiveTeamsAsync(structure.ProjectId, CancellationToken.None);
        Assert.Equal([structure.FirstTeamId, data.Team.Id], active.Select(team => team.Id));
        Assert.All(active, team => Assert.Equal(TeamStatus.Active, team.Status));

        var first = await seam.FindTeamAsync(structure.FirstTeamId, CancellationToken.None);
        Assert.Equal(structure.RootId, first!.DefaultAreaId);

        var owner = await seam.FindAreaOwnerAsync(platform.Area.Id, CancellationToken.None);
        Assert.Equal(structure.FirstTeamId, owner!.OwningTeamId);
        Assert.False(owner.IsInherited);
    }

    // US-7 · "A re-own to a team and that team's deletion at the same moment", the deletion first. The deletion is held at its
    // commit with the team locked FOR UPDATE; the re-own of «Отчёты» (owned by the first team) to «Платформа» blocks on that
    // lock, then finds the team deleted: one of the two is refused, and no area ends up owned by a deleted team
    // (ADR-0026 rule 8, plan-backend § 8.1). Needs the SetAreaOwner use case (lane L-24) at verification.
    [Fact]
    public async Task Delete_ConcurrentWithReownToIt_DeletionFirst_ReownRefused_NoAreaOwnedByDeletedTeam()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var teamId = structure.Platform.Team.Id;
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты", structure.FirstTeamId);
        using var gate = factory.CommitGates.Open();

        var deletion = DeleteAsync(structure, teamId, gate);
        await Task.WhenAny(gate.Reached, deletion);
        Assert.False(deletion.IsCompleted, "The deletion finished before reaching its commit gate.");

        var reown = ReownAsync(structure, reports.Id, teamId);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, reown);
        Assert.False(reown.IsCompleted, "The re-own did not wait for the deletion's lock on the team.");

        gate.Release();
        var (a, b) = (await deletion, await reown);

        Assert.Equal(HttpStatusCode.NoContent, a.Status);
        Assert.Equal(HttpStatusCode.NotFound, b.Status);
        Assert.Equal(TeamNotFoundCode, b.ErrorCode);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal(TeamStatus.Deleted, (await StoredTeamAsync(teamId)).Status);
        Assert.Equal(structure.FirstTeamId, tree[reports.Id].OwnerTeamId);
        Assert.DoesNotContain(tree.PreOrder(), node => node.OwnerTeamId == teamId || node.DefaultOfTeamId == teamId);
    }

    // US-7 · "A re-own to a team and that team's deletion at the same moment", the re-own first. The re-own is held at its
    // commit with the team locked FOR SHARE and the area written; the deletion blocks on the team, then re-owns every
    // explicit area, «Отчёты» included, to the first team: both succeed and no area ends up owned by a deleted team.
    // Needs the SetAreaOwner use case (lane L-24) at verification.
    [Fact]
    public async Task Delete_ConcurrentWithReownToIt_ReownFirst_BothSucceed_AreaEndsWithFirstTeam()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var teamId = structure.Platform.Team.Id;
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты", structure.FirstTeamId);
        using var gate = factory.CommitGates.Open();

        var reown = ReownAsync(structure, reports.Id, teamId, gate);
        await Task.WhenAny(gate.Reached, reown);
        Assert.False(reown.IsCompleted, "The re-own finished before reaching its commit gate.");

        var deletion = DeleteAsync(structure, teamId);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, deletion);
        Assert.False(deletion.IsCompleted, "The deletion did not wait for the re-own's lock on the team.");

        gate.Release();
        var (a, b) = (await reown, await deletion);

        Assert.Equal(HttpStatusCode.OK, a.Status);
        Assert.Equal(teamId, a.Body.GetProperty("ownerTeamId").GetGuid());
        Assert.Equal(HttpStatusCode.NoContent, b.Status);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal(TeamStatus.Deleted, (await StoredTeamAsync(teamId)).Status);
        Assert.Equal(structure.FirstTeamId, tree[reports.Id].OwnerTeamId);
        Assert.Equal(structure.FirstTeamId, tree[structure.Platform.Area.Id].OwnerTeamId);
        Assert.DoesNotContain(tree.PreOrder(), node => node.OwnerTeamId == teamId || node.DefaultOfTeamId == teamId);
    }

    // mod-016 US-3 · "A deleted team's memberships end" (mod-002 BR-M1-18, BR-M1-36): «Платформа» has two members, one of whom is
    // also in the first team. After the deletion the deleted team has no members; the first team keeps hers; both stay members of
    // the organization; and the deleted team's member list and page answer `team.not_found`.
    [Fact]
    public async Task Delete_EndsTheTeamsMemberships_TheOtherTeamKeepsHers_BothStayInTheOrganization()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory, creatorJoinsFirstTeam: false);
        var teamId = structure.Platform.Team.Id;
        var reader = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, Mustask.Domain.Identity.OrganizationRole.Viewer);
        var both = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, Mustask.Domain.Identity.OrganizationRole.Member);
        var onlyHere = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, Mustask.Domain.Identity.OrganizationRole.Member);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, teamId, both.Id);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, teamId, onlyHere.Id);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, structure.FirstTeamId, both.Id);

        var answer = await DeleteAsync(structure, teamId);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Empty(await MembershipTestData.TeamMemberIdsAsync(factory, teamId));
        Assert.Equal([both.Id], await MembershipTestData.TeamMemberIdsAsync(factory, structure.FirstTeamId));
        Assert.NotNull(await MembershipTestData.FindMembershipAsync(factory, structure.OrganizationId, both.Id));
        Assert.NotNull(await MembershipTestData.FindMembershipAsync(factory, structure.OrganizationId, onlyHere.Id));

        var teamPath = $"{OrganizationsApi.Teams(structure.OrganizationId, structure.ProjectId)}/{teamId}";
        var members = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{teamPath}/members", token: reader.Token);
        var page = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, teamPath, token: reader.Token);
        Assert.Equal((HttpStatusCode.NotFound, TeamNotFoundCode), (members.Status, members.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, TeamNotFoundCode), (page.Status, page.ErrorCode));
    }

    // mod-016 US-3: a team with no members is deleted as before — the memberships step finds nothing to end.
    [Fact]
    public async Task Delete_ATeamWithNoMembers_StillSucceeds()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory, creatorJoinsFirstTeam: false);
        var other = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, Mustask.Domain.Identity.OrganizationRole.Member);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, structure.FirstTeamId, other.Id);

        var answer = await DeleteAsync(structure, structure.Platform.Team.Id);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal([other.Id], await MembershipTestData.TeamMemberIdsAsync(factory, structure.FirstTeamId));
    }
}
