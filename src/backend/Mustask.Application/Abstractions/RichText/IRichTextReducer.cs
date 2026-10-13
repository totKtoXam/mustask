namespace Mustask.Application.Abstractions.RichText;

/// <summary>
/// The server's reduction of a rich-text description to the safe subset and its canonical form (feat-018 US-2, US-7,
/// ADR-0027 decision 5). The implementation lives in Infrastructure, so the domain and the application stay free of
/// an HTML parser; the Domain keeps the limits and the order they are applied in.
/// </summary>
/// <remarks>
/// CPU-bound, no I/O: it is legitimately called outside any transaction, and nothing slow ever holds a row lock. It
/// does not occupy a request thread while it waits for a free slot or while it runs. One call is one parse and one
/// write: markup length, visible length and "has content" come from the same pass (BR-RT-7, BR-RT-8).
/// </remarks>
public interface IRichTextReducer
{
    /// <summary>
    /// Reduces <paramref name="html"/>. Never throws for a limit: output past
    /// <see cref="RichTextReductionLimits.MaxMarkupCodePoints"/>, or input past the deterministic structural bounds,
    /// answers <see cref="RichTextReduction.Exceeded"/>, the same answer for the same input on every host. It throws
    /// for a defect or when the safety net (a wall-clock bound no legitimate input comes near) trips: the platform's
    /// server error, never a refusal reason. It throws <see cref="OperationCanceledException"/> when
    /// <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    Task<RichTextReduction> ReduceAsync(string html, RichTextReductionLimits limits, CancellationToken cancellationToken);
}

/// <summary>
/// The result of a reduction.
/// </summary>
/// <param name="Exceeded">
/// The input passed a bound (the output's size, or a structural bound of the input); every other member is then
/// meaningless. The caller answers the markup reason.
/// </param>
/// <param name="Html">The reduced, canonical html; <c>""</c> when it has nothing to hold.</param>
/// <param name="MarkupLength">The reduced html in Unicode code points (BR-RT-7).</param>
/// <param name="VisibleLength">The visible text in Unicode code points (BR-RT-8).</param>
/// <param name="HasContent">Whether the visible text has a non-whitespace character (BR-RT-6).</param>
public sealed record RichTextReduction(bool Exceeded, string Html, int MarkupLength, int VisibleLength, bool HasContent)
{
    /// <summary>The answer for output or input past a bound.</summary>
    public static RichTextReduction OverTheBound { get; } = new(true, string.Empty, 0, 0, false);
}

/// <summary>
/// A profile point of the reduction (ADR-0027 "What later features inherit"): the budget is an argument, so a
/// narrower profile for comments is a new value, not a fork.
/// </summary>
/// <param name="MaxMarkupCodePoints">The most reduced markup, in Unicode code points, the output may reach.</param>
public sealed record RichTextReductionLimits(int MaxMarkupCodePoints);
