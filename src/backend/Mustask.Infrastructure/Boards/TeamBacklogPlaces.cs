using Mustask.Application.Features.Boards;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Boards;

/// <summary>
/// <see cref="ITeamBacklogPlaces"/> over <c>backlog_places</c>: the boards module drops its own rows when another module's
/// write changes who owns the item (mod-017 BR-OT-6, BR-OT-12; feat-028 BR-OL-8, BR-OL-9). Each is one set-based statement in the
/// caller's transaction.
/// </summary>
internal sealed class TeamBacklogPlaces(MustaskDbContext context) : ITeamBacklogPlaces
{
    public Task DropForItemAsync(Guid workItemId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM backlog_places WHERE work_item_id = {workItemId}",
            cancellationToken);

    public Task DropForTeamAsync(Guid teamId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM backlog_places WHERE team_id = {teamId}",
            cancellationToken);

    public Task DropForTeamInProjectAsync(Guid teamId, Guid projectId, CancellationToken cancellationToken) =>
        // The team's places on the items of this one project: the join names the project through the item, the place having none.
        context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             DELETE FROM backlog_places AS place
             USING work_items AS item
             WHERE item.id = place.work_item_id AND item.project_id = {projectId} AND place.team_id = {teamId}
             """,
            cancellationToken);
}
