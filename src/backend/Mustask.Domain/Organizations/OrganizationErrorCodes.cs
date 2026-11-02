using Mustask.Shared.Errors;

namespace Mustask.Domain.Organizations;

/// <summary>
/// Error codes of the organizations module (M1, ADR-0005 registry). mod-001
/// registered the first one; mod-002 owns the file from then on (mod-002 D-k)
/// and adds every code of its api-contract.md § 6. The class lives in Domain
/// because the module's own domain throws these codes, and by Onion the domain
/// cannot reference Application.
/// </summary>
/// <remarks>
/// Every constant is an "application code" (ADR-0005, item 5): it is marked with
/// <see cref="ErrorCodesAttribute"/> and must have an entry with Russian and
/// Kazakh texts in <c>contracts/errors/organizations.json</c> (checked by
/// <c>tests/Mustask.Contracts.Tests</c>).
/// </remarks>
[ErrorCodes]
public static class OrganizationErrorCodes
{
    /// <summary>
    /// «Организация не найдена.»: the identifier is malformed, names nothing, or
    /// names a deactivated organization (BR-M1-20); after <c>mod-016</c> also a
    /// non-member. None of these can be told apart from another.
    /// </summary>
    public const string OrganizationNotFound = "organizations.organization.not_found";

    /// <summary>The project is malformed, unknown, of another organization, or not reachable (BR-M1-20, BR-M1-22).</summary>
    public const string ProjectNotFound = "organizations.project.not_found";

    /// <summary>The team is malformed, unknown, of another project, deleted, or not reachable; also a team named in a body.</summary>
    public const string TeamNotFound = "organizations.team.not_found";

    /// <summary>The area is malformed, unknown or of another organization; also an area named in a body.</summary>
    public const string AreaNotFound = "organizations.area.not_found";

    /// <summary>The top-level code of every <see cref="Mustask.Shared.Exceptions.ValidationException"/> of the module.</summary>
    public const string ValidationFailed = "organizations.validation_failed";

    /// <summary>Field reason of <c>name</c>: empty, spaces only, or over 100 characters after trimming (BR-M1-1).</summary>
    public const string NameLength = "organizations.name_length";

    /// <summary>Field reason of <c>description</c>: over 2 000 characters (feat-028 BR-OL-3).</summary>
    public const string DescriptionLength = "organizations.description_length";

    /// <summary>Field reason of <c>destination</c>: an unlink that names neither a team nor «Без команды» (feat-028 BR-OL-8).</summary>
    public const string TeamDestinationRequired = "organizations.team.destination_required";

    /// <summary>The token lacks <c>organizations.organization.create</c> (BR-M1-2). Never <c>http.forbidden</c> (D-p).</summary>
    public const string OrganizationCreateForbidden = "organizations.organization.create_forbidden";

    /// <summary>The process seed failed at an organization's creation; nothing is created (BR-M1-3, D-t). HTTP 500.</summary>
    public const string OrganizationCreateFailed = "organizations.organization.create_failed";

    /// <summary>An organization of any status holds the name (<c>params.name</c>: the holder's stored name).</summary>
    public const string OrganizationNameTaken = "organizations.organization.name_taken";

    /// <summary>An active project of the organization holds the name.</summary>
    public const string ProjectNameTaken = "organizations.project.name_taken";

    /// <summary>A deactivated project of the organization holds the name.</summary>
    public const string ProjectNameTakenByDeactivated = "organizations.project.name_taken_by_deactivated";

    /// <summary>Field reason of <c>key</c>: null or empty.</summary>
    public const string ProjectKeyRequired = "organizations.project.key_required";

    /// <summary>Field reason of <c>key</c>: not <c>^[A-Z][A-Z0-9]{1,9}$</c> as sent.</summary>
    public const string ProjectKeyInvalid = "organizations.project.key_invalid";

    /// <summary>A project of any status in the organization holds the key (<c>params.key</c>).</summary>
    public const string ProjectKeyTaken = "organizations.project.key_taken";

    /// <summary>An active team of the organization holds the name (feat-028 BR-OL-3).</summary>
    public const string TeamNameTaken = "organizations.team.name_taken";

    /// <summary>
    /// mod-017 BR-OT-2, feat-028 BR-OL-6: the team named in a work item's create or save, as the project's default or as an
    /// unlink's destination is an active team of the organization that is not linked to the project.
    /// </summary>
    public const string TeamNotInProject = "organizations.team.not_in_project";

