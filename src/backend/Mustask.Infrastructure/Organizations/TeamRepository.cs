using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <inheritdoc cref="OrganizationRepository"/>
/// <remarks>
/// The lock modes are fixed statements, not one with a mode spliced in: interpolation is for values only (backend.md, "Raw SQL").
/// </remarks>
internal sealed class TeamRepository(MustaskDbContext context) : ITeamRepository
{
    public async Task<Team?> LockForNoKeyUpdateAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM teams WHERE id = {teamId} AND project_id = {projectId} FOR NO KEY UPDATE",
            cancellationToken);

        return await LoadAsync(projectId, teamId, cancellationToken);
    }

    public async Task<Team?> LockForShareAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM teams WHERE id = {teamId} AND project_id = {projectId} FOR SHARE",
            cancellationToken);

        return await LoadAsync(projectId, teamId, cancellationToken);
    }

    public async Task<IReadOnlyList<Team>> LockProjectTeamsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        // ORDER BY before the lock: the rows are locked in ascending id order, one fixed order for every caller.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM teams WHERE project_id = {projectId} AND status = 'Active' ORDER BY id FOR NO KEY UPDATE",
            cancellationToken);

        // A new statement under READ COMMITTED: the teams as the previous lock holder left them.
        var teams = await context.Teams
            .Where(team => team.ProjectId == projectId && team.Status == TeamStatus.Active)
            .ToListAsync(cancellationToken);

        return teams.OrderBy(team => team.Id).ToList();
    }

    public async Task<DefaultTeamMark> GetMarkAsync(Team team, CancellationToken cancellationToken)
    {
        var projectId = team.ProjectId;
        var teamId = team.Id;

        var stored = await context.Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => new
            {
                IsNamed = project.DefaultTeamId == teamId,
                ActiveTeams = context.Teams.Count(other => other.ProjectId == projectId && other.Status == TeamStatus.Active),
            })
            .SingleAsync(cancellationToken);

        return ProjectDefaultTeam.MarkOf(stored.IsNamed, stored.ActiveTeams);
    }

    public Task<bool> IsActiveNameTakenAsync(Guid projectId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        context.Teams.AnyAsync(
            team => team.ProjectId == projectId
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

    private Task<Team?> LoadAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
        context.Teams.SingleOrDefaultAsync(team => team.Id == teamId && team.ProjectId == projectId, cancellationToken);
}
