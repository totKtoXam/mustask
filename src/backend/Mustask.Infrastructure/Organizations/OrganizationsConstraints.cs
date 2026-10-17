using Mustask.Domain.Organizations;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// The names of the module's unique indexes (plan-backend § 3), in one place:
/// the configurations create them under these names, and
/// <see cref="OrganizationsSaveChanges"/> maps a race on the business ones to
/// the same 409 code as its pre-check (§ 6.3).
/// </summary>
internal static class OrganizationsConstraints
{
    /// <summary>BR-M1-1: an organization's name, instance-wide, all rows.</summary>
    public const string OrganizationName = "ix_organizations_normalized_name";

    /// <summary>BR-M1-3: one process per organization.</summary>
    public const string OrganizationProcess = "ix_organizations_process_id";

    /// <summary>BR-M1-1: a project's name in its organization, all rows.</summary>
    public const string ProjectName = "ix_projects_organization_id_normalized_name";

    /// <summary>BR-M1-1: a project's key in its organization, all rows.</summary>
    public const string ProjectKey = "ix_projects_organization_id_key";

    /// <summary>feat-028 BR-OL-3: a team's name among the organization's active teams; freed on delete.</summary>
    public const string TeamName = "ix_teams_organization_id_normalized_name";

    /// <summary>feat-028 BR-OL-4: one link per project and team — the primary key <c>(project_id, team_id)</c>.</summary>
    public const string ProjectTeamPrimaryKey = "pk_project_teams";

    /// <summary>feat-028: a team's projects — the direction the link's primary key does not serve.</summary>
    public const string ProjectTeamTeamProject = "ix_project_teams_team_id_project_id";

    /// <summary>
    /// feat-028 ADR-0028 rule 4: a team membership names a team of its own organization — the composite key
    /// <c>(organization_id, team_id)</c> onto the team's alternate key.
    /// </summary>
    public const string TeamMembershipTeamKey = "fk_team_memberships_teams_organization_id_team_id";

    /// <summary>BR-M1-12: a name among siblings.</summary>
    public const string AreaName = "ix_areas_parent_id_normalized_name";

    /// <summary>BR-M1-11: one root per project.</summary>
    public const string AreaRoot = "ix_areas_project_id_root";

    /// <summary>BR-M1-5: one membership per user per organization — the primary key <c>(organization_id, user_id)</c> (mod-016).</summary>
    public const string MembershipPrimaryKey = "pk_organization_memberships";

    /// <summary>"My organizations" and the scoped check's read by user (mod-016).</summary>
    public const string MembershipUser = "ix_organization_memberships_user_id";

    /// <summary>A user is in a team at most once — the primary key <c>(team_id, user_id)</c> (mod-016).</summary>
    public const string TeamMembershipPrimaryKey = "pk_team_memberships";

    /// <summary>
    /// BR-M1-10: a team member is an organization member — the composite key onto the organization membership, which
    /// cascades a removal (BR-M1-36) and closes the add-versus-remove race (mod-016 plan-backend § 3.8).
    /// </summary>
    public const string TeamMembershipOrganizationMembership = "fk_team_memberships_organization_memberships";

    /// <summary>The index of that composite key; it also serves "the teams I am in".</summary>
    public const string TeamMembershipOrganizationUser = "ix_team_memberships_organization_id_user_id";

    /// <summary>
    /// mod-017 BR-OT-4 (ADR-0028 rule 9), feat-028 BR-OL-7: the key of the project's default-team setting onto one of the
    /// project's own links. A violation is a team that is no longer linked (plan-backend D-10, D-15).
    /// </summary>
    public const string ProjectDefaultTeamKey = "fk_projects_project_teams_id_default_team_id";

    /// <summary>BR-M1-1: 1 to 100 characters, on every table with a name.</summary>
    public static readonly string NameLengthCheck = $"char_length(name) BETWEEN 1 AND {OrganizationLimits.NameMaxLength}";

    /// <summary>feat-028 BR-OL-3: a description is absent or 1 to 2 000 characters; the empty string is stored as <c>NULL</c>.</summary>
    public static readonly string DescriptionLengthCheck =
        $"description IS NULL OR char_length(description) BETWEEN 1 AND {OrganizationLimits.DescriptionMaxLength}";
}
