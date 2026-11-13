using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.DryRun;
using Mustask.Domain.Imports.Source;
using Mustask.Domain.Imports.Templates;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Abstractions.Imports;

/// <summary>Renders a catalog reason (<c>imports.row.*</c>) into its Russian text; implemented in the API over the error catalog.</summary>
public interface IReasonText
{
    string Render(string code, IReadOnlyDictionary<string, string> parameters);
}

/// <summary>
/// What the caller may do in the import module, read once per request from the served access (api-contract § 0). The client keeps no copy of
/// the rule: <c>canEdit</c>, <c>canDelete</c>, <c>canCopy</c>, <c>canRun</c>, <c>canCreate</c> are these.
/// </summary>
public sealed record ImportRights(bool IsAdmin, bool Write, bool Run, string UserId, AccessSet Set)
{
    public static async Task<ImportRights> LoadAsync(IOrganizationAccess access, string userId, CancellationToken cancellationToken)
    {
        if (!access.Holds(PermissionCatalog.ImportsTemplateRead))
        {
            throw new ForbiddenException(ImportErrorCodes.AccessForbidden);
        }

        var set = await access.SetAsync(cancellationToken);

        return new ImportRights(
            set.Role == OrganizationRole.Admin,
            set.HoldsAnywhere(PermissionCatalog.ImportsTemplateWrite),
            set.HoldsAnywhere(PermissionCatalog.ImportsImportRun),
            userId,
            set);
    }

    public bool CanCreate => Write;

    public bool CanCopy => Write;

    /// <summary>BR-IE-18: the administrator any template; a manager the ones they authored (and only while they still manage somewhere).</summary>
    public bool CanChange(ImportTemplate template) => IsAdmin || (Write && string.Equals(template.AuthorId, UserId, StringComparison.Ordinal));

    public void EnsureWriteOrRun()
    {
        if (!Write && !Run)
        {
            throw new ForbiddenException(ImportErrorCodes.AccessForbidden);
        }
    }

    public void EnsureRun()
    {
        if (!Run)
        {
            throw new ForbiddenException(ImportErrorCodes.AccessForbidden);
        }
    }

    public void EnsureCreate()
    {
        if (!Write)
        {
            throw new ForbiddenException(ImportErrorCodes.AccessForbidden);
        }
    }
}

public sealed record TemplateSourceBrief(string Format, string? Sheet, string? Encoding, string? Delimiter);

public sealed record TemplateRowView(
    Guid Id, string Name, int Version, TemplateSourceBrief Source, string AuthorId, string? AuthorName, DateTimeOffset UpdatedAt,
    bool CanEdit, bool CanDelete, bool CanCopy, bool CanRun);

public sealed record TemplatePageView(IReadOnlyList<TemplateRowView> Items, int Total, int Page, int PageSize, bool CanCreate, bool CanRun);

public sealed record TemplateView(
    Guid Id, string Name, int Version, string AuthorId, string? AuthorName, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    bool CanEdit, bool CanDelete, bool CanCopy, bool CanRun, TemplateSettings Settings)
{
    public static TemplateView Of(ImportTemplate t, ImportRights rights, string? authorName) =>
        new(t.Id, t.Name, t.Version, t.AuthorId, authorName, t.CreatedAt, t.UpdatedAt, rights.CanChange(t), rights.CanChange(t), rights.CanCopy, rights.Run, t.Settings);
}

public sealed record TemplateColumnsView(Guid Id, string Name, IReadOnlyList<string> Columns);

public sealed record FileColumnView(int Index, string Header, string Address, string? ProposedField);

public sealed record FileCellView(string Text, string Kind, string Address);

public sealed record FileSampleRowView(int Row, IReadOnlyList<FileCellView> Cells);

public sealed record FileReadView(
    string Format, IReadOnlyList<string> Sheets, string? Sheet, int HeaderRow, int FirstDataRow, string? Encoding, string? Delimiter,
    bool EncodingDetected, bool DelimiterDetected, string? DateSystem, int DataRowCount,
    IReadOnlyList<FileColumnView> Columns, IReadOnlyList<FileSampleRowView> SampleRows)
{
    public static FileReadView Of(SourceTable table)
    {
        var columns = table.Headers.Select(h => new FileColumnView(h.Index, h.Text, h.Address, ProposeField(h.Text))).ToList();
        var sample = table.Rows.Take(ImportLimits.SampleRows)
            .Select(r => new FileSampleRowView(r.Number, r.Cells.Select(c => new FileCellView(c.Text, c.Kind.ToString().ToLowerInvariant(), c.Address)).ToList()))
            .ToList();

        return new FileReadView(
            table.Format == SourceFormat.Xlsx ? TemplateTokens.FormatXlsx : TemplateTokens.FormatCsv,
            table.Sheets, table.Sheet, table.HeaderRow, table.FirstDataRow, table.Encoding, table.Delimiter,
            table.EncodingDetected, table.DelimiterDetected, table.DateSystem?.ToString(), table.DataRowCount, columns, sample);
    }

    /// <summary>BR-IE-4: the field whose Russian name or id equals the header, ignoring case and spaces; else <c>null</c> («Не переносить»).</summary>
    public static string? ProposeField(string header)
    {
        var key = Squash(header);

        return ImportFieldCatalogue.All.FirstOrDefault(f => Squash(f.Name) == key || Squash(f.Id) == key)?.Id;
    }

    private static string Squash(string text) => new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();
}

