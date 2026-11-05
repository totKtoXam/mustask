using Mustask.Shared.Errors;

namespace Mustask.Domain.UserDirectory;

/// <summary>
/// The error codes of module <c>user_directory</c> — the registry of
/// ADR-0005, texts in <c>contracts/errors/user_directory.json</c>.
/// Declared in Domain because the sign-in rules (<see cref="SignInProfileValidation"/>) throw some of them, and by
/// Onion the domain cannot reference Application.
/// </summary>
/// <remarks>
/// tech-025 (ADR-0034) retired the push channel's seven codes — <c>too_many_entries</c>, <c>push_malformed</c>,
/// <c>push_unauthorized</c> and the four <c>snapshot_*</c> codes. Their constants are gone and their catalog entries
/// stay, marked "deprecated"; the ids stay taken (ADR-0005 rule 3, contracts/README.md).
/// </remarks>
[ErrorCodes]
public static class UserDirectoryErrorCodes
{
    /// <summary>Top-level code of every validation refusal (a search, a read of ids, a sign-in's claims).</summary>
    public const string ValidationFailed = "user_directory.validation_failed";

    /// <summary>Field reason: a required value missing or empty.</summary>
    public const string ValueRequired = "user_directory.value_required";

    /// <summary>Field reason: over a length limit (<c>params.limit</c>).</summary>
    public const string ValueTooLong = "user_directory.value_too_long";

    /// <summary>Field reason: a value that cannot be stored (U+0000, a noncharacter).</summary>
    public const string ValueInvalid = "user_directory.value_invalid";

    /// <summary>Field reason (<c>amongIds</c>, <c>ids</c>): over the ids limit of one read (<c>params.limit</c>).</summary>
    public const string TooManyIds = "user_directory.too_many_ids";
}
