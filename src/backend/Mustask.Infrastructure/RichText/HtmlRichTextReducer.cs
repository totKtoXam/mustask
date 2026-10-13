using System.Runtime.CompilerServices;
using System.Text;
using AngleSharp.Dom;
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
    /// queues (awaiting, holding no thread) instead of exhausting memory; <c>0</c> means <c>max(2, ProcessorCount)</c>.
    /// </summary>
    public int MaxConcurrentReductions { get; set; }

    /// <summary>
    /// The last-resort safety net, in milliseconds: a reduction still running after this long is abandoned with a
    /// server error (<see cref="RichTextReductionAbortedException"/>), never a refusal reason. What the reduction
    /// accepts or refuses does <b>not</b> depend on it: those are the deterministic bounds of
    /// <see cref="RichTextGuardLimits"/>, which keep legitimate input orders of magnitude inside this.
    /// </summary>
    public int SafetyNetMilliseconds { get; set; } = 20_000;
}

/// <summary>
/// The reduction was abandoned by the safety net (a wall-clock bound, or the parser nearly out of stack) rather than
/// refused by a deterministic bound: a server defect or an overloaded host, answered as a server error.
/// </summary>
public sealed class RichTextReductionAbortedException : Exception
{
    public RichTextReductionAbortedException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

/// <summary>
/// <see cref="IRichTextReducer"/> on AngleSharp's WHATWG parser (ADR-0027 decision 5, plan-backend § 5): bound the raw
/// string structurally (deterministic, before the parser sees it), normalise, parse once on a dedicated thread with a
/// large stack, walk the document into the canonical model, write it under the output budget.
/// </summary>
internal sealed class HtmlRichTextReducer : IRichTextReducer, IDisposable
{
    // The explicit <body> puts the tree builder in the "in body" mode a fragment parse with a body context starts
    // in, and a document parse is near-linear where ParseFragment is quadratic in siblings (plan § 6.1).
    private const string Wrapper = "<!DOCTYPE html><html><head></head><body>";

    /// <summary>Each position of a table is at least <c>&lt;td&gt;&lt;p&gt;&lt;/p&gt;&lt;/td&gt;</c>.</summary>
    private const int MinCellMarkup = 16;

    /// <summary>
    /// The parser's stack. The tree builder recurses once per preformatted start tag in a run and per nested template;
    /// the guard bounds both far below what this holds (about 4 KB a level), and the stack check in the element hook
    /// is the net under that. The memory is reserved, not committed.
    /// </summary>
    private const int ParserStackBytes = 32 * 1024 * 1024;

    private readonly RichTextVocabulary vocabulary;
    private readonly RichTextGuardLimits guard;
    private readonly SemaphoreSlim gate;
    private readonly TimeSpan safetyNet;

    public HtmlRichTextReducer(RichTextVocabulary vocabulary, RichTextOptions? options = null, RichTextGuardLimits? guard = null)
    {
        options ??= new RichTextOptions();
        this.vocabulary = vocabulary;
        this.guard = guard ?? RichTextGuardLimits.Default;
        var slots = options.MaxConcurrentReductions > 0 ? options.MaxConcurrentReductions : Math.Max(2, Environment.ProcessorCount);
        gate = new SemaphoreSlim(slots, slots);
        safetyNet = TimeSpan.FromMilliseconds(Math.Max(1, options.SafetyNetMilliseconds));
    }

    /// <summary>The synchronous form, for callers with no request to keep free (tests and tools).</summary>
    public RichTextReduction Reduce(string html, RichTextReductionLimits limits, CancellationToken cancellationToken) =>
        ReduceAsync(html, limits, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

    public async Task<RichTextReduction> ReduceAsync(string html, RichTextReductionLimits limits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(limits);

        var text = Normalise(html);

        // The deterministic structural bounds come first, on the caller's thread: linear, no parser, no slot.
        if (!RichTextInputGuard.IsWithinBounds(text, guard))
        {
            return RichTextReduction.OverTheBound;
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var done = new TaskCompletionSource<RichTextReduction>(TaskCreationOptions.RunContinuationsAsynchronously);

            var thread = new Thread(
                () =>
                {
                    try
                    {
                        done.SetResult(ReduceCore(text, limits.MaxMarkupCodePoints, cancellationToken));
                    }
                    catch (Exception exception)
                    {
                        done.SetException(exception);
                    }
                },
                ParserStackBytes)
            {
                IsBackground = true,
                Name = "rich-text-reduction",
            };

            thread.Start();

            return await done.Task.ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose() => gate.Dispose();

    private RichTextReduction ReduceCore(string text, int limit, CancellationToken cancellationToken)
    {
        using var net = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        net.CancelAfter(safetyNet);

        IHtmlDocument document;

        try
        {
            var watcher = new TreeWatcher(guard);
            var options = new HtmlParserOptions { IsScripting = true, OnCreated = watcher.Created };
            document = new HtmlParser(options).ParseDocumentAsync(Wrapper + text, net.Token).GetAwaiter().GetResult();
        }
        catch (RichTextBudgetExceededException)
        {
            return RichTextReduction.OverTheBound;
        }
        catch (InsufficientExecutionStackException exception)
        {
            throw new RichTextReductionAbortedException("The parser nearly ran out of stack.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RichTextReductionAbortedException("The reduction passed its safety net.", exception);
        }

        try
        {
            // A frameset replaces the body; nothing of it is kept.
            if (document.Body is null)
            {
                return new RichTextReduction(false, string.Empty, 0, 0, false);
            }

            var converter = new DomConverter(vocabulary, limit / MinCellMarkup, guard.MaxDomDepth, net.Token);
            var blocks = converter.Convert(document.Body);

            var writer = new CanonicalWriter(vocabulary, limit);
            writer.Write(blocks);

            // US-4 "Limits come before the empty rule": a value with nothing in it is stored as "" and its size is
            // what was read; a value with content is bounded by what is written (BR-RT-7 (2): the reduced markup).
            if (!writer.HasContent && writer.MarkupLength + converter.DroppedWhitespace > limit)
            {
                return RichTextReduction.OverTheBound;
            }

            return new RichTextReduction(
                false, writer.Html, (int)writer.MarkupLength, (int)writer.VisibleLength, writer.HasContent);
        }
        catch (RichTextBudgetExceededException)
        {
            return RichTextReduction.OverTheBound;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RichTextReductionAbortedException("The reduction passed its safety net.", exception);
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
