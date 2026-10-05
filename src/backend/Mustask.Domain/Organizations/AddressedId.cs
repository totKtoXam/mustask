using Mustask.Domain.Process;

namespace Mustask.Domain.Organizations;

/// <summary>
/// What a well-formed identifier of this module is, in an address or in a body
/// (api-contract § 0.2): a UUID in the canonical 8-4-4-4-12 form, in either
/// letter case, not all zeros. Anything else answers like an id that names
/// nothing (BR-M1-20). It is mod-001's <see cref="OrganizationKey"/> rule, by
/// delegation rather than by copy, so the organization-context filter and the
/// process read can never disagree about an organization id.
/// </summary>
public static class AddressedId
{
    public static bool TryParse(string? value, out Guid id) => OrganizationKey.TryParse(value, out id);
}
