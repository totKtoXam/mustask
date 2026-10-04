using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.UserDirectory;

/// <summary>
/// The rules of the user entry and of an entries list (api-contract.md,
/// Part B), shared by both apply paths — a changes request (B-1) and a
/// snapshot page (B-3) — so neither slice has its own copy. A refusal is one
/// <see cref="ValidationException"/> whose field keys are JSON paths into the
/// request (<c>changes[3].email</c>, Part C).
/// </summary>
public static class UserPushValidation
{
    /// <summary>Microseconds per tick of <see cref="DateTimeOffset"/> (one tick is 100 ns).</summary>
    private const long TicksPerMicrosecond = TimeSpan.TicksPerMicrosecond;

    /// <param name="entries">The list as it arrived; null or empty is refused.</param>
    /// <param name="field">The list's JSON name: <c>changes</c> or <c>entries</c>.</param>
    /// <param name="isSnapshotPage">
    /// A snapshot page: a deletion is invalid there, and an id may appear at
    /// most once; a changes request allows both.
    /// </param>
    /// <param name="now">MUSTASK's clock: a moment more than an hour ahead of it is invalid.</param>
    public static IReadOnlyList<UserPush> Validate(
        IReadOnlyList<UserPushEntry?>? entries,
        string field,
        bool isSnapshotPage,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);

        var errors = new Dictionary<string, IReadOnlyList<FieldError>>(StringComparer.Ordinal);

        if (entries is null || entries.Count == 0)
        {
            errors[field] = [new FieldError(UserDirectoryErrorCodes.ValueRequired)];
            throw Refusal(errors);
        }

        if (entries.Count > UserDirectoryLimits.EntriesPerPush)
        {
            errors[field] = [LimitError(UserDirectoryErrorCodes.TooManyEntries, UserDirectoryLimits.EntriesPerPush)];
            throw Refusal(errors);
        }

        var latestMoment = MicrosecondsSinceEpoch(now + UserDirectoryLimits.MomentAheadOfClock);
        var pushes = new List<UserPush>(entries.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < entries.Count; index++)
        {
            var path = $"{field}[{index}]";
            var entry = entries[index];

            if (entry is null)
            {
                errors[path] = [new FieldError(UserDirectoryErrorCodes.ValueRequired)];
                continue;
            }

            var before = errors.Count;

            CheckRequiredText(errors, $"{path}.id", entry.Id);
            CheckRequiredText(errors, $"{path}.username", entry.Username);
            CheckOptionalText(errors, $"{path}.firstName", entry.FirstName);
            CheckOptionalText(errors, $"{path}.lastName", entry.LastName);
            CheckOptionalText(errors, $"{path}.email", entry.Email);

            if (entry.Moment is null)
            {
                errors[$"{path}.moment"] = [new FieldError(UserDirectoryErrorCodes.ValueRequired)];
            }
            else if (entry.Moment <= 0 || entry.Moment > latestMoment)
            {
                errors[$"{path}.moment"] = [new FieldError(UserDirectoryErrorCodes.ValueInvalid)];
            }

            var deleted = entry.Deleted ?? false;

            if (deleted && isSnapshotPage)
            {
                errors[$"{path}.deleted"] = [new FieldError(UserDirectoryErrorCodes.ValueInvalid)];
            }

            if (entry.Enabled is null && !deleted)
            {
                errors[$"{path}.enabled"] = [new FieldError(UserDirectoryErrorCodes.ValueRequired)];
            }

            if (isSnapshotPage && !string.IsNullOrEmpty(entry.Id) && !seenIds.Add(entry.Id) && !errors.ContainsKey($"{path}.id"))
            {
                errors[$"{path}.id"] = [new FieldError(UserDirectoryErrorCodes.ValueInvalid)];
            }

            if (errors.Count == before)
            {
                pushes.Add(new UserPush(
                    entry.Id!,
                    entry.Moment!.Value,
                    entry.Username!,
                    entry.FirstName,
                    entry.LastName,
                    entry.Email,
                    Enabled: !deleted && entry.Enabled!.Value,
                    Deleted: deleted));
            }
        }

