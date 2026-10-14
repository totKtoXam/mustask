using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// The scoped check's one read (ADR-0027 rule 3): the organization's status and the caller's role in it, in one statement —
/// the organization left-joined to the caller's membership by its key, <c>AsNoTracking</c>. It reads <c>organizations</c> and
/// <c>organization_memberships</c> and nothing else: no user projection (BR-M1-32), no Keycloak.
/// </summary>
internal sealed class OrganizationAccessReadModel(MustaskDbContext context) : IOrganizationAccessReadModel
{
    public Task<OrganizationAccessRow?> FindAsync(Guid organizationId, string userId, CancellationToken cancellationToken) =>
        context.Organizations
            .AsNoTracking()
            .Where(organization => organization.Id == organizationId)
            .Select(organization => new OrganizationAccessRow(
                organization.Status,
                context.OrganizationMemberships
                    .Where(membership => membership.OrganizationId == organization.Id && membership.UserId == userId)
                    .Select(membership => (OrganizationRole?)membership.Role)
                    .FirstOrDefault()))
            .SingleOrDefaultAsync(cancellationToken);
}
