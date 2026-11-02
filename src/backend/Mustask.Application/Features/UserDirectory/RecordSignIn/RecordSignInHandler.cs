using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.UserDirectory;
using Microsoft.Extensions.Logging;

namespace Mustask.Application.Features.UserDirectory.RecordSignIn;

/// <summary>
/// The only writer of the user projection (tech-025, ADR-0034): upserts the person who just signed in from the claims of
/// their id token. Validate (<see cref="SignInProfileValidation"/>, the projection's own limits), then in one transaction
/// take the per-user lock, load the user, and either create the row or bring it up to date — so two concurrent first
/// sign-ins of one person end in one row, the second finding the first's, and never in a unique-key failure.
/// </summary>
/// <remarks>
/// A refusal (<see cref="Mustask.Shared.Exceptions.ValidationException"/>) or a database failure propagates. What the
/// caller does with it is the caller's rule: the session callback fails the sign-in of a user the projection does not
/// know, and lets a known one through (ADR-0034, decision 2). The log names the user by id only, never by name or
/// e-mail.
/// </remarks>
public sealed class RecordSignInHandler(
    IProjectedUserStore users,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<RecordSignInHandler> logger)
{
    public async Task<RecordSignInResult> Handle(RecordSignInCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Profile);

        SignInProfileValidation.EnsureValid(command.Profile);

        var profile = command.Profile;
        var now = clock.UtcNow;

        var outcome = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var held = await users.LockAndLoadAsync(profile.Id!, ct);

            if (held is null)
            {
                users.Add(ProjectedUser.SignIn(profile, now));

                return SignInOutcome.Added;
            }

            return held.RecordSignIn(profile, now);
        }, cancellationToken);

        if (outcome == SignInOutcome.Added)
        {
            logger.LogInformation("User directory: user {UserId} added at first sign-in.", profile.Id);
        }
        else
        {
            logger.LogDebug("User directory: sign-in of user {UserId} recorded ({Outcome}).", profile.Id, outcome);
        }

        return new RecordSignInResult(outcome);
    }
}