        if (errors.Count > 0)
        {
            throw Refusal(errors);
        }

        return pushes;
    }

    /// <summary>The moment scale of the push channel: microseconds since the Unix epoch.</summary>
    public static long MicrosecondsSinceEpoch(DateTimeOffset value) =>
        (value.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) / TicksPerMicrosecond;

    /// <summary>
    /// A marker outside an entry — a snapshot's <c>enumerationStart</c>: required,
    /// greater than zero and at most <see cref="UserDirectoryLimits.MomentAheadOfClock"/>
    /// ahead of <paramref name="now"/>, the same rule as an entry's <c>moment</c>.
    /// </summary>
    public static long RequireMoment(long? moment, string field, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);

        if (moment is null)
        {
            throw FieldRefusal(field, UserDirectoryErrorCodes.ValueRequired);
        }

        if (moment <= 0 || moment > MicrosecondsSinceEpoch(now + UserDirectoryLimits.MomentAheadOfClock))
        {
            throw FieldRefusal(field, UserDirectoryErrorCodes.ValueInvalid);
        }

        return moment.Value;
    }

    /// <summary>
    /// The reason a count that must be present and not negative is refused
    /// (<c>pageCount</c>, <c>entryCount</c>), or null when it holds — so a
    /// slice can name every bad field in one refusal (<see cref="Refusal"/>).
    /// </summary>
    public static FieldError? NonNegativeCountError(int? value) =>
        value is null ? new FieldError(UserDirectoryErrorCodes.ValueRequired)
        : value < 0 ? new FieldError(UserDirectoryErrorCodes.ValueInvalid)
        : null;

    /// <summary>A single-field refusal, for the slices' own fields (<c>pageNumber</c>, <c>pageCount</c>, …).</summary>
    public static ValidationException FieldRefusal(string field, string reasonCode) =>
        Refusal(new Dictionary<string, IReadOnlyList<FieldError>>(StringComparer.Ordinal)
        {
            [field] = [new FieldError(reasonCode)],
        });

    private static void CheckRequiredText(Dictionary<string, IReadOnlyList<FieldError>> errors, string path, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            errors[path] = [new FieldError(UserDirectoryErrorCodes.ValueRequired)];
            return;
        }

        CheckOptionalText(errors, path, value);
    }

    private static void CheckOptionalText(Dictionary<string, IReadOnlyList<FieldError>> errors, string path, string? value)
    {
        if (value is null)
        {
            return;
        }

        if (DirectorySearchText.CodePointCount(value) > UserDirectoryLimits.FieldMaxLength)
        {
            errors[path] = [LimitError(UserDirectoryErrorCodes.ValueTooLong, UserDirectoryLimits.FieldMaxLength)];
        }
        else if (HoldsAnUnacceptableCharacter(value))
        {
            // U+0000: PostgreSQL text cannot hold it, so left to the save it is a 500, and the provider retries a 500
            // without end. A noncharacter: it has no place in a name; the derivation would take it (DirectorySearchText
            // reads it as U+FFFD since D-5 — before that .NET's normaliser threw on U+FFFE and it was a 500 too), but
            // B.3 refuses it so that a stored text is always one the provider could have sent. The provider never sends
            // either (ProjectedUserReader): the same rule as for an unpaired surrogate, which cannot reach this far.
            errors[path] = [new FieldError(UserDirectoryErrorCodes.ValueInvalid)];
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

    private static FieldError LimitError(string code, int limit) =>
        new(code, new ErrorParams { ["limit"] = ErrorParam.Number(limit) });

    /// <summary>The module's validation refusal: the top-level code and one entry per bad field.</summary>
    public static ValidationException Refusal(IReadOnlyDictionary<string, IReadOnlyList<FieldError>> errors) =>
        new(UserDirectoryErrorCodes.ValidationFailed, errors);
}
