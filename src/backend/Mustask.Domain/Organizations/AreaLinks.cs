namespace Mustask.Domain.Organizations;

/// <summary>
/// A link between an area and a project of the same organization (mod-018 BR-AW-6, ADR-0028 rule 4): one per pair — the primary key
/// <c>(area_id, project_id)</c> — no status, who made it and when, and nothing granted (BR-AW-8). The link ends with its area; a
/// deactivated project's link stays stored and inert (BR-AW-10).
/// </summary>
public sealed class AreaProject
{
    private AreaProject()
    {
        // EF materialization.
    }

    private AreaProject(Guid organizationId, Guid areaId, Guid projectId, string linkedBy, DateTimeOffset linkedAt)
    {
        OrganizationId = organizationId;
        AreaId = areaId;
        ProjectId = projectId;
        LinkedBy = linkedBy;
        LinkedAt = linkedAt;
    }

    public Guid OrganizationId { get; private set; }

    public Guid AreaId { get; private set; }

    public Guid ProjectId { get; private set; }

    /// <summary>The Keycloak subject (<c>sub</c>) of the person who made the link.</summary>
    public string LinkedBy { get; private set; } = null!;

    public DateTimeOffset LinkedAt { get; private set; }

    public static AreaProject Link(Guid organizationId, Guid areaId, Guid projectId, string linkedBy, DateTimeOffset linkedAt)
    {
        AreaLinkRules.EnsureWellFormed(organizationId, areaId, projectId, linkedBy);

        return new AreaProject(organizationId, areaId, projectId, linkedBy, linkedAt);
    }
}

/// <summary>A link between an area and a team of the same organization; the same rules as <see cref="AreaProject"/>. It ends with its area or its team (BR-AW-10).</summary>
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
    public static void EnsureWellFormed(Guid organizationId, Guid areaId, Guid otherEndId, string linkedBy)
    {
        if (organizationId == Guid.Empty || areaId == Guid.Empty || otherEndId == Guid.Empty)
        {
            throw new ArgumentException("A link joins an area and a project or team of one organization; no id is all zeros.");
        }

        if (string.IsNullOrWhiteSpace(linkedBy) || linkedBy.Length > OrganizationLimits.LinkedByMaxLength)
        {
            throw new ArgumentException(
                $"The link's author is the token's subject: 1 to {OrganizationLimits.LinkedByMaxLength} characters.",
                nameof(linkedBy));
        }
    }
}
