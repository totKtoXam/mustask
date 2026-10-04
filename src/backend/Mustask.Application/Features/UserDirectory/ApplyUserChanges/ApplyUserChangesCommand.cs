using Mustask.Domain.UserDirectory;

namespace Mustask.Application.Features.UserDirectory.ApplyUserChanges;

/// <summary>
/// <c>POST /api/user-directory/feed/changes</c> (api-contract.md B-1): the
/// changes the provider captured from Keycloak events, 1 to 500 entries,
/// validated by the handler through <see cref="UserPushValidation"/> under the
/// field <see cref="Field"/>.
/// </summary>
public sealed record ApplyUserChangesCommand(IReadOnlyList<UserPushEntry?>? Changes)
{
    /// <summary>The request's list field, the root of every field key of a refusal.</summary>
    public const string Field = "changes";
}
