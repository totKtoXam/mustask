using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Organizations;

/// <summary>
/// A team as the rule about naming it reads it (mod-017 api-contract § 0.2): the facts of
/// <c>IOrganizationStructure.FindTeamAsync</c>, whatever the team is. <paramref name="IsReachable"/> is <c>true</c>
/// only for an active team of an active project of an active organization.
/// </summary>
public sealed record TeamFacts(Guid Id, Guid OrganizationId, Guid ProjectId, TeamStatus Status, bool IsReachable);

/// <summary>
/// The one rule for a team named in a write (mod-017 BR-OT-2, BR-OT-4, BR-OT-16): a work item's create or save, and the
/// project's default naming, all judge the team by this table. The first row that applies:
/// <list type="number">
/// <item>no such team, a team of another organization, a deleted team, a team of a project that is not reachable →
/// <see cref="OrganizationErrorCodes.TeamNotFound"/> (404), alike for each (ADR-0026 rule 5: a deactivation is never revealed;
/// BR-OT-16: another tenant's id names nothing);</item>
/// <item>an active team of another active project of the organization → <see cref="OrganizationErrorCodes.TeamNotInProject"/> (409);</item>
/// <item>an active team of the project → accepted.</item>
/// </list>
/// </summary>
public static class ProjectTeams
{
    /// <summary>
    /// Accepts <paramref name="team"/> as a team of <paramref name="projectId"/> in <paramref name="organizationId"/>, or
    /// throws the refusal of the table above. The caller has judged the project of the address already (reachable, of the
    /// organization), so a team of that project that is active is reachable.
    /// </summary>
    public static void EnsureBindable(TeamFacts? team, Guid organizationId, Guid projectId)
    {
        if (team is null || team.OrganizationId != organizationId || team.Status != TeamStatus.Active || !team.IsReachable)
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        if (team.ProjectId != projectId)
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
