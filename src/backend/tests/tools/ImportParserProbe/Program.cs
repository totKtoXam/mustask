using System.Text;
using Mustask.Domain.Imports.Source;
using Mustask.Infrastructure.Imports.Reading;
using Mustask.Shared.Exceptions;

// Usage: ImportParserProbe <file> [--sheet NAME] [--header-row N] [--first-data-row N] [--rows N]
//                                 [--encoding utf-8|windows-1251] [--delimiter ";"|","|tab]
// Reads the file read-only with the import module's real reader and prints the result to the console. Nothing is written anywhere.

Console.OutputEncoding = Encoding.UTF8;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("Usage: ImportParserProbe <file> [--sheet NAME] [--header-row N] [--first-data-row N] [--rows N] [--encoding utf-8|windows-1251] [--delimiter ;|,|tab]");
    return args.Length == 0 ? 2 : 0;
}

var path = args[0];
string? sheet = null;
string? encoding = null;
string? delimiter = null;
var headerRow = 1;
int? firstDataRow = null;
var rowsToShow = 10;

for (var i = 1; i < args.Length; i++)
{
    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");

    switch (args[i])
    {
        case "--sheet":
            sheet = Next();
            break;
        case "--header-row":
            headerRow = int.Parse(Next());
            break;
        case "--first-data-row":
            firstDataRow = int.Parse(Next());
            break;
        case "--rows":
            rowsToShow = int.Parse(Next());
            break;
        case "--encoding":
            encoding = Next();
            break;
        case "--delimiter":
            var value = Next();
            delimiter = value.Equals("tab", StringComparison.OrdinalIgnoreCase) ? "\t" : value;
            break;
        default:
            Console.WriteLine($"Unknown option {args[i]}");
            return 2;
    }
}

if (!File.Exists(path))
{
    Console.WriteLine($"File not found: {path}");
    return 2;
}

var content = await File.ReadAllBytesAsync(path); // read-only: the file is read into memory and never written back or copied

Console.WriteLine($"File: {Path.GetFileName(path)}  ({content.Length:N0} bytes)");

try
{
    var table = new SourceFileReader().Read(content, Path.GetFileName(path), new SourceReadOptions(sheet, headerRow, firstDataRow, encoding, delimiter, rowsToShow));

    Console.WriteLine($"Format: {table.Format}");

    if (table.Sheets.Count > 0)
    {
        Console.WriteLine($"Sheets ({table.Sheets.Count}): {string.Join(" | ", table.Sheets.Select(s => $"«{s}»"))}");
        Console.WriteLine($"Read sheet: «{table.Sheet}»  (date system {table.DateSystem})");
    }
    else
    {
        Console.WriteLine($"Encoding: {table.Encoding} ({(table.EncodingDetected ? "detected" : "given")})  Delimiter: {Describe(table.Delimiter)} ({(table.DelimiterDetected ? "detected" : "given")})");
    }

    Console.WriteLine($"Header row: {table.HeaderRow}  First data row: {table.FirstDataRow}  Data rows: {table.DataRowCount}");
    Console.WriteLine();
    Console.WriteLine($"Headers ({table.Headers.Count}):");

    foreach (var header in table.Headers)
    {
        Console.WriteLine($"  {header.Address,-6} «{header.Text}»");
    }

    Console.WriteLine();
    Console.WriteLine($"First {table.Rows.Count} data row(s):");

    foreach (var row in table.Rows)
    {
        var cells = row.Cells.Select((cell, index) => cell.IsEmpty ? null : $"{cell.Address}={Show(cell)}").Where(text => text is not null);
        Console.WriteLine($"  row {row.Number}: {(row.IsEmpty ? "(empty)" : string.Join("  ", cells))}");
    }

    return 0;
}
catch (ValidationException refusal)
{
    Console.WriteLine();
    Console.WriteLine("REFUSED (nothing was read):");
    Console.WriteLine($"  code: {refusal.Code}");

    foreach (var (field, errors) in refusal.Errors)
    {
        foreach (var error in errors)
        {
            var parameters = string.Join(", ", error.Params.Select(p => $"{p.Key}={p.Value.Value}"));
            Console.WriteLine($"  {field}: {error.Code} {(parameters.Length > 0 ? $"({parameters})" : string.Empty)}");
        }
    }

    return 1;
}

static string Describe(string? delimiter) => delimiter switch { "\t" => "tab", null => "-", _ => $"«{delimiter}»" };

static string Show(SourceCell cell)
{
    var text = cell.Text.Replace("\r", "\\r").Replace("\n", "\\n");
    text = text.Length > 40 ? text[..40] + "…" : text;
    var kind = cell.Kind == SourceCellKind.Text ? string.Empty : $"[{cell.Kind.ToString().ToLowerInvariant()}]";
    var link = cell.Hyperlink is null ? string.Empty : $" <link {cell.Hyperlink}>";
    return $"«{text}»{kind}{link}";
}
