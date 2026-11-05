using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary><c>AsNoTracking</c> projections, one query each, ordered in SQL (plan-backend § 6.4, § 6.5).</summary>
internal sealed class OrganizationReadModel(MustaskDbContext context) : IOrganizationReadModel
{
    public async Task<IReadOnlyList<OrganizationRow>> ListAsync(
        OrganizationStatus status,
        string userId,
        IReadOnlyCollection<OrganizationRole>? onlyRoles,
        CancellationToken cancellationToken)
    {
        // The membership narrows the list inside the one statement (mod-016 BR-M1-7): an EXISTS over the caller's own row.
        var mine = context.OrganizationMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId);

        if (onlyRoles is not null)
        {
            mine = mine.Where(membership => onlyRoles.Contains(membership.Role));
        }

        return await context.Organizations
            .AsNoTracking()
            .Where(organization => organization.Status == status)
            .Where(organization => mine.Any(membership => membership.OrganizationId == organization.Id))
            .OrderBy(organization => EF.Functions.Collate(organization.Name, OrganizationsCollation.Russian))
            .ThenBy(organization => organization.Id)
            .Select(organization => new OrganizationRow(organization.Id, organization.Name))
            .ToListAsync(cancellationToken);
    }

    public Task<OrganizationRow?> FindActiveAsync(Guid organizationId, CancellationToken cancellationToken) =>
        context.ActiveOrganizations()
            .AsNoTracking()
            .Where(organization => organization.Id == organizationId)
            .Select(organization => new OrganizationRow(organization.Id, organization.Name))
            .SingleOrDefaultAsync(cancellationToken);
}
