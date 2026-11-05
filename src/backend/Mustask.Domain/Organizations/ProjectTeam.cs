namespace Mustask.Domain.Organizations;

/// <summary>
/// A link between a project and a team of the same organization (feat-028 BR-OL-4, ADR-0028 rule 4). The team belongs to the
/// organization; a link says that it works in the project. There is one link per pair — the primary key <c>(project_id,
/// team_id)</c> — and no status: a row exists or it does not. A link records who made it and when, and grants nothing.
/// The organization is copied from the two ends once, so the database holds "both ends are in one organization" as two
/// composite keys; neither end ever changes organization, so the copy cannot go stale.
/// </summary>
public sealed class ProjectTeam
{
    private ProjectTeam()
    {
        // EF materialization.
    }

    private ProjectTeam(Guid organizationId, Guid projectId, Guid teamId, string linkedBy, DateTimeOffset linkedAt)
    {
        OrganizationId = organizationId;
        ProjectId = projectId;
        TeamId = teamId;
        LinkedBy = linkedBy;
        LinkedAt = linkedAt;
    }

    public Guid OrganizationId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid TeamId { get; private set; }

    /// <summary>The Keycloak subject (<c>sub</c>) of the person who made the link.</summary>
    public string LinkedBy { get; private set; } = null!;

    public DateTimeOffset LinkedAt { get; private set; }

    public static ProjectTeam Link(Guid organizationId, Guid projectId, Guid teamId, string linkedBy, DateTimeOffset linkedAt)
    {
        if (organizationId == Guid.Empty || projectId == Guid.Empty || teamId == Guid.Empty)
        {
            throw new ArgumentException("A link joins a project and a team of one organization; no id is all zeros.");
        }

        if (string.IsNullOrWhiteSpace(linkedBy) || linkedBy.Length > OrganizationLimits.LinkedByMaxLength)
        {
            throw new ArgumentException(
                $"The link's author is the token's subject: 1 to {OrganizationLimits.LinkedByMaxLength} characters.",
                nameof(linkedBy));
        }

        return new ProjectTeam(organizationId, projectId, teamId, linkedBy, linkedAt);
    }
}
