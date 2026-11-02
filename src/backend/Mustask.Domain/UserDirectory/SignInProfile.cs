namespace Mustask.Domain.UserDirectory;

/// <summary>
/// What a successful sign-in tells MUSTASK about the person (tech-025, ADR-0034): the claims of the id token the
/// identity provider issued, as they arrived and before <see cref="SignInProfileValidation"/> has judged them. The
/// projection is written from nothing else.
/// </summary>
/// <param name="Id">The <c>sub</c> claim: the Keycloak user id, opaque text.</param>
/// <param name="Username">The <c>preferred_username</c> claim.</param>
/// <param name="FirstName">The <c>given_name</c> claim; null means empty.</param>
/// <param name="LastName">The <c>family_name</c> claim; null means empty.</param>
/// <param name="Email">The <c>email</c> claim; null means none.</param>
public sealed record SignInProfile(
    string? Id,
    string? Username,
    string? FirstName,
    string? LastName,
    string? Email);
