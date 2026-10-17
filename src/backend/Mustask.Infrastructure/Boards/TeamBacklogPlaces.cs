using Mustask.Application.Features.Boards;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Boards;

/// <summary>
/// <see cref="ITeamBacklogPlaces"/> over <c>backlog_places</c>: the boards module drops its own rows when another module's
/// write changes who owns the item (mod-017 BR-OT-6, BR-OT-12). Each is one set-based statement in the caller's transaction.
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
}
