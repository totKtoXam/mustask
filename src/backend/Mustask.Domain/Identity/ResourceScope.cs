namespace Mustask.Domain.Identity;

/// <summary>
/// The kind of object an organization code is checked on (ADR-0029 § 2, amended by ADR-0038). Every organization code has
/// exactly one; instance codes have none. "Scope" in documents means this and nothing else: where a code comes from is
/// <see cref="PermissionSource"/>. A work item, its project's backlog and its card are checked on the item's project; a
/// sprint and a team's backlog on the team. <see cref="Area"/> is the stored name of the «Департамент» and of the project
/// nodes and sub-areas below it (ADR-0031).
/// </summary>
public enum ResourceScope
{
    /// <summary>The organization itself.</summary>
    Organization,

    /// <summary>A node of the area tree.</summary>
    Area,

    /// <summary>A project.</summary>
    Project,

    /// <summary>A team.</summary>
    Team,
}