public sealed record FileUploadedView(Guid FileId, string FileName, long Size, DateTimeOffset ExpiresAt, FileReadView Read);

public sealed record FileValueView(string Value, int Rows);

public sealed record FileValuesView(IReadOnlyList<FileValueView> Values, int DistinctCount, bool Exceeded);

public sealed record ChainErrorView(string Code, IReadOnlyDictionary<string, string> Params);

public sealed record ChainStepSampleView(string Converter, string? Value, ChainErrorView? Error);

public sealed record ChainRowSampleView(int Row, string Original, IReadOnlyList<ChainStepSampleView> Steps, string? Result, ChainErrorView? Error);

public sealed record ChainSampleView(IReadOnlyList<ChainRowSampleView> Rows);

public sealed record DryRunReasonView(string Severity, string Code, IReadOnlyDictionary<string, string> Params, string? Column, string? Cell, string? Value, string Text);

public sealed record DryRunRowView(
    int Row, string Result, DryRunItemView Item, DryRunProject? Project, IReadOnlyList<DryRunFieldValue> Fields, IReadOnlyList<DryRunReasonView> Reasons);

public sealed record DryRunView(
    Guid Id, Guid TemplateId, int TemplateVersion, bool Stale, string FileName, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt,
    DryRunTotals Totals, IReadOnlyList<string> Notes, bool Importable, string Result, IReadOnlyList<DryRunRowView> Rows, int RowsShown)
{
    public static DryRunView Of(ImportDryRun run, string result, bool stale, IReasonText texts)
    {
        var filter = RowResults.Filters.Contains(result) ? result : RowResults.All;
        var shown = run.Rows.Where(r => filter == RowResults.All || r.Result == filter).Take(ImportLimits.PreviewRows)
            .Select(r => new DryRunRowView(r.Row, r.Result, new DryRunItemView(r.Title, r.Identifier), r.Project, r.Fields, r.Reasons.Select(x => Reason(x, texts)).ToList()))
            .ToList();
        var notes = run.Notes.ToList();

        return new DryRunView(
            run.Id, run.TemplateId, run.TemplateVersion, stale, run.FileName, run.CreatedAt, run.ExpiresAt, run.Totals, notes,
            !stale && run.Totals.Created + run.Totals.Updated + run.Totals.Unchanged > 0, filter, shown, shown.Count);
    }

    public static DryRunReasonView Reason(DryRunReason r, IReasonText texts) =>
        new(r.Severity, r.Code, r.Params, r.Column, r.Cell, r.Value, texts.Render(r.Code, r.Params));
}

public sealed record DryRunItemView(string? Title, string? Identifier);

public sealed record ReportFile(byte[] Content, string FileName);

public sealed record CatalogueLimitsView(
    int MaxFileBytes, int MaxDataRows, int MaxColumns, int MaxSheets, int MaxHeaderRow, int MaxTemplates, int MaxChainLength,
    int MaxRecodingEntries, int MaxPatternLength, int PreviewRows, int SampleRows);

public sealed record CatalogueFieldView(
    string Id, string Name, string Group, bool Required, bool MultiValued, string Recoding, bool AllowsEmptyPolicy, bool SupportsClear,
    IReadOnlyList<string>? TypeKinds, string ValueKind);

public sealed record CatalogueParamView(string Name, string Kind, bool Required);

public sealed record CatalogueConverterView(string Id, string Name, string Class, bool IsSource, IReadOnlyList<CatalogueParamView> Params);

public sealed record CatalogueDateFormatView(string Id, string Name);

public sealed record CatalogueView(
    CatalogueLimitsView Limits, IReadOnlyList<CatalogueFieldView> Fields, IReadOnlyList<CatalogueConverterView> Converters, IReadOnlyList<CatalogueDateFormatView> DateFormats);
