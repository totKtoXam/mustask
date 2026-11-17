using Mustask.Domain.Imports.Source;

namespace Mustask.Application.Abstractions.Imports;

/// <summary>
/// Reads a .xlsx or .csv file (doc-017 BR-IE-2, BR-IE-3, BR-IE-21). Both calls apply every limit and every file-safety refusal
/// <b>before a row's value is handed back</b>, and refuse the whole file with <see cref="SourceRefusal"/>; formulas are never evaluated
/// and macros never run. Pure over the bytes: no file system, no network.
/// </summary>
public interface ISourceFileReader
{
    /// <summary>
    /// Reads the file as the options say: the sheets, the headers of the header row, the data rows (up to <see cref="SourceReadOptions.MaxRows"/>
    /// handed back) and their full count.
    /// </summary>
    /// <exception cref="Mustask.Shared.Exceptions.ValidationException">The file is refused (reason in <c>errors.file</c>), or an option is bad.</exception>
    SourceTable Read(byte[] content, string fileName, SourceReadOptions options);
}
