namespace Mustask.Domain.UserDirectory;

/// <summary>
/// One user entry of the push channel (api-contract.md, Part B, "The user
/// entry"), already validated by the slice that received it: one user's whole
/// projected state as of <see cref="Moment"/>, or that user's deletion
/// carrying the last state Keycloak held (BR-UP-6).
/// </summary>
/// <param name="Id">The Keycloak user id — the <c>sub</c> of that user's tokens.</param>
/// <param name="Moment">The ordering marker: microseconds since the Unix epoch on the provider's clock (ADR-0025 § 6).</param>
/// <param name="Username">Required, also on a deletion.</param>
/// <param name="FirstName">Null means empty.</param>
/// <param name="LastName">Null means empty.</param>
/// <param name="Email">Null means none.</param>
/// <param name="Enabled">Ignored when <paramref name="Deleted"/> is true.</param>
/// <param name="Deleted">A reported deletion.</param>
public sealed record UserPush(
    string Id,
    long Moment,
    string Username,
    string? FirstName,
    string? LastName,
    string? Email,
    bool Enabled,
    bool Deleted);
