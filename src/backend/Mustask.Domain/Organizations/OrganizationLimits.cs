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
}
