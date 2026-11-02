using Mustask.Domain.UserDirectory;

namespace Mustask.Application.Features.UserDirectory.RecordSignIn;

/// <summary>
/// A successful sign-in, as the panel's session endpoints see it (tech-025, ADR-0034): the claims of the id token the
/// identity provider issued for the person. There is no HTTP route — the session callback and the token renewal call the
/// handler in process. Validated by the handler through <see cref="SignInProfileValidation"/>.
/// </summary>
public sealed record RecordSignInCommand(SignInProfile Profile);
