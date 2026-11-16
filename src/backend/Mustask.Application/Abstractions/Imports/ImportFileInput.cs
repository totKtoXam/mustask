using Mustask.Domain.Imports;
using Mustask.Domain.Imports.Source;
using Mustask.Domain.Imports.Templates;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Abstractions.Imports;

/// <summary>The source options a caller sends with a file (api-contract § 3.1, § 3.3); every part is optional.</summary>
public sealed record FileSourceBody(string? Sheet, int? HeaderRow, int? FirstDataRow, string? Encoding, string? Delimiter)
{
    public SourceReadOptions ToOptions(int? maxRows = null) =>
        new(string.IsNullOrWhiteSpace(Sheet) ? null : Sheet, HeaderRow ?? 1, FirstDataRow, EmptyToNull(Encoding), EmptyToNull(Delimiter), maxRows);

    private static string? EmptyToNull(string? value) => string.IsNullOrEmpty(value) ? null : value;
}

/// <summary>The body of the values and chain-sample requests (api-contract § 3.3).</summary>
public sealed record MappingProbeBody(FileSourceBody? Source, FieldMapping? Mapping);

/// <summary>Loads the caller's stored file and runs a mapping over it; shared by the file slices (a slice never references another).</summary>
public static class ImportFileInput
{
    public static async Task<ImportFile> LoadAsync(
        IImportFileRepository files, Guid organizationId, string userId, Guid fileId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await files.FindAsync(organizationId, userId, fileId, now, cancellationToken)
        ?? throw new NotFoundException(ImportErrorCodes.FileNotFound);

    /// <summary>The mapping, or a 400 when the body has none or it breaks the template rules (chain cap, lookup cap, step parameters).</summary>
    public static FieldMapping RequireMapping(MappingProbeBody? body)
    {
        if (body?.Mapping is null)
        {
            throw new ValidationException(
                ImportErrorCodes.ValidationFailed,
                new Dictionary<string, IReadOnlyList<FieldError>> { ["mapping"] = [new FieldError(ImportErrorCodes.MappingSourceRequired)] });
        }

        TemplateValidator.EnsureProbeValid(body.Mapping);

        return body.Mapping;
    }
}
