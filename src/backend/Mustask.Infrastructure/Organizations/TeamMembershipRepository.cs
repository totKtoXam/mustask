using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// Team memberships (mod-016 plan-backend § 3.4). The team row is locked through <see cref="TeamRepository"/>, not here; the
/// deletes are single raw statements.
/// </summary>
internal sealed class TeamMembershipRepository(MustaskDbContext context) : ITeamMembershipRepository
{
    public Task<bool> ExistsAsync(Guid teamId, string userId, CancellationToken cancellationToken) =>
        context.TeamMemberships
            .AsNoTracking()
            .AnyAsync(membership => membership.TeamId == teamId && membership.UserId == userId, cancellationToken);

    public void Add(TeamMembership membership) => context.TeamMemberships.Add(membership);

    public async Task<bool> DeleteAsync(Guid teamId, string userId, CancellationToken cancellationToken) =>
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM team_memberships WHERE team_id = {teamId} AND user_id = {userId}",
            cancellationToken) > 0;

    public async Task EndAllAsync(Guid teamId, CancellationToken cancellationToken) =>
        _ = await context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM team_memberships WHERE team_id = {teamId}",
            cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken) => OrganizationsSaveChanges.SaveAsync(context, cancellationToken);
}
