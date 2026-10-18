namespace Mustask.Domain.Organizations;

/// <summary>
/// Column widths of the organizations module (plan-backend § 3). They are no
/// business value anyone changes without a release, so they are constants next
/// to the entities that own them (docs/conventions/backend.md, "Named values").
/// </summary>
public static class OrganizationLimits
{
    /// <summary>The longest name of an organization, project, team or area, after trimming (BR-M1-1).</summary>
    public const int NameMaxLength = 100;

    /// <summary>The longest project key (BR-M1-1: 2 to 10 characters).</summary>
    public const int KeyMaxLength = 10;

    /// <summary>The width of a stored status name (<c>Active</c>, <c>Deactivated</c>, <c>Deleted</c>).</summary>
    public const int StatusMaxLength = 16;

    /// <summary>The width of a creator record: an opaque Keycloak subject (<c>sub</c>).</summary>
    public const int CreatedByMaxLength = 255;

    /// <summary>
    /// The width of a member's user id in a membership: an opaque Keycloak subject (mod-016 api-contract § 0.3). Its own
    /// constant, not <see cref="CreatedByMaxLength"/>: the two are equal by coincidence of the identifier they hold,
    /// and changing one must not silently change the other (docs/conventions/backend.md, "Named values", 5).
    /// </summary>
    public const int UserIdMaxLength = 255;

    /// <summary>The width of a stored role name (<c>admin</c>, <c>member</c>, <c>viewer</c>).</summary>
    public const int RoleMaxLength = 16;

    /// <summary>The longest description of a project or a team, in UTF-16 code units (feat-028 BR-OL-3).</summary>
    public const int DescriptionMaxLength = 2000;

    /// <summary>
    /// The width of the record of who made a project–team link: an opaque Keycloak subject. Its own constant, equal to
    /// <see cref="CreatedByMaxLength"/> by coincidence of the identifier it holds (docs/conventions/backend.md, "Named values", 5).
    /// </summary>
    public const int LinkedByMaxLength = 255;
}
