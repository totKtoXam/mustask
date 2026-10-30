namespace Mustask.Domain.Organizations;

/// <summary>
/// A link between a department and a team of the same organization (mod-018 BR-AW-6, OQ-8 A, ADR-0028 rule 4): one per pair, no
/// status, who made it and when, nothing granted (BR-AW-8). It ends with its department or its team (BR-AW-10). A project's teams
/// are feat-028's project-team links, never this one (OQ-11 A).
/// </summary>
public sealed class AreaTeam
{
    private AreaTeam()
    {
        // EF materialization.
    }

    private AreaTeam(Guid organizationId, Guid areaId, Guid teamId, string linkedBy, DateTimeOffset linkedAt)
    {
        OrganizationId = organizationId;
        AreaId = areaId;
        TeamId = teamId;
        LinkedBy = linkedBy;
        LinkedAt = linkedAt;
    }

    public Guid OrganizationId { get; private set; }

    public Guid AreaId { get; private set; }

    public Guid TeamId { get; private set; }

    /// <summary>The Keycloak subject (<c>sub</c>) of the person who made the link.</summary>
    public string LinkedBy { get; private set; } = null!;

    public DateTimeOffset LinkedAt { get; private set; }

    public static AreaTeam Link(Guid organizationId, Guid areaId, Guid teamId, string linkedBy, DateTimeOffset linkedAt)
    {
        AreaLinkRules.EnsureWellFormed(organizationId, areaId, teamId, linkedBy);

        return new AreaTeam(organizationId, areaId, teamId, linkedBy, linkedAt);
    }
}

internal static class AreaLinkRules
{
    public static void EnsureWellFormed(Guid organizationId, Guid areaId, Guid teamId, string linkedBy)
    {
        if (organizationId == Guid.Empty || areaId == Guid.Empty || teamId == Guid.Empty)
        {
            throw new ArgumentException("A link joins a department and a team of one organization; no id is all zeros.");
        }

        if (string.IsNullOrWhiteSpace(linkedBy) || linkedBy.Length > OrganizationLimits.LinkedByMaxLength)
        {
            throw new ArgumentException(
                $"The link's author is the token's subject: 1 to {OrganizationLimits.LinkedByMaxLength} characters.",
                nameof(linkedBy));
        }
    }
}
