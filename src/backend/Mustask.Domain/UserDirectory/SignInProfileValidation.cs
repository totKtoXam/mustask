using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.UserDirectory;

/// <summary>
/// The rules a sign-in's claims must meet before they are stored (tech-025, ADR-0034). The limits are the projection's
/// own (<see cref="UserDirectoryLimits.FieldMaxLength"/>, Keycloak's column size); a text the database cannot hold
/// (U+0000) or that has no place in a name (a Unicode noncharacter) is refused too, so a stored text is always one the
/// derivation of <see cref="DirectorySearchText"/> reads without surprise. A refusal is one
/// <see cref="ValidationException"/> keyed by the profile's field names.
/// </summary>
public static class SignInProfileValidation
{
    public const string IdField = "id";
    public const string UsernameField = "username";
    public const string FirstNameField = "firstName";
    public const string LastNameField = "lastName";
    public const string EmailField = "email";

    /// <exception cref="ValidationException">A required claim is missing, or any claim is over the limit or holds an unacceptable character.</exception>
    public static void EnsureValid(SignInProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var errors = new Dictionary<string, IReadOnlyList<FieldError>>(StringComparer.Ordinal);

        CheckRequired(errors, IdField, profile.Id);
        CheckRequired(errors, UsernameField, profile.Username);
        CheckOptional(errors, FirstNameField, profile.FirstName);
        CheckOptional(errors, LastNameField, profile.LastName);
        CheckOptional(errors, EmailField, profile.Email);

        if (errors.Count > 0)
        {
            throw new ValidationException(UserDirectoryErrorCodes.ValidationFailed, errors);
        }
    }

    private static void CheckRequired(Dictionary<string, IReadOnlyList<FieldError>> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [new FieldError(UserDirectoryErrorCodes.ValueRequired)];
            return;
        }

        CheckOptional(errors, field, value);
    }

    private static void CheckOptional(Dictionary<string, IReadOnlyList<FieldError>> errors, string field, string? value)
    {
        if (value is null)
        {
            return;
        }

        if (DirectorySearchText.CodePointCount(value) > UserDirectoryLimits.FieldMaxLength)
        {
            errors[field] =
            [
                new FieldError(
                    UserDirectoryErrorCodes.ValueTooLong,
                    new ErrorParams { ["limit"] = ErrorParam.Number(UserDirectoryLimits.FieldMaxLength) }),
            ];
        }
        else if (HoldsAnUnacceptableCharacter(value))
        {
            // U+0000: PostgreSQL text cannot hold it, so left to the save it would be a 500. A noncharacter has no place
            // in a name. (An unpaired surrogate cannot reach this far: a JWT payload is UTF-8 JSON.)
            errors[field] = [new FieldError(UserDirectoryErrorCodes.ValueInvalid)];
        }
    }

    private static bool HoldsAnUnacceptableCharacter(string value)
    {
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Value == 0 || DirectorySearchText.IsNoncharacter(rune))
            {
                return true;
            }
        }

        return false;
    }
}
