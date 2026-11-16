using Mustask.Domain.Identity;

namespace Mustask.Domain.Organizations;

/// <summary>
/// The sight rule of doc-016 BR-TD-12..-14 (feat-041, ADR-0040 § 4) as pure functions over facts the read model has already
/// gathered. The organization «Администратор» sees every project, team and department; everyone else sees only what they take
/// part in or what their access allows. Sight adds no code (BR-TD-16): it only decides whether the object exists for the caller.
/// </summary>
public static class SightRules
{
    /// <summary>
    /// A project is seen through (a) a grant that reaches it (a project grant, or a department grant through its node with the
    /// node's «Наследовать доступ» on), (b) membership of a team linked to it, or (c) its «Видим всей организации».
    /// </summary>
    public static bool SeesProject(OrganizationRole role, bool visibleToOrganization, bool grantReaches, bool memberOfLinkedTeam) =>
        role == OrganizationRole.Admin || visibleToOrganization || grantReaches || memberOfLinkedTeam;

    /// <summary>A team is seen by its member, by a holder of a grant on it or on its department, and by one who sees a project linked to it.</summary>
    public static bool SeesTeam(OrganizationRole role, bool isMember, bool grantOnTeamOrDepartment, bool seesLinkedProject) =>
        role == OrganizationRole.Admin || isMember || grantOnTeamOrDepartment || seesLinkedProject;

    /// <summary>A department is seen by a holder of a grant on it, and by one who sees a team or a project standing in it.</summary>
    public static bool SeesDepartment(OrganizationRole role, bool grantOnDepartment, bool seesTeamInside, bool seesProjectInside) =>
        role == OrganizationRole.Admin || grantOnDepartment || seesTeamInside || seesProjectInside;
}
