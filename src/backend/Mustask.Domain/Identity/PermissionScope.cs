namespace Mustask.Domain.Identity;

/// <summary>
/// Where a permission code comes from (ADR-0027, status.md D-8). An
/// <see cref="Instance"/> code is held in the token (Keycloak realm roles bundle
/// it, ADR-0018); an <see cref="Organization"/> code is never in the token: it
/// is derived on every request from the caller's membership in the
/// organization of the address.
/// </summary>
public enum PermissionScope
{
    /// <summary>Granted by the token: the realm's client roles of <c>mustask-api</c>.</summary>
    Instance,

    /// <summary>Granted by an organization membership's role bundle (<see cref="OrganizationRoleBundles"/>).</summary>
    Organization,
}
