namespace Mustask.Domain.Iterations;

/// <summary>The named values of the iterations module, each next to the concept that owns it (backend.md, "Named values").</summary>
public static class SprintLimits
{
    /// <summary>A sprint's name, in UTF-16 code units after trimming (BR-7).</summary>
    public const int NameMaxLength = 100;

    /// <summary>The rows of one page of the list, of the participants and of the outcome (OQ-5, D-2).</summary>
    public const int PageSize = 200;

    /// <summary>The most sprints one range reads (BR-15).</summary>
    public const int MaxRangeSprints = 20;

    /// <summary>The default range reaches this many days back from the sprint's start (BR-15).</summary>
    public const int DefaultRangeDays = 90;

    /// <summary>The state names as stored.</summary>
    public const int StateMaxLength = 16;
}
