using Mustask.Shared.Errors;

namespace Mustask.Domain.Iterations;

/// <summary>
/// A sprint's name (BR-7): trimmed, 1 to 100 UTF-16 code units, compared ignoring letter case and surrounding spaces.
/// <see cref="Normalized"/> is the key the team's unique index holds (the rule of <c>EntityName</c>, with this module's own
/// reason code).
/// </summary>
public readonly record struct SprintName
{
    private SprintName(string value)
    {
        Value = value;
        Normalized = Normalize(value);
    }

    public string Value { get; }

    public string Normalized { get; }

    public static FieldError InvalidReason => new(SprintErrorCodes.NameInvalid);

    public static bool TryCreate(string? raw, out SprintName name)
    {
        var trimmed = raw?.Trim();

        if (trimmed is not { Length: > 0 and <= SprintLimits.NameMaxLength })
        {
            name = default;
            return false;
        }

        name = new SprintName(trimmed);
        return true;
    }

    /// <summary>The default name of sprint <paramref name="number"/> of a team (BR-7): «Спринт 12».</summary>
    public static SprintName Default(int number) => new($"Спринт {number}");

    public static string Normalize(string value) => value.Trim().ToUpperInvariant();

    public override string ToString() => Value;
}
