using System.Net;
using System.Text.Json;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/deactivate</c> (api-contract
/// § 1.6) through the real host and a real PostgreSQL. Every test builds its
/// own organization (<see cref="OrganizationsTestData"/>): the database is
/// shared and never emptied, so no test asserts the full content of a global
/// list. The organization level of the not-found theories (malformed, unknown,
/// deactivated, a non-member, on every route) is the scoped check's
/// <c>ScopedAccessTests</c> (mod-016 ADR-0027); here the deactivated case is the
/// route's own second call.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DeactivateOrganizationEndpointTests(MustaskApiFactory factory)
{
    private const string NotFoundCode = "organizations.organization.not_found";

    private Task<Answer> DeactivateAsync(Guid organizationId, string? token = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            $"{OrganizationsApi.Organization(organizationId)}/deactivate",
            token: token ?? OrganizationsTokens.SignedIn(),
            gate: gate);

    private Task<Answer> GetAsync(string path) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: OrganizationsTokens.SignedIn());

    // US-2 · "The organization is deactivated": 204 with no body, it leaves the active organizations and is among the
    // deactivated ones (BR-M1-24). The token carries no right: the caller is the creator, the administrator, who holds
    // organizations.organization.deactivate (mod-016 BR-M1-7, D-13).
    [Fact]
    public async Task Deactivate_Returns204_LeavesActiveList_JoinsDeactivatedList()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var other = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await DeactivateAsync(organization.Id);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Null(answer.MediaType);
        Assert.Equal(JsonValueKind.Undefined, answer.Body.ValueKind);

        var active = await ListAsync("/api/organizations");
        var deactivated = await ListAsync("/api/organizations/deactivated");
        Assert.DoesNotContain(active, item => item.Id == organization.Id);
        Assert.Contains((organization.Id, organization.Name), deactivated);
        Assert.Contains(active, item => item.Id == other.Id);
        Assert.DoesNotContain(deactivated, item => item.Id == other.Id);
    }

    // US-2 · "The organization is deactivated": opening its address, or the address of its project or of anything in that
    // project, answers «Организация не найдена» (BR-M1-20); the answer the next request meets, not the filter's fixture.
    [Fact]
    public async Task Deactivate_ThenEverythingBeneath_AnswersOrganizationNotFound()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = OrganizationsApi.Organization(structure.OrganizationId);
        var project = OrganizationsApi.Project(structure.OrganizationId, structure.ProjectId);
        Assert.Equal(HttpStatusCode.NoContent, (await DeactivateAsync(structure.OrganizationId)).Status);

        foreach (var path in new[]
        {
            organization,
            $"{organization}/projects",
            $"{organization}/projects/deactivated",
            project,
            $"{project}/teams",
            $"{organization}/teams",
            $"{organization}/teams/{structure.Platform.Id}",
            $"{project}/areas",
        })
        {
            var answer = await GetAsync(path);

            Assert.True(answer.Status == HttpStatusCode.NotFound, $"GET {path} answered {answer.Status}.");
            Assert.Equal(NotFoundCode, answer.ErrorCode);
            Assert.Equal("Организация не найдена.", answer.Detail);
        }
    }

    // US-2 · "And nothing under it is deleted" (BR-M1-20, ADR-0026 rule 2): the project, team, area, default, owner and
    // process rows beneath are exactly as they were; a project deactivated on its own stays so, and nothing else gains a status.
    [Fact]
    public async Task Deactivate_DeletesNothing_RowsBeneathUnchanged()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var old = await OrganizationsTestData.CreateProjectAsync(
            factory, structure.OrganizationId, "Старый проект", "OLD", ProjectStatus.Deactivated);
        await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        await OrganizationsTestData.SetDefaultTeamAsync(factory, structure.OrganizationId, structure.ProjectId, structure.Platform.Id);
        await OrganizationsTestData.AddAreaAsync(factory, old.Project.Id, old.Root.Id, "Архив");
        var before = await BeneathAsync(structure.OrganizationId);

        var answer = await DeactivateAsync(structure.OrganizationId);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        var after = await BeneathAsync(structure.OrganizationId);
        Assert.Equal(before, after);
        Assert.Equal(2, after.Count(row => row.StartsWith("project ", StringComparison.Ordinal)));
        Assert.Contains(after, row => row.StartsWith($"project {structure.ProjectId} Active ", StringComparison.Ordinal));
        Assert.Contains(after, row => row.StartsWith($"project {old.Project.Id} Deactivated ", StringComparison.Ordinal));
        Assert.Equal(3, after.Count(row => row.StartsWith("team ", StringComparison.Ordinal)));
        Assert.DoesNotContain(after, row => row.StartsWith("team ", StringComparison.Ordinal) && row.Contains("Deleted", StringComparison.Ordinal));
        var process = Assert.Single(after, row => row.StartsWith("process ", StringComparison.Ordinal));
        Assert.DoesNotContain("levels=0 ", process, StringComparison.Ordinal);
    }

    // US-1 · "A visitor who is not signed in sees nothing": the challenge comes first, and the organization stays active.
    [Fact]
    public async Task Deactivate_NoToken_Returns401_ChangesNothing()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{OrganizationsApi.Organization(organization.Id)}/deactivate");

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal(OrganizationStatus.Active, await StatusOfAsync(organization.Id));
    }

    // US-2 · "A deactivated organization takes no change ... deactivates ... anything under it" and api-contract § 1.6
    // ("already deactivated included"): the second call answers not found, and the organization stays deactivated.
    [Fact]
    public async Task Deactivate_AlreadyDeactivated_Returns404OrganizationNotFound_StaysDeactivated()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        Assert.Equal(HttpStatusCode.NoContent, (await DeactivateAsync(organization.Id)).Status);

        var again = await DeactivateAsync(organization.Id);

        Assert.Equal(HttpStatusCode.NotFound, again.Status);
        Assert.Equal(NotFoundCode, again.ErrorCode);
        Assert.Equal("Организация не найдена.", again.Detail);
        Assert.Equal(OrganizationStatus.Deactivated, await StatusOfAsync(organization.Id));
    }

    // US-2 · "A deactivated organization is listed separately, with no purge": the row, its name and its process stay, and
    // another module reads it with its status (api-contract § 7, ADR-0026 rule 4).
    [Fact]
    public async Task Deactivate_KeepsTheOrganization_SeamReadsItWithItsStatus()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        await DeactivateAsync(organization.Id);

        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IOrganizationStructure>()
            .FindOrganizationAsync(organization.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal((organization.Name, OrganizationStatus.Deactivated, organization.ProcessId), (stored.Name, stored.Status, stored.ProcessId));
    }

    // plan-backend § 8.2: the organization is locked FOR NO KEY UPDATE and read after the lock. Request A is held at its
    // commit with the row written; B passes the filter (A is not committed), blocks on the row lock, and once A commits it
    // reads the organization deactivated and answers not found, so exactly one deactivation happened.
    [Fact]
    public async Task Deactivate_TwoAtTheSameMoment_OneDeactivates_OtherAnswersNotFound()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        using var gate = factory.CommitGates.Open();

        var first = DeactivateAsync(organization.Id, gate: gate);
        await Task.WhenAny(gate.Reached, first);
        Assert.False(first.IsCompleted, "The first deactivation finished before reaching its commit gate.");

        var second = DeactivateAsync(organization.Id);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "The second deactivation did not wait for the first one's lock.");

        gate.Release();
        var (a, b) = (await first, await second);

        Assert.Equal(HttpStatusCode.NoContent, a.Status);
        Assert.Equal(HttpStatusCode.NotFound, b.Status);
        Assert.Equal(NotFoundCode, b.ErrorCode);
        Assert.Equal(OrganizationStatus.Deactivated, await StatusOfAsync(organization.Id));
    }

    private async Task<IReadOnlyList<(Guid Id, string Name)>> ListAsync(string path)
    {
        var answer = await GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, answer.Status);

        return answer.Body.EnumerateArray()
            .Select(item => (Id: item.GetProperty("id").GetGuid(), Name: item.GetProperty("name").GetString()!))
            .ToList();
    }

    private async Task<OrganizationStatus> StatusOfAsync(Guid organizationId)
    {
        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IOrganizationStructure>()
            .FindOrganizationAsync(organizationId, CancellationToken.None);

        return stored!.Status;
    }

    /// <summary>
    /// Everything beneath the organization, as stored now and whatever its status: every project with its status
    /// and default-team setting, every team with its status, every area with its parent, and the process with its levels, types and states.
    /// </summary>
    private async Task<IReadOnlyList<string>> BeneathAsync(Guid organizationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        var projects = await db.Projects.AsNoTracking().Where(project => project.OrganizationId == organizationId).OrderBy(project => project.Id).ToListAsync();
        var projectIds = projects.Select(project => project.Id).ToList();
        var teams = await db.Teams.AsNoTracking().Where(team => team.OrganizationId == organizationId).OrderBy(team => team.Id).ToListAsync();
        var links = await db.ProjectTeams.AsNoTracking().Where(link => link.OrganizationId == organizationId).OrderBy(link => link.ProjectId).ThenBy(link => link.TeamId).ToListAsync();
        var areas = await db.Areas.AsNoTracking().Where(area => area.ProjectId != null && projectIds.Contains(area.ProjectId.Value)).OrderBy(area => area.Id).ToListAsync();
        var processes = await db.Processes
            .AsNoTracking()
            .Include(process => process.BacklogLevels)
            .Include(process => process.WorkItemTypes).ThenInclude(type => type.States)
            .Where(process => process.OrganizationId == organizationId)
            .ToListAsync();

        var rows = new List<string>();
        rows.AddRange(projects.Select(project => $"project {project.Id} {project.Status} {project.Name} {project.Key} default={project.DefaultTeamId}"));
        rows.AddRange(teams.Select(team => $"team {team.Id} {team.Status} {team.Name} {team.Description}"));
        rows.AddRange(links.Select(link => $"link {link.ProjectId} {link.TeamId} {link.LinkedBy} {link.LinkedAt:O}"));
        rows.AddRange(areas.Select(area => $"area {area.Id} {area.Name} parent={area.ParentId}"));

        rows.AddRange(processes.Select(process =>
            $"process {process.Id.Value} levels={process.BacklogLevels.Count} types={process.WorkItemTypes.Count} states={process.WorkItemTypes.Sum(type => type.States.Count)}"));

        return rows;
    }
}
