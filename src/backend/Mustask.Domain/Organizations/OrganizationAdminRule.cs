using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Organizations;

/// <summary>
/// BR-M1-4, the one definition: no change MUSTASK makes leaves an organization
/// without a known active administrator (BR-M1-32). Handlers count — under the
/// organization's row lock, from what is committed, counting only administrators
/// whom the user projection holds as active — and the domain decides.
/// </summary>
public static class OrganizationAdminRule
{
    /// <summary>
    /// A role change or a removal that takes an administrator out of the bundle leaves
    /// <paramref name="knownActiveAdministratorsLeft"/> known active administrators.
    /// </summary>
    /// <exception cref="ConflictException"><see cref="OrganizationErrorCodes.MembershipLastAdmin"/> when none is left.</exception>
    public static void EnsureAnAdministratorRemains(int knownActiveAdministratorsLeft)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(knownActiveAdministratorsLeft);

        if (knownActiveAdministratorsLeft == 0)
        {
            throw new ConflictException(OrganizationErrorCodes.MembershipLastAdmin);
        }
    }
}
