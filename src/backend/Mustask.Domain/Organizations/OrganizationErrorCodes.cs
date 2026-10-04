using Mustask.Shared.Errors;

namespace Mustask.Domain.Organizations;

/// <summary>
/// Error codes of the organizations module (ADR-0005 registry). The class lives
/// in Domain under the organizations module for two reasons: the module that
/// owns the organization (<c>mod-002</c>) takes the code over after this task
/// (status.md D-7), and that module's own domain throws it too. It is an
/// error-code class, not an Organization concept: this module knows no
/// organization, only an opaque identifier (status.md C-3).
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
    /// «Организация не найдена»: the identifier is malformed, or no process
    /// exists for it. After <c>mod-002</c> also an organization that does not
    /// exist or is deactivated, and after <c>mod-016</c> a non-member. None of
    /// these can be told apart from another (BR-M2-11).
    /// </summary>
    public const string OrganizationNotFound = "organizations.organization.not_found";
}
