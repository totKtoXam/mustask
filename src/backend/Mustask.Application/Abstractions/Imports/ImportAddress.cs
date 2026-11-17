using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.Templates;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Abstractions.Imports;

/// <summary>Parses the ids of a route and looks up authors' names; shared by the import slices (a slice never references another).</summary>
public static class ImportAddress
{
    public static Guid Organization(string? value) =>
        AddressedId.TryParse(value, out var id) ? id : throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);

    public static Guid Template(string? value) =>
        Guid.TryParse(value, out var id) ? id : throw new NotFoundException(ImportErrorCodes.TemplateNotFound);

    public static Guid File(string? value) =>
        Guid.TryParse(value, out var id) ? id : throw new NotFoundException(ImportErrorCodes.FileNotFound);

    public static Guid DryRun(string? value) =>
        Guid.TryParse(value, out var id) ? id : throw new NotFoundException(ImportErrorCodes.DryRunNotFound);

    /// <summary>The display names of the authors, <c>null</c> for one the directory does not know.</summary>
    public static async Task<IReadOnlyDictionary<string, string?>> AuthorNamesAsync(
        IUserDirectory directory, IEnumerable<string> authorIds, CancellationToken cancellationToken)
    {
        var ids = authorIds.Distinct(StringComparer.Ordinal).ToList();
        var names = ids.ToDictionary(id => id, _ => (string?)null, StringComparer.Ordinal);

        if (ids.Count == 0)
        {
            return names;
        }

        var found = await directory.LookupAsync(ids, cancellationToken);

        foreach (var (id, user) in found.Known)
        {
            names[id] = user.DisplayName;
        }

        return names;
    }

    /// <summary>The reader's options from a template's source settings.</summary>
    public static Domain.Imports.Source.SourceReadOptions OptionsOf(TemplateSource source, int? maxRows = null) =>
        new(source.Sheet, source.HeaderRow, source.FirstDataRow, source.Encoding, source.Delimiter, maxRows);
}
