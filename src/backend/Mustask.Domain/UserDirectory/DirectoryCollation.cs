using System.Globalization;

namespace Mustask.Domain.UserDirectory;

/// <summary>
/// BR-UP-15's order of display names: the platform ICU collation for culture
/// <c>ru</c> at primary strength — letter case and diacritics ignored (so
/// <c>ё</c> equals <c>е</c>), punctuation, symbols and spaces significant and
/// ranked before digits, digits before Cyrillic, Cyrillic before Latin
/// (ADR-0025 § 9). The projection stores <see cref="SortKey"/> per user and
/// orders a search in SQL by it; consumers merge batched answers with
/// <see cref="Compare"/> — the same comparison, so the two orders agree by
/// construction.
/// </summary>
/// <remarks>
/// The options value was chosen by <c>DirectoryCollationTests</c> (plan § 13,
/// item 8): <c>IgnoreCase | IgnoreNonSpace</c> gives the scenario's order and
/// its keys sort bytewise as <see cref="CompareInfo.Compare(string, string, CompareOptions)"/>
/// does; adding <c>StringSort</c> changed nothing on ICU.
/// </remarks>
public static class DirectoryCollation
{
    /// <summary>The comparison's options: primary strength.</summary>
    public const CompareOptions Options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    private const string CultureName = "ru";

    private static readonly CompareInfo Info = CultureInfo.GetCultureInfo(CultureName).CompareInfo;

    /// <summary>
    /// Identifies the collation that produced a stored key: a runtime with a
    /// different ICU version may produce different keys, and a user row whose
    /// key carries another version gets its key recomputed on its next write,
    /// or by a snapshot's completion if none comes (a deleted or absent user).
    /// </summary>
    public static string Version { get; } = string.Create(
        CultureInfo.InvariantCulture,
        $"{CultureName}/{Info.Version.FullVersion}/{Info.Version.SortId}/{(int)Options}");

    /// <summary>The bytes whose bytewise order is this collation's order.</summary>
    public static byte[] SortKey(string displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);

        return Info.GetSortKey(displayName, Options).KeyData;
    }

    public static int Compare(string? left, string? right) => Info.Compare(left, right, Options);

    /// <summary>
    /// Refuses a runtime whose globalization does not give BR-UP-15's order —
    /// an invariant-globalization runtime (no ICU) above all, where every
    /// comparison is ordinal. Called by <c>Mustask.Api</c> before it starts
    /// (ADR-0025 § 9).
    /// </summary>
    public static void EnsureSupported()
    {
        try
        {
            EnsureOrder();
        }
        catch (Exception ex) when (ex is CultureNotFoundException or TypeInitializationException)
        {
            // Invariant globalization with predefined cultures only: the
            // culture itself does not exist on this runtime.
            throw new InvalidOperationException(
                "The user directory needs ICU globalization with the 'ru' collation (ADR-0025 § 9); this runtime "
                + "has no 'ru' culture. An invariant-globalization runtime (no ICU) cannot run Mustask.Api.",
                ex);
        }
    }

    private static void EnsureOrder()
    {
        var failures = new List<string>();

        if (Compare("ё", "е") != 0)
        {
            failures.Add("\"ё\" does not equal \"е\"");
        }

        if (Compare("Анна", "анна") != 0)
        {
            failures.Add("letter case is not ignored");
        }

        if (Compare("я", "a") >= 0)
        {
            failures.Add("Cyrillic does not sort before Latin");
        }

        if (Compare("-a", "1") >= 0 || Compare("1", "а") >= 0)
        {
            failures.Add("punctuation, digits and letters are not in that order");
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                "The user directory needs ICU globalization with the 'ru' collation (ADR-0025 § 9); this runtime "
                + $"fails it: {string.Join("; ", failures)}. An invariant-globalization runtime (no ICU) cannot run Mustask.Api.");
        }
    }
}
