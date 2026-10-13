namespace Mustask.Application.Abstractions.RichText;

/// <summary>
/// The server's reduction of a rich-text description to the safe subset and its canonical form (feat-018 US-2, US-7,
/// ADR-0027 decision 5). The implementation lives in Infrastructure, so the domain and the application stay free of
/// an HTML parser; the Domain keeps the limits and the order they are applied in.
/// </summary>
/// <remarks>
/// Synchronous and CPU-bound: it does no I/O, so it is legitimately called outside any transaction, and nothing slow
/// ever holds a row lock. One call is one parse and one write: markup length, visible length and "has content" come
/// from the same pass (BR-RT-7, BR-RT-8).
/// </remarks>
public interface IRichTextReducer
{
    /// <summary>
    /// Reduces <paramref name="html"/>. Never throws for a limit: output past
    /// <see cref="RichTextReductionLimits.MaxMarkupCodePoints"/> stops the work and answers
    /// <see cref="RichTextReduction.Exceeded"/>. It throws only for a defect (the platform's 500) or when
    /// <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    RichTextReduction Reduce(string html, RichTextReductionLimits limits, CancellationToken cancellationToken);
}

/// <summary>
/// The result of a reduction.
/// </summary>
/// <param name="Exceeded">
/// The work stopped because the output (or the work it takes to produce it) passed the bound; every other member is
/// then meaningless. The caller answers the markup reason.
/// </param>
/// <param name="Html">The reduced, canonical html; <c>""</c> when it has nothing to hold.</param>
/// <param name="MarkupLength">The reduced html in Unicode code points (BR-RT-7).</param>
/// <param name="VisibleLength">The visible text in Unicode code points (BR-RT-8).</param>
/// <param name="HasContent">Whether the visible text has a non-whitespace character (BR-RT-6).</param>
public sealed record RichTextReduction(bool Exceeded, string Html, int MarkupLength, int VisibleLength, bool HasContent)
{
    /// <summary>The answer for output past the bound.</summary>
    public static RichTextReduction OverTheBound { get; } = new(true, string.Empty, 0, 0, false);
}

/// <summary>
/// A profile point of the reduction (ADR-0027 "What later features inherit"): the budget is an argument, so a
/// narrower profile for comments is a new value, not a fork.
/// </summary>
/// <param name="MaxMarkupCodePoints">The most reduced markup, in Unicode code points, the output may reach.</param>
public sealed record RichTextReductionLimits(int MaxMarkupCodePoints);
