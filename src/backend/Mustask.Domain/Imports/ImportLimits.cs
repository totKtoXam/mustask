namespace Mustask.Domain.Imports;

/// <summary>
/// The limits of the import module (doc-017 BR-IE-21, BR-IE-18, BR-IE-9, BR-IE-7 and the file-safety NFR). The spec fixes them as
/// constants of the feature, not as per-deployment settings, so they live next to the concept that owns them.
/// </summary>
public static class ImportLimits
{
    /// <summary>The largest accepted file: 10 MB.</summary>
    public const int MaxFileBytes = 10 * 1024 * 1024;

    /// <summary>The most data rows of a file (and so of a dry run and of a batch).</summary>
    public const int MaxDataRows = 5_000;

    /// <summary>The most columns of a sheet.</summary>
    public const int MaxColumns = 200;

    /// <summary>The most sheets of a workbook.</summary>
    public const int MaxSheets = 50;

    /// <summary>A zip container may expand to this many bytes in total; more is refused (the zip-bomb cap).</summary>
    public const long MaxExpandedBytes = 100L * 1024 * 1024;

    /// <summary>The most entries of the zip container of a workbook.</summary>
    public const int MaxZipEntries = 5_000;

    /// <summary>The header row is within 1..<see cref="MaxHeaderRow"/>.</summary>
    public const int MaxHeaderRow = 50;

    /// <summary>At most this many templates per organization (BR-IE-18).</summary>
    public const int MaxTemplates = 100;

    /// <summary>The longest name of a template, after trimming.</summary>
    public const int MaxTemplateNameLength = 100;

    /// <summary>At most this many converters in one chain (BR-IE-9).</summary>
    public const int MaxChainLength = 10;

    /// <summary>At most this many entries in one recoding or lookup table (BR-IE-7).</summary>
    public const int MaxRecodingEntries = 1_000;

    /// <summary>The longest regular expression (BR-IE-21).</summary>
    public const int MaxPatternLength = 500;

    /// <summary>A regular expression runs at most this long per cell (NFR file safety).</summary>
    public static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>The preview of a dry run: the first rows of the chosen result (BR-IE-13).</summary>
    public const int PreviewRows = 50;

    /// <summary>The sample rows under a header and in a chain's sample.</summary>
    public const int SampleRows = 5;

    /// <summary>An uploaded file and a stored dry run live this long (BR-IE-14).</summary>
    public static readonly TimeSpan FileLifetime = TimeSpan.FromHours(24);
}
