using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// Organization memberships (mod-016 plan-backend § 3.4). Locks by key with raw SQL, then loads with the regular tracking
/// query (ADR-0010, "Consequences"): under READ COMMITTED the load is a new statement, so it sees the row as the lock
/// holder left it. The delete is one raw statement, and the foreign key from <c>team_memberships</c> cascades it
/// (BR-M1-36).
/// </summary>
internal sealed class MembershipRepository(MustaskDbContext context) : IMembershipRepository
{
    public Task<bool> ExistsAsync(Guid organizationId, string userId, CancellationToken cancellationToken) =>
        context.OrganizationMemberships
            .AsNoTracking()
            .AnyAsync(membership => membership.OrganizationId == organizationId && membership.UserId == userId, cancellationToken);

    public Task<OrganizationRole?> FindRoleAsync(Guid organizationId, string userId, CancellationToken cancellationToken) =>
        context.OrganizationMemberships
            .AsNoTracking()
            .Where(membership => membership.OrganizationId == organizationId && membership.UserId == userId)
            .Select(membership => (OrganizationRole?)membership.Role)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<OrganizationMembership?> LockMemberAsync(Guid organizationId, string userId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM organization_memberships WHERE organization_id = {organizationId} AND user_id = {userId} FOR UPDATE",
            cancellationToken);

        return await context.OrganizationMemberships.SingleOrDefaultAsync(
            membership => membership.OrganizationId == organizationId && membership.UserId == userId,
            cancellationToken);
    }

    public void Add(OrganizationMembership membership) => context.OrganizationMemberships.Add(membership);

    public async Task<bool> DeleteAsync(Guid organizationId, string userId, CancellationToken cancellationToken) =>
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM organization_memberships WHERE organization_id = {organizationId} AND user_id = {userId}",
            cancellationToken) > 0;

    public Task SaveAsync(CancellationToken cancellationToken) => OrganizationsSaveChanges.SaveAsync(context, cancellationToken);
}
