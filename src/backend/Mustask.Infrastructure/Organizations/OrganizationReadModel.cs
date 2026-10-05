using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary><c>AsNoTracking</c> projections, one query each, ordered in SQL (plan-backend § 6.4, § 6.5).</summary>
internal sealed class OrganizationReadModel(MustaskDbContext context) : IOrganizationReadModel
{
    public async Task<IReadOnlyList<OrganizationRow>> ListAsync(OrganizationStatus status, CancellationToken cancellationToken) =>
        await context.Organizations
            .AsNoTracking()
            .Where(organization => organization.Status == status)
            .OrderBy(organization => EF.Functions.Collate(organization.Name, OrganizationsCollation.Russian))
            .ThenBy(organization => organization.Id)
            .Select(organization => new OrganizationRow(organization.Id, organization.Name))
            .ToListAsync(cancellationToken);

    public Task<OrganizationRow?> FindActiveAsync(Guid organizationId, CancellationToken cancellationToken) =>
        context.ActiveOrganizations()
            .AsNoTracking()
            .Where(organization => organization.Id == organizationId)
            .Select(organization => new OrganizationRow(organization.Id, organization.Name))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<OrganizationStatus?> FindStatusAsync(Guid organizationId, CancellationToken cancellationToken) =>
        context.Organizations
            .AsNoTracking()
            .Where(organization => organization.Id == organizationId)
            .Select(organization => (OrganizationStatus?)organization.Status)
            .SingleOrDefaultAsync(cancellationToken);
}
