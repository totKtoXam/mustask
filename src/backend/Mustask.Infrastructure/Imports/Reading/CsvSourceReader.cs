using System.Text;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.Source;

namespace Mustask.Infrastructure.Imports.Reading;

/// <summary>
/// Reads a .csv (doc-017 BR-IE-2): UTF-8 with or without a BOM, or Windows-1251; the delimiter <c>;</c>, <c>,</c> or a tab; quoted
/// fields per RFC 4180, line breaks inside quotes included. Encoding and delimiter are detected unless given. Every value is text.
/// A record is a row: its number counts records, not physical lines.
/// </summary>
internal static class CsvSourceReader
{
    private const int DetectionRecords = 50;

    public static SourceTable Read(byte[] content, SourceReadOptions options)
    {
        if (Array.IndexOf(content, (byte)0, 0, Math.Min(content.Length, 8_192)) >= 0)
        {
            // A zip, a binary workbook or any other binary renamed .csv.
            throw SourceRefusal.Because(ImportErrorCodes.FileUnreadable);
        }

        var (text, encoding, encodingDetected) = Decode(content, options.Encoding);
        var delimiter = options.Delimiter is null ? Detect(text) : ParseDelimiter(options.Delimiter);
        var firstDataRow = options.FirstDataRow ?? options.HeaderRow + 1;
        var maxRecords = firstDataRow + ImportLimits.MaxDataRows;
        var records = Parse(text, delimiter, maxRecords);

        var lastDataRow = 0;
        var width = 0;

        for (var index = 0; index < records.Count; index++)
        {
            var number = index + 1;
            var fields = records[index];
            var used = LastNonEmpty(fields) + 1;

            if (used > ImportLimits.MaxColumns && (number == options.HeaderRow || number >= firstDataRow))
            {
                throw SourceRefusal.Because(ImportErrorCodes.FileTooManyColumns);
            }

            if (number >= firstDataRow && used > 0)
            {
                lastDataRow = number;
            }
        }

        if (lastDataRow - firstDataRow + 1 > ImportLimits.MaxDataRows)
        {
            throw SourceRefusal.Because(ImportErrorCodes.FileTooManyRows);
        }

        var headerFields = options.HeaderRow <= records.Count ? records[options.HeaderRow - 1] : [];
        var headers = new List<SourceHeader>();

        for (var column = 0; column < headerFields.Length; column++)
        {
            var header = headerFields[column].Trim();

            if (header.Length > 0)
            {
                headers.Add(new SourceHeader(column, header, CellAddress.Of(column, options.HeaderRow)));
            }
        }

        if (headers.Count == 0)
        {
            throw SourceRefusal.BadOption("headerRow", ImportErrorCodes.HeaderEmpty);
        }

        width = headers[^1].Index + 1;
        var dataRowCount = Math.Max(0, lastDataRow - firstDataRow + 1);
        var handBack = Math.Min(dataRowCount, options.MaxRows ?? int.MaxValue);
        var rows = new List<SourceRow>(handBack);

        for (var number = firstDataRow; number < firstDataRow + handBack; number++)
        {
            var fields = records[number - 1];
            var cells = new SourceCell[width];

            for (var column = 0; column < width; column++)
            {
                cells[column] = SourceCell.OfText(column < fields.Length ? fields[column] : string.Empty, CellAddress.Of(column, number));
            }

            rows.Add(new SourceRow(number, cells));
        }

        return new SourceTable(
            SourceFormat.Csv,
            [],
            null,
            options.HeaderRow,
            firstDataRow,
            encoding,
            delimiter.ToString(),
            encodingDetected,
            options.Delimiter is null,
            DateSystem: null,
            headers,
            rows,
            dataRowCount);
    }

