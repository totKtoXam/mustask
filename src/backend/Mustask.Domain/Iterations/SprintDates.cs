namespace Mustask.Domain.Iterations;

/// <summary>A sprint's dates (BR-4): both or neither, the finish not before the start.</summary>
public readonly record struct SprintDates(DateOnly? Start, DateOnly? End)
{
    public static SprintDates None => new(null, null);

    public bool IsSet => Start is not null && End is not null;

    /// <summary>The reason code the pair breaks, or <c>null</c> when it is acceptable for a Planned sprint.</summary>
    public string? Check()
    {
        if ((Start is null) != (End is null))
        {
            return SprintErrorCodes.DatesUnpaired;
        }

        return Start is { } start && End is { } end && end < start ? SprintErrorCodes.DatesReversed : null;
    }
}
