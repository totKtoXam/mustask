using System.Text.RegularExpressions;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// The comparison of feat-062's non-disclosure tests (doc-016 NFR Non-disclosure, mod-007 NFR): what a user who cannot see project X
/// is answered is compared with what the same data without X answers a user of the twin organization. The two organizations have
/// their own ids and clocks, so an answer is reduced to its shape: every id, instant and cursor is replaced by a marker, and what
/// stays (names, keys, identifiers, counts, totals, order, the presence of a next page) must be equal.
/// </summary>
internal static partial class SightTwin
{
    /// <summary>The answer with its ids, instants and cursor values replaced; the status stays.</summary>
    public static string Shape(Answer answer) =>
        $"{(int)answer.Status} " + CursorValue().Replace(Instant().Replace(Id().Replace(Trace().Replace(answer.Body.GetRawText(), "\"traceId\":\"TRACE\""), "ID"), "T"), "\"nextCursor\":\"CURSOR\"");

    /// <summary>The status, the error code and the detail: what a refusal says, apart from its ids.</summary>
    public static string Refusal(Answer answer) => $"{(int)answer.Status} {answer.ErrorCode} {Id().Replace(answer.Detail ?? string.Empty, "ID")}";

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex Id();

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})?")]
    private static partial Regex Instant();

    [GeneratedRegex("\"traceId\":\"[^\"]*\"")]
    private static partial Regex Trace();

    [GeneratedRegex("\"nextCursor\":\"[^\"]*\"")]
    private static partial Regex CursorValue();
}