    private static (string Text, string Encoding, bool Detected) Decode(byte[] content, string? requested)
    {
        var windows1251 = CodePagesEncodingProvider.Instance.GetEncoding(1251)!;
        var offset = content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF ? 3 : 0;

        if (content.Length >= 2 && ((content[0] == 0xFF && content[1] == 0xFE) || (content[0] == 0xFE && content[1] == 0xFF)))
        {
            // UTF-16 is not a supported encoding (BR-IE-2).
            throw SourceRefusal.Because(ImportErrorCodes.FileUnreadable);
        }

        switch (requested)
        {
            case SourceNames.Windows1251:
                return (windows1251.GetString(content, offset, content.Length - offset), SourceNames.Windows1251, false);
            case SourceNames.Utf8:
                return (new UTF8Encoding(false, false).GetString(content, offset, content.Length - offset), SourceNames.Utf8, false);
            case null:
                try
                {
                    return (new UTF8Encoding(false, true).GetString(content, offset, content.Length - offset), SourceNames.Utf8, true);
                }
                catch (DecoderFallbackException)
                {
                    return (windows1251.GetString(content, offset, content.Length - offset), SourceNames.Windows1251, true);
                }

            default:
                throw SourceRefusal.BadOption("encoding", ImportErrorCodes.ValidationFailed);
        }
    }

    private static char ParseDelimiter(string delimiter) => delimiter switch
    {
        SourceNames.Semicolon => ';',
        SourceNames.Comma => ',',
        SourceNames.Tab => '\t',
        _ => throw SourceRefusal.BadOption("delimiter", ImportErrorCodes.ValidationFailed),
    };

    /// <summary>
    /// The candidate whose first records agree best on a field count above one; a tie goes to <c>;</c>, then <c>,</c>, then a tab.
    /// </summary>
    private static char Detect(string text)
    {
        char[] candidates = [';', ',', '\t'];
        var best = ';';
        var bestScore = 0L;

        foreach (var candidate in candidates)
        {
            var counts = Parse(text, candidate, DetectionRecords)
                .Where(record => LastNonEmpty(record) >= 0)
                .Select(record => record.Length)
                .ToList();

            if (counts.Count == 0)
            {
                continue;
            }

            var mode = counts.GroupBy(count => count).OrderByDescending(group => group.Count()).ThenByDescending(group => group.Key).First();

            if (mode.Key <= 1)
            {
                continue;
            }

            var score = (mode.Count() * 1_000L) + mode.Key;

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static int LastNonEmpty(string[] fields)
    {
        for (var i = fields.Length - 1; i >= 0; i--)
        {
            if (fields[i].Length > 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// RFC 4180: a field is quoted when it starts with a quote, <c>""</c> inside is a quote, and a delimiter or a line break inside quotes
    /// belongs to the value. A final line break does not start another record. Stops after <paramref name="maxRecords"/> records.
    /// </summary>
    internal static List<string[]> Parse(string text, char delimiter, int maxRecords)
    {
        var records = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var fieldStarted = false;
        var i = 0;

        while (i < text.Length && records.Count < maxRecords)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }

                    inQuotes = false;
                }
                else
                {
                    field.Append(c);
                }

                i++;
                continue;
            }

            if (c == '"' && field.Length == 0 && !fieldStarted)
            {
                inQuotes = true;
                fieldStarted = true;
                i++;
            }
            else if (c == delimiter)
            {
                fields.Add(field.ToString());
                field.Clear();
                fieldStarted = false;
                i++;
            }
            else if (c is '\r' or '\n')
            {
                fields.Add(field.ToString());
                records.Add([.. fields]);
                fields.Clear();
                field.Clear();
                fieldStarted = false;
                i += c == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
            }
            else
            {
                field.Append(c);
                fieldStarted = true;
                i++;
            }
        }

        if (i >= text.Length && (fields.Count > 0 || field.Length > 0 || fieldStarted) && records.Count < maxRecords)
        {
            fields.Add(field.ToString());
            records.Add([.. fields]);
        }

        return records;
    }
}