    // feat-028 retired organizations.team.last_team_cannot_be_deleted with BR-OL-10 (a project's last team may be deleted):
    // its constant is gone and its catalog entry stays, marked "deprecated", like the four below.

    // mod-017 retired four codes with BR-M1-14 and BR-M1-18 (an area owner, a team's default area, the first team):
    // organizations.team.default_area_not_owned, organizations.team.first_team_default_fixed,
    // organizations.team.first_team_cannot_be_deleted and organizations.area.is_team_default. Their constants are gone; their
    // catalog entries stay, marked "deprecated", and the ids stay taken (ADR-0005 rule 3, contracts/README.md).

    /// <summary>A sibling holds the name (BR-M1-12).</summary>
    public const string AreaNameTaken = "organizations.area.name_taken";

    /// <summary>The destination is the moved node itself or one of its descendants (BR-M1-12).</summary>
    public const string AreaMoveIntoOwnSubtree = "organizations.area.move_into_own_subtree";

    /// <summary>The node has child nodes (a department with projects, a node with sub-areas) or a work item points at it (BR-AW-9).</summary>
    public const string AreaNotEmpty = "organizations.area.not_empty";

    /// <summary>A creation or a move would put a node below level 10 (BR-AW-3, mod-018).</summary>
    public const string AreaDepthExceeded = "organizations.area.depth_exceeded";

    /// <summary>
    /// A project's node renamed or deleted on its own (OQ-5 A, OQ-10 A): it follows its project. Un-deprecated by mod-018's typed
    /// tree (the first build had retired it with the per-project root).
    /// </summary>
    public const string AreaRootIsFixed = "organizations.area.root_is_fixed";

    /// <summary>A creation or a move under a parent the type rule of BR-AW-3 does not allow (OQ-3 A, OQ-7 A).</summary>
    public const string AreaTypeNotAllowed = "organizations.area.type_not_allowed";

    /// <summary>A sub-area moved under another project's node or sub-area (BR-AW-3, BR-AW-11).</summary>
    public const string AreaOutsideProject = "organizations.area.outside_project";

    /// <summary>A team linked as an area-team link to a node that is not a department (OQ-8 A, OQ-11 A).</summary>
    public const string AreaLinkTypeNotAllowed = "organizations.area.link_type_not_allowed";

    // ----- mod-016: organization access (api-contract § 5) -----

    /// <summary>
    /// «Недостаточно прав»: the caller is a member of the organization of the address, but the role's bundle lacks the
    /// route's code (ADR-0027). The one 403 of the scoped check, whatever module the route belongs to.
    /// </summary>
    public const string AccessForbidden = "organizations.access.forbidden";

    /// <summary>The user already has a membership in the organization, or a concurrent add took it first (BR-M1-5).</summary>
    public const string MembershipAlreadyMember = "organizations.membership.already_member";

    /// <summary>The change would leave no known active administrator (BR-M1-4, BR-M1-32).</summary>
    public const string MembershipLastAdmin = "organizations.membership.last_admin";

    /// <summary>The user is unknown to the user projection or inactive, so cannot be added (BR-M1-32).</summary>
    public const string MembershipUserUnavailable = "organizations.membership.user_unavailable";

    /// <summary>The user has no membership in the organization (also one another administrator just removed).</summary>
    public const string MembershipNotFound = "organizations.membership.not_found";

    /// <summary>Field reason of <c>role</c>: not exactly <c>admin</c>, <c>member</c> or <c>viewer</c>.</summary>
    public const string MembershipRoleInvalid = "organizations.membership.role_invalid";

    /// <summary>Field reason of <c>userId</c>: missing, blank or over 255 characters.</summary>
    public const string MembershipUserInvalid = "organizations.membership.user_invalid";

    /// <summary>The user is already in the team, or a concurrent add took it first.</summary>
    public const string TeamMemberExists = "organizations.team.member_exists";

    /// <summary>The user is not an active member of the team's organization (BR-M1-10, BR-M1-32).</summary>
    public const string TeamMemberNotInOrganization = "organizations.team.member_not_in_organization";

    /// <summary>The user is not in the team (also one another administrator just removed).</summary>
    public const string TeamMemberNotFound = "organizations.team.member_not_found";
}
