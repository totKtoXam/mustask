using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Organizations;

/// <summary>
/// A team as the rule about naming it reads it (feat-028 api-contract § 0.3): the facts of
/// <c>IOrganizationStructure.FindProjectTeamAsync</c> — the team, and whether it is <b>linked to the project of the request</b>.
/// A team of another organization never reaches this record: the structure answers <c>null</c> for it. A write that locks the link
/// takes <see cref="IsLinked"/> from the statement that locked it, which returns the row it held, and not from the structure's read:
/// a link committed after the lock reads linked there and is held by nobody (review-security S-1).
/// </summary>
public sealed record TeamFacts(Guid Id, Guid OrganizationId, TeamStatus Status, bool IsLinked);

/// <summary>
/// The one rule for a team named in a write (mod-017 BR-OT-2, BR-OT-4, feat-028 BR-OL-6): a work item's create or save, the
/// project's default naming and the destination of an unlink all judge the team by this table. The first row that applies:
/// <list type="number">
/// <item>no such team, a team of another organization, a deleted team →
/// <see cref="OrganizationErrorCodes.TeamNotFound"/> (404), alike for each (ADR-0026 rule 5; BR-OL-1: another tenant's id
/// names nothing);</item>
/// <item>an active team of the organization that is not linked to the project →
/// <see cref="OrganizationErrorCodes.TeamNotInProject"/> (409);</item>
/// <item>an active team linked to the project → accepted.</item>
/// </list>
/// </summary>
public static class ProjectTeams
{
    /// <summary>
    /// Accepts <paramref name="team"/> as a team linked to the project of the request in <paramref name="organizationId"/>, or
    /// throws the refusal of the table above. The caller has judged the project of the address already (reachable, of the
    /// organization).
    /// </summary>
    public static void EnsureBindable(TeamFacts? team, Guid organizationId)
    {
        EnsureLinkable(team, organizationId);

        if (!team!.IsLinked)
        {
            throw new ConflictException(OrganizationErrorCodes.TeamNotInProject);
        }
    }

    /// <summary>
    /// Accepts <paramref name="team"/> as a team that may be linked or named: an active team of the organization, linked or not.
    /// Anything else is <see cref="OrganizationErrorCodes.TeamNotFound"/>.
    /// </summary>
    public static void EnsureLinkable(TeamFacts? team, Guid organizationId)
    {
        if (team is null || team.OrganizationId != organizationId || team.Status != TeamStatus.Active)
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }
    }

    /// <summary>
    /// The destination of an unlink (BR-OL-8), judged under the links' locks: the team on a fresh read, its link by what the lock
    /// statement returned (<see cref="TeamFacts.IsLinked"/>). An unknown, foreign or deleted team is
    /// <see cref="OrganizationErrorCodes.TeamNotFound"/>; the unlinked team itself, or an active team that is not linked to the
    /// project, is <see cref="OrganizationErrorCodes.TeamNotInProject"/>.
    /// </summary>
    public static void EnsureDestination(TeamFacts? destination, Guid unlinkedTeamId)
    {
        if (destination is null || destination.Status != TeamStatus.Active)
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        if (destination.Id == unlinkedTeamId || !destination.IsLinked)
        {
            throw new ConflictException(OrganizationErrorCodes.TeamNotInProject);
        }
    }

    /// <summary>
    /// The owning team of a child created beneath a parent (BR-OT-3): the parent's team as stored when the child is saved,
    /// else the project's default team as it stands then, else none. A team named in the same request is never read.
    /// </summary>
    public static Guid? ChildTeam(Guid? parentTeamId, Guid? projectDefaultTeamId) => parentTeamId ?? projectDefaultTeamId;
}
