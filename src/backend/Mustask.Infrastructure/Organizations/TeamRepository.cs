using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <inheritdoc cref="OrganizationRepository"/>
/// <remarks>
/// The three lock modes are three fixed statements, not one with a mode spliced
/// in: interpolation is for values only (backend.md, "Raw SQL").
/// </remarks>
internal sealed class TeamRepository(MustaskDbContext context) : ITeamRepository
{
    public async Task<Team?> LockForUpdateAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM teams WHERE id = {teamId} AND project_id = {projectId} FOR UPDATE",
            cancellationToken);

        return await LoadAsync(projectId, teamId, cancellationToken);
    }

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

    public Task<Team> GetFirstTeamAsync(Guid projectId, CancellationToken cancellationToken) =>
        context.Teams.SingleAsync(team => team.ProjectId == projectId && team.IsFirstTeam, cancellationToken);

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

    // The composite key keeps every area a team owns in the team's project (qa.md D-1), so naming the project changes no
    // row and lets ix_areas_project_id_owner_team_id, the key's index, find them.
    public Task ReassignExplicitAreasAsync(Guid fromTeamId, Guid toTeamId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE areas SET owner_team_id = {toTeamId}, default_of_team_id = NULL WHERE project_id = (SELECT project_id FROM teams WHERE id = {fromTeamId}) AND owner_team_id = {fromTeamId}",
            cancellationToken);

    public void Add(NewTeam team)
    {
        context.Teams.Add(team.Team);
        context.Areas.Add(team.Area);
    }

    public void Add(Team team) => context.Teams.Add(team);

    public Task SaveAsync(CancellationToken cancellationToken) => OrganizationsSaveChanges.SaveAsync(context, cancellationToken);

    private Task<Team?> LoadAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
        context.Teams.SingleOrDefaultAsync(team => team.Id == teamId && team.ProjectId == projectId, cancellationToken);
}
