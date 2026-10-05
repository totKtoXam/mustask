using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <inheritdoc cref="OrganizationReadModel"/>
internal sealed class ProjectReadModel(MustaskDbContext context) : IProjectReadModel
{
    public async Task<IReadOnlyList<ProjectRow>> ListAsync(Guid organizationId, ProjectStatus status, CancellationToken cancellationToken) =>
        await context.ProjectsOfActiveOrganization(organizationId, status)
            .AsNoTracking()
            .OrderBy(project => EF.Functions.Collate(project.Name, OrganizationsCollation.Russian))
            .ThenBy(project => project.Id)
            .Select(project => new ProjectRow(project.Id, project.OrganizationId, project.Name, project.Key))
            .ToListAsync(cancellationToken);

    public Task<ProjectRow?> FindReachableAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
        context.ReachableProjects(organizationId)
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => new ProjectRow(project.Id, project.OrganizationId, project.Name, project.Key))
            .SingleOrDefaultAsync(cancellationToken);
}
