using Mustask.Application.Abstractions.Imports;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.Source;

namespace Mustask.Infrastructure.Imports.Reading;

/// <summary>
/// <see cref="ISourceFileReader"/>: picks the reader by the file's extension (.xlsx or .csv; .xls, .xlsm and anything else are refused,
/// BR-IE-2), applies the size limit and validates the source options before a byte of content is interpreted.
/// </summary>
public sealed class SourceFileReader : ISourceFileReader
{
    public SourceTable Read(byte[] content, string fileName, SourceReadOptions options)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(options);

        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();

        if (extension is not (".xlsx" or ".csv"))
        {
            throw SourceRefusal.Because(ImportErrorCodes.FileUnsupportedFormat);
        }

        if (content.Length > ImportLimits.MaxFileBytes)
        {
            throw SourceRefusal.Because(ImportErrorCodes.FileTooLarge);
        }

        if (content.Length == 0)
        {
            throw SourceRefusal.Because(ImportErrorCodes.FileUnreadable);
        }

        if (options.HeaderRow is < 1 or > ImportLimits.MaxHeaderRow)
        {
            throw SourceRefusal.BadOption("headerRow", ImportErrorCodes.HeaderRowInvalid);
        }

        if (options.FirstDataRow is { } first && (first <= options.HeaderRow || first > 1_048_576))
        {
            throw SourceRefusal.BadOption("firstDataRow", ImportErrorCodes.FirstDataRowInvalid);
        }

        return extension == ".xlsx" ? XlsxSourceReader.Read(content, options) : CsvSourceReader.Read(content, options);
    }
}
