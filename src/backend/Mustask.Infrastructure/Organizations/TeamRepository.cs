using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <inheritdoc cref="OrganizationRepository"/>
/// <remarks>
/// The lock modes are fixed statements, not one with a mode spliced in: interpolation is for values only (backend.md, "Raw SQL").
/// Every statement carries the organization (feat-028 BR-OL-1), so a team of another organization is locked by nobody and loads
/// as <c>null</c>.
/// </remarks>
internal sealed class TeamRepository(MustaskDbContext context) : ITeamRepository
{
    public async Task<Team?> LockForNoKeyUpdateAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM teams WHERE id = {teamId} AND organization_id = {organizationId} FOR NO KEY UPDATE",
            cancellationToken);

        return await LoadAsync(organizationId, teamId, cancellationToken);
    }

    public async Task<Team?> LockForShareAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM teams WHERE id = {teamId} AND organization_id = {organizationId} FOR SHARE",
            cancellationToken);

        return await LoadAsync(organizationId, teamId, cancellationToken);
    }

    public async Task<IReadOnlyList<Team>> LockManyForShareAsync(
        Guid organizationId, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken)
    {
        if (teamIds.Count == 0)
        {
            return [];
        }

        var ids = teamIds.Distinct().ToArray();

        // ORDER BY before the locks: the rows are locked in ascending id order, one fixed order for every caller.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM teams WHERE organization_id = {organizationId} AND id = ANY({ids}) ORDER BY id FOR SHARE",
            cancellationToken);

        // A new statement under READ COMMITTED: the teams as the previous lock holder left them.
        var teams = await context.Teams
            .Where(team => team.OrganizationId == organizationId && ids.Contains(team.Id))
            .ToListAsync(cancellationToken);

        return teams.OrderBy(team => team.Id).ToList();
    }

    public Task<bool> IsActiveNameTakenAsync(Guid organizationId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        context.Teams.AnyAsync(
            team => team.OrganizationId == organizationId
                && team.Status == TeamStatus.Active
                && team.NormalizedName == normalizedName
                && team.Id != exceptId,
            cancellationToken);

    public Task<string?> FindNameAsync(Guid teamId, CancellationToken cancellationToken) =>
        context.Teams
            .AsNoTracking()
            .Where(team => team.Id == teamId)
            .Select(team => team.Name)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(Team team) => context.Teams.Add(team);

    public Task SaveAsync(CancellationToken cancellationToken) => OrganizationsSaveChanges.SaveAsync(context, cancellationToken);

    private Task<Team?> LoadAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
        context.Teams.SingleOrDefaultAsync(team => team.Id == teamId && team.OrganizationId == organizationId, cancellationToken);
}
