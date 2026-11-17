using Mustask.Domain.Identity;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// The caller as a read that returns the rows of several projects takes it (feat-040 BR-TS-13, ADR-0029 § 7): the organization
/// of the address, the user and the role ADR-0027 rule 3 resolved from the membership. A <b>required</b> argument of the team
/// views' read model, not an optional one, so a read of a team's rows cannot be written without saying whose they are: its
/// <see cref="OrganizationId"/> is the tenant predicate of the statement (a project of another organization is never a row),
/// and it is where <c>mod-019-scoped-access</c> (what the role grants per object) and <c>feat-041-project-visibility</c>
/// (the set of projects the user may see) change what the caller carries — with no new signature.
/// </summary>
/// <param name="OrganizationId">The organization of the address, which the scoped check has already found the caller a member of.</param>
/// <param name="UserId">The caller's subject (the token's <c>sub</c>), the key of their memberships.</param>
/// <param name="Role">The caller's role in <paramref name="OrganizationId"/>.</param>
/// <param name="Visible">
/// The projects the caller sees (feat-062, BR-TD-17): what a multi-project read narrows by, before paging. A handler passes
/// <see cref="IOrganizationAccess.VisibleProjectsAsync"/>. A read reaches it through <see cref="Projects"/>, which fails closed:
/// a non-administrator caller with none is a defect, not an unrestricted read.
/// </param>
public sealed record CallerAccess(Guid OrganizationId, string UserId, OrganizationRole Role, VisibleProjects? Visible = null)
{
    /// <summary>The projects this caller's rows may come from; throws for a non-administrator whose sight was not given.</summary>
    public VisibleProjects Projects =>
        Visible ?? (Role == OrganizationRole.Admin
            ? VisibleProjects.All
            : throw new InvalidOperationException("A read of several projects' rows needs the caller's visible projects (feat-062)."));
}
