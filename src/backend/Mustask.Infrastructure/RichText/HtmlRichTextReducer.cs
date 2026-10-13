using System.Text;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Mustask.Application.Abstractions.RichText;

namespace Mustask.Infrastructure.RichText;

/// <summary>Settings of <see cref="HtmlRichTextReducer"/>, from configuration section <c>RichText</c>.</summary>
public sealed class RichTextOptions
{
    public const string SectionName = "RichText";

    /// <summary>
    /// How many reductions run at once. A 2 000 000-unit input can hold hundreds of thousands of nodes, so a burst
    /// queues instead of exhausting memory; <c>0</c> means <c>max(2, ProcessorCount)</c>.
    /// </summary>
    public int MaxConcurrentReductions { get; set; }

    /// <summary>
    /// The parse's work budget in milliseconds. The HTML5 tree builder is quadratic in nesting depth (spike, plan
    /// § 6.1), so hostile deep nesting is bounded by time, not by our walker; expiry is "exceeded". Legitimate input
    /// at the ceiling parses in well under a second.
    /// </summary>
    public int ParseBudgetMilliseconds { get; set; } = 1_500;
}

/// <summary>
/// <see cref="IRichTextReducer"/> on AngleSharp's WHATWG parser (ADR-0027 decision 5, plan-backend § 5): normalise
/// the raw string, parse once, walk the document into the canonical model, write it under the output budget.
/// </summary>
internal sealed class HtmlRichTextReducer : IRichTextReducer, IDisposable
{
    // The explicit <body> puts the tree builder in the "in body" mode a fragment parse with a body context starts
    // in, and a document parse is near-linear where ParseFragment is quadratic in siblings (plan § 6.1).
    private const string Wrapper = "<!DOCTYPE html><html><head></head><body>";

    /// <summary>Each position of a table is at least <c>&lt;td&gt;&lt;p&gt;&lt;/p&gt;&lt;/td&gt;</c>.</summary>
    private const int MinCellMarkup = 16;

    private readonly RichTextVocabulary vocabulary;
    private readonly SemaphoreSlim gate;
    private readonly TimeSpan parseBudget;

    public HtmlRichTextReducer(RichTextVocabulary vocabulary, RichTextOptions? options = null)
    {
        options ??= new RichTextOptions();
        this.vocabulary = vocabulary;
        var slots = options.MaxConcurrentReductions > 0 ? options.MaxConcurrentReductions : Math.Max(2, Environment.ProcessorCount);
        gate = new SemaphoreSlim(slots, slots);
        parseBudget = TimeSpan.FromMilliseconds(Math.Max(1, options.ParseBudgetMilliseconds));
    }

    public RichTextReduction Reduce(string html, RichTextReductionLimits limits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(limits);

        gate.Wait(cancellationToken);

        try
        {
            return ReduceCore(Normalise(html), limits.MaxMarkupCodePoints, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose() => gate.Dispose();

    private RichTextReduction ReduceCore(string text, int limit, CancellationToken cancellationToken)
    {
        IHtmlDocument document;

        using (var parseCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            parseCancellation.CancelAfter(parseBudget);

            try
            {
                var parser = new HtmlParser(new HtmlParserOptions { IsScripting = true });
                document = parser.ParseDocumentAsync(Wrapper + text, parseCancellation.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return RichTextReduction.OverTheBound;
            }
        }

        try
        {
            // A frameset replaces the body; nothing of it is kept.
            if (document.Body is null)
            {
                return new RichTextReduction(false, string.Empty, 0, 0, false);
            }

            var converter = new DomConverter(vocabulary, limit / MinCellMarkup, limit, cancellationToken);
            var blocks = converter.Convert(document.Body);

            var writer = new CanonicalWriter(vocabulary, limit, converter.DroppedWhitespace);
            writer.Write(blocks);

            return new RichTextReduction(
                false, writer.Html, (int)writer.MarkupLength, (int)writer.VisibleLength, writer.HasContent);
        }
        catch (RichTextBudgetExceededException)
        {
            return RichTextReduction.OverTheBound;
        }
        finally
        {
            document.Dispose();
        }
    }

    /// <summary>U+0000 removed, each lone surrogate replaced by U+FFFD (BR-RT-8, BR-RT-9), before anything parses it.</summary>
    internal static string Normalise(string raw)
    {
        var clean = true;

        for (var index = 0; index < raw.Length && clean; index++)
        {
            var c = raw[index];

            if (c == '\0')
            {
                clean = false;
            }
            else if (char.IsHighSurrogate(c))
            {
                if (index + 1 < raw.Length && char.IsLowSurrogate(raw[index + 1]))
                {
                    index++;
                }
                else
                {
                    clean = false;
                }
            }
            else if (char.IsLowSurrogate(c))
            {
                clean = false;
            }
        }

        if (clean)
        {
            return raw;
        }

        var builder = new StringBuilder(raw.Length);

        foreach (var rune in raw.EnumerateRunes())
        {
            if (rune.Value != 0)
            {
                builder.Append(rune.ToString());
            }
        }

        return builder.ToString();
    }
}
