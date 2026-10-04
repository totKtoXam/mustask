using Mustask.Application.Abstractions.Process;
using Mustask.Domain.Process;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Process;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/process</c>. A fixed three queries,
/// all <c>AsNoTracking</c> with projections: the process by organization, its
/// levels, and its types joined to their levels with their states. The server
/// fixes the order (design R-25: the client never sorts): levels by rank
/// descending, types by their level's rank descending and then by position (EPIC,
/// FEAT, US, BUG, TASK; design DD-2), states by position.
/// </summary>
public sealed class ProcessDefinitionReadModel(MustaskDbContext context) : IProcessDefinitionReadModel
{
    public async Task<ProcessDefinitionRow?> FindByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        // 1. The process by organization.
        var process = await context.Processes
            .AsNoTracking()
            .Where(candidate => candidate.OrganizationId == organizationId)
            .Select(candidate => new { candidate.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (process is null)
        {
            return null;
        }

        // 2. The levels. A separate query: a level with no type must not disappear from the answer.
        var levels = await context.BacklogLevels
            .AsNoTracking()
            .Where(level => level.ProcessId == process.Id)
            .OrderByDescending(level => level.Rank)
            .Select(level => new LevelProjection(level.Id, level.Name, level.Rank, level.Kind))
            .ToListAsync(cancellationToken);

        // 3. The types with their level and their states, in one statement.
        var types = await context.WorkItemTypes
            .AsNoTracking()
            .Where(type => type.ProcessId == process.Id)
            .Join(
                context.BacklogLevels,
                type => type.BacklogLevelId,
                level => level.Id,
                (type, level) => new { Type = type, Level = level })
            .OrderByDescending(pair => pair.Level.Rank)
            .ThenBy(pair => pair.Type.Position)
            .Select(pair => new TypeProjection(
                pair.Type.Id,
                pair.Type.Key,
                pair.Type.Name,
                pair.Type.Appearance,
                pair.Level.Id,
                pair.Level.Name,
                pair.Type.States
                    .OrderBy(state => state.Position)
                    .Select(state => new StateProjection(state.Id, state.Name, state.Position, state.Category))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return Project(process.Id, levels, types);
    }

    /// <summary>The one place the stored rows become the slice's rows.</summary>
    private static ProcessDefinitionRow Project(
        ProcessId id, IReadOnlyList<LevelProjection> levels, IReadOnlyList<TypeProjection> types) => new(
        id,
        levels.Select(level => new BacklogLevelRow(level.Id, level.Name, level.Rank, level.Kind)).ToList(),
        types.Select(type => new WorkItemTypeRow(
            type.Id,
            type.Key,
            type.Name,
            type.Appearance,
            type.LevelId,
            type.LevelName,
            type.States.Select(state => new StateRow(state.Id, state.Name, state.Position, state.Category)).ToList())).ToList());

    private sealed record LevelProjection(Guid Id, string Name, int Rank, BacklogLevelKind Kind);

    private sealed record StateProjection(Guid Id, string Name, int Position, StateCategory Category);

    private sealed record TypeProjection(
        Guid Id,
        string Key,
        string Name,
        AppearanceToken Appearance,
        Guid LevelId,
        string LevelName,
        List<StateProjection> States);
}
