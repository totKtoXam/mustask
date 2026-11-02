using Mustask.Domain.UserDirectory;

namespace Mustask.Application.Features.UserDirectory.RecordSignIn;

/// <summary>What the sign-in did to the projection.</summary>
public sealed record RecordSignInResult(SignInOutcome Outcome);
