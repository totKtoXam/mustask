using System.Net;
using System.Text.Json;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/restore</c> (api-contract § 1.7)
/// through the real host and a real PostgreSQL: lane L-7's Api tests
/// (plan-backend § 13, § 14.3). Every test builds its own organization
/// (<see cref="OrganizationsTestData"/>): the database is shared and never
/// emptied. A deactivation is arranged through the data helpers, never through
/// another lane's endpoint, and the outcome is read back through the seam and the
/// read models of phase 1. Every request is made by a signed-in user with no
/// right at all, because until mod-016 any signed-in user may restore (BR-M1-19).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class RestoreOrganizationEndpointTests(MustaskApiFactory factory)
{
    private const string NotFoundCode = "organizations.organization.not_found";

    private Task<Answer> RestoreAsync(string organizationId) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            $"/api/organizations/{Uri.EscapeDataString(organizationId)}/restore",
            token: OrganizationsTokens.SignedIn());

    private Task<Answer> RestoreAsync(Guid organizationId) => RestoreAsync(organizationId.ToString());

    private Task<Answer> ReadProcessAsync(Guid organizationId) =>
        OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, $"/api/organizations/{organizationId}/process", token: OrganizationsTokens.SignedIn());

    private Task DeactivateAsync(Guid organizationId) =>
        OrganizationsTestData.SetOrganizationStatusAsync(factory, organizationId, OrganizationStatus.Deactivated);

    /// <summary>
    /// «Пример организации» with its project «Пример проекта», a second team «Платформа», and under the root «Отчёты»
    /// (owned by «Платформа») with «Квартальные» inheriting it: a tree with an explicit owner, an inherited one and two
    /// default areas.
    /// </summary>
    private async Task<(OrganizationStructure Structure, Area Reports, Area Quarterly)> CreateRichStructureAsync()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory, OrganizationsTestData.UniqueName("Пример организации"));
        var reports = await OrganizationsTestData.AddAreaAsync(
            factory, structure.ProjectId, structure.RootId, "Отчёты", structure.Platform.Team.Id);
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Квартальные");

        return (structure, reports, quarterly);
    }

    /// <summary>
    /// Everything the seam and the stored tree say about one organization's structure, the whole of it comparable as a
    /// value: statuses, names, process, reachability, the first team, the root, the default areas and the tree in pre-order.
    /// </summary>
    private sealed record Stored(
        OrganizationInfo Organization,
        ProjectInfo Project,
        TeamInfo FirstTeam,
        TeamInfo Platform,
        string Tree,
        ProjectInfo? Old,
        string? OldTree);

    private async Task<Stored> ReadAsync(OrganizationStructure structure, NewProject? old = null)
    {
        using var scope = factory.Services.CreateScope();
        var seam = scope.ServiceProvider.GetRequiredService<IOrganizationStructure>();
        var ct = CancellationToken.None;

        return new Stored(
            (await seam.FindOrganizationAsync(structure.OrganizationId, ct))!,
            (await seam.FindProjectAsync(structure.ProjectId, ct))!,
            (await seam.FindTeamAsync(structure.FirstTeamId, ct))!,
            (await seam.FindTeamAsync(structure.Platform.Team.Id, ct))!,
            await DescribeTreeAsync(structure.ProjectId),
            old is null ? null : await seam.FindProjectAsync(old.Project.Id, ct),
            old is null ? null : await DescribeTreeAsync(old.Project.Id));
    }

    private async Task<string> DescribeTreeAsync(Guid projectId) =>
        string.Join("; ", (await OrganizationsTestData.ReadTreeAsync(factory, projectId)).PreOrder());

    private async Task<IReadOnlyList<Guid>> ListedOrganizationsAsync(OrganizationStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IOrganizationReadModel>().ListAsync(status, CancellationToken.None);

        return rows.Select(row => row.Id).ToList();
    }

    private async Task<IReadOnlyList<string>> ListedProjectsAsync(Guid organizationId, ProjectStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IProjectReadModel>().ListAsync(organizationId, status, CancellationToken.None);

        return rows.Select(row => row.Name).ToList();
    }

    // US-2 · "The organization is restored exactly as it was": «Пример организации» comes back with its name, process,
    // project, teams, default areas and area tree as before, and is listed among the active organizations again. The
    // answer is the contract's Organization (api-contract § 1.7). The caller holds no right: any signed-in user may.
    [Fact]
    public async Task Restore_BringsBackNameProcessProjectsTeamsDefaultsAndTrees()
    {
        var (structure, reports, quarterly) = await CreateRichStructureAsync();
        var before = await ReadAsync(structure);
        await DeactivateAsync(structure.OrganizationId);
        var whileDeactivated = await ReadAsync(structure);

        // The arrangement bites: while it is deactivated, nothing beneath is reachable and the organization is not listed.
        Assert.Equal(OrganizationStatus.Deactivated, whileDeactivated.Organization.Status);
        Assert.False(whileDeactivated.Project.IsReachable);
        Assert.False(whileDeactivated.Platform.IsReachable);
        Assert.DoesNotContain(structure.OrganizationId, await ListedOrganizationsAsync(OrganizationStatus.Active));

        var answer = await RestoreAsync(structure.OrganizationId);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(["id", "name"], PropertyNames(answer.Body));
        Assert.Equal(structure.OrganizationId, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal(structure.Organization.Name, answer.Body.GetProperty("name").GetString());

        var after = await ReadAsync(structure);
        Assert.Equal(before, after);
        Assert.Equal(OrganizationStatus.Active, after.Organization.Status);
        Assert.Equal(structure.Organization.ProcessId, after.Organization.ProcessId);
        Assert.Equal(structure.Organization.Name, after.Organization.Name);
        Assert.True(after.Project.IsReachable);

        // The default areas: the first team keeps the root, «Платформа» keeps its own area.
        Assert.Equal(structure.RootId, after.FirstTeam.DefaultAreaId);
        Assert.Equal(structure.Platform.Area.Id, after.Platform.DefaultAreaId);

        // The tree: «Отчёты» is explicit and «Квартальные» inherits it, as before.
        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal("Отчёты / Квартальные", tree.PathOf(quarterly.Id));
        Assert.Equal(new ResolvedOwner(structure.Platform.Team.Id, false), tree.ResolveOwner(reports.Id));
        Assert.Equal(new ResolvedOwner(structure.Platform.Team.Id, true), tree.ResolveOwner(quarterly.Id));

        Assert.Contains(structure.OrganizationId, await ListedOrganizationsAsync(OrganizationStatus.Active));
        Assert.DoesNotContain(structure.OrganizationId, await ListedOrganizationsAsync(OrganizationStatus.Deactivated));
    }

    // US-2 · "...«Старый проект» is still deactivated and is listed among the organization's deactivated projects"
    // (BR-M1-20): a project deactivated on its own before the organization stays deactivated, with its teams and areas
    // as they were, while the project beside it comes back.
    [Fact]
    public async Task Restore_ProjectDeactivatedOnItsOwn_StaysDeactivated()
    {
        var (structure, _, _) = await CreateRichStructureAsync();
        var old = await OrganizationsTestData.CreateProjectAsync(
            factory, structure.OrganizationId, "Старый проект", "OLD", ProjectStatus.Deactivated);
        var before = await ReadAsync(structure, old);
        await DeactivateAsync(structure.OrganizationId);

        var answer = await RestoreAsync(structure.OrganizationId);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var after = await ReadAsync(structure, old);
        Assert.Equal(before, after);
        Assert.Equal(ProjectStatus.Deactivated, after.Old!.Status);
        Assert.False(after.Old.IsReachable);
        Assert.Equal(ProjectStatus.Active, after.Project.Status);
        Assert.True(after.Project.IsReachable);
        Assert.Equal(["Старый проект"], await ListedProjectsAsync(structure.OrganizationId, ProjectStatus.Deactivated));
        Assert.Equal(["Пример проекта"], await ListedProjectsAsync(structure.OrganizationId, ProjectStatus.Active));
    }

    // US-2 · "...its process screen opens again" (BR-M1-20, D-g): while the organization is deactivated mod-001's process
    // read answers not found, and after the restore it answers the organization's own process again.
    [Fact]
    public async Task Restore_ProcessReadAnswersAgain()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        await DeactivateAsync(organization.Id);

        var whileDeactivated = await ReadProcessAsync(organization.Id);
        Assert.Equal(HttpStatusCode.NotFound, whileDeactivated.Status);
        Assert.Equal(NotFoundCode, whileDeactivated.ErrorCode);

        var restored = await RestoreAsync(organization.Id);
        var afterwards = await ReadProcessAsync(organization.Id);

        Assert.Equal(HttpStatusCode.OK, restored.Status);
        Assert.Equal(HttpStatusCode.OK, afterwards.Status);
        Assert.Equal(organization.ProcessId.Value, afterwards.Body.GetProperty("id").GetGuid());
    }

    // api-contract § 1.7 · "Restoring an active organization changes nothing and answers 200": the same answer both
    // times, and nothing it holds changes. The filter accepts an active organization on this route too.
    [Fact]
    public async Task Restore_ActiveOrganization_IsANoOp()
    {
        var (structure, _, _) = await CreateRichStructureAsync();
        var before = await ReadAsync(structure);

        var first = await RestoreAsync(structure.OrganizationId);
        var second = await RestoreAsync(structure.OrganizationId);

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.Equal(structure.OrganizationId, first.Body.GetProperty("id").GetGuid());
        Assert.Equal(structure.Organization.Name, first.Body.GetProperty("name").GetString());
        Assert.Equal(first.Body.GetRawText(), second.Body.GetRawText());
        Assert.Equal(before, await ReadAsync(structure));
        Assert.Contains(structure.OrganizationId, await ListedOrganizationsAsync(OrganizationStatus.Active));
    }

    // US-2 · "An unknown organization answers not found" (BR-M1-20, D-g): a malformed value, an id that names no
    // organization, and an id that names something else (here, a project) answer organization.not_found, and nothing is
    // restored: a deactivated organization beside stays deactivated.
    [Theory]
    [InlineData("malformed")]
    [InlineData("all zeros")]
    [InlineData("without hyphens")]
    [InlineData("unknown")]
    [InlineData("a project's id")]
    public async Task Restore_Unknown_Returns404OrganizationNotFound_RestoresNothing(string how)
    {
        var bystander = await OrganizationsTestData.CreateOrganizationAsync(factory, status: OrganizationStatus.Deactivated);
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organizationId = how switch
        {
            "malformed" => "abc",
            "all zeros" => Guid.Empty.ToString(),
            "without hyphens" => bystander.Id.ToString("N"),
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => structure.ProjectId.ToString(),
        };

        var answer = await RestoreAsync(organizationId);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal(NotFoundCode, answer.ErrorCode);
        Assert.Equal("Организация не найдена.", answer.Detail);
        Assert.Equal(OrganizationStatus.Deactivated, await ReadStatusAsync(bystander.Id));
        Assert.Equal(OrganizationStatus.Active, await ReadStatusAsync(structure.OrganizationId));
    }

    // US-1 · "A visitor who is not signed in sees nothing": no token answers 401 before the not-found of a deactivated
    // organization, and the organization stays deactivated.
    [Fact]
    public async Task Restore_NoToken_Returns401_RestoresNothing()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, status: OrganizationStatus.Deactivated);

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"/api/organizations/{organization.Id}/restore");

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal("http.unauthorized", answer.ErrorCode);
        Assert.Equal(OrganizationStatus.Deactivated, await ReadStatusAsync(organization.Id));
    }

    private async Task<OrganizationStatus> ReadStatusAsync(Guid organizationId)
    {
        using var scope = factory.Services.CreateScope();
        var info = await scope.ServiceProvider.GetRequiredService<IOrganizationStructure>()
            .FindOrganizationAsync(organizationId, CancellationToken.None);

        return info!.Status;
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
}
