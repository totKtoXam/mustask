namespace Mustask.Domain.UserDirectory;

/// <summary>What recording one sign-in did to the projection (tech-025, ADR-0034).</summary>
public enum SignInOutcome
{
    /// <summary>The first sign-in of this user: a row was created.</summary>
    Added,

    /// <summary>The user was known and something about them differs: the row was brought up to date.</summary>
    Updated,

    /// <summary>The user was known and nothing differs.</summary>
    Unchanged,
}
