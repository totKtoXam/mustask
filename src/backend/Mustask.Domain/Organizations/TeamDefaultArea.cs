using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Organizations;

/// <summary>
/// The one domain operation that spans a team and two areas: changing the
/// team's default area (US-4, BR-M1-14).
/// </summary>
public static class TeamDefaultArea
{
    /// <summary>
    /// Decides whether <paramref name="team"/>'s default moves from
    /// <paramref name="current"/> to <paramref name="next"/>. Refuses the first
    /// team (<see cref="OrganizationErrorCodes.TeamFirstTeamDefaultFixed"/>) and an
    /// area the team does not own explicitly
    /// (<see cref="OrganizationErrorCodes.TeamDefaultAreaNotOwned"/>). Returns
    /// <c>false</c> when <paramref name="next"/> already is the default: nothing changes.
    /// </summary>
    /// <remarks>
    /// When it returns <c>true</c>, the caller clears <paramref name="current"/>
    /// (<see cref="Area.ClearDefault"/>), flushes, and only then marks
    /// <paramref name="next"/> (<see cref="Area.MarkDefaultOf"/>) and flushes
    /// again: the one-default-per-team unique index is checked row by row, so a
    /// single statement that moved the mark could collide with itself
    /// (plan-backend § 8.2).
    /// </remarks>
    public static bool RequiresChange(Team team, Area current, Area next)
    {
        if (current.DefaultOfTeamId != team.Id)
        {
            throw new ArgumentException("The current area is the team's own default.", nameof(current));
        }

        if (next.ProjectId != team.ProjectId)
        {
            throw new ArgumentException("A team's default area is an area of its own project.", nameof(next));
        }

        if (team.IsFirstTeam)
        {
            throw new ConflictException(OrganizationErrorCodes.TeamFirstTeamDefaultFixed);
        }

        if (next.Id == current.Id)
        {
            return false;
        }

        if (next.OwnerTeamId != team.Id)
        {
            throw new ConflictException(OrganizationErrorCodes.TeamDefaultAreaNotOwned);
        }

        return true;
    }
}
