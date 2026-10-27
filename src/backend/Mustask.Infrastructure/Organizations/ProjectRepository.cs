using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <inheritdoc cref="OrganizationRepository"/>
internal sealed class ProjectRepository(MustaskDbContext context) : IProjectRepository
{
    public Task<ProjectStatus?> FindNameHolderAsync(Guid organizationId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        context.Projects
            .AsNoTracking()
            .Where(project =>
                project.OrganizationId == organizationId
                && project.NormalizedName == normalizedName
                && project.Id != exceptId)
            .Select(project => (ProjectStatus?)project.Status)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> IsKeyTakenAsync(Guid organizationId, string key, CancellationToken cancellationToken) =>
        context.Projects.AnyAsync(project => project.OrganizationId == organizationId && project.Key == key, cancellationToken);

    public Task<bool> IsReachableAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
        context.ReachableProjects(organizationId).AnyAsync(project => project.Id == projectId, cancellationToken);

    public async Task<int> CountReachableAsync(Guid organizationId, IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
        {
            return 0;
        }

        var ids = projectIds.Distinct().ToArray();

        return await context.ReachableProjects(organizationId).CountAsync(project => ids.Contains(project.Id), cancellationToken);
    }

    public async Task<Project?> LockAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM projects WHERE id = {projectId} AND organization_id = {organizationId} FOR NO KEY UPDATE",
            cancellationToken);

        return await context.Projects.SingleOrDefaultAsync(
            project => project.Id == projectId && project.OrganizationId == organizationId,
            cancellationToken);
    }

    public Task ClearDefaultTeamAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE projects SET default_team_id = NULL
             WHERE organization_id = {organizationId} AND id = {projectId} AND default_team_id = {teamId}
             """,
            cancellationToken);

    public Task ClearDefaultTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE projects SET default_team_id = NULL WHERE organization_id = {organizationId} AND default_team_id = {teamId}",
            cancellationToken);

    public void Add(Project project) => context.Projects.Add(project);

    public Task SaveAsync(CancellationToken cancellationToken) => OrganizationsSaveChanges.SaveAsync(context, cancellationToken);
}
