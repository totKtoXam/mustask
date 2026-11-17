using Mustask.Application.Abstractions.Imports;
using Mustask.Domain.Identity;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.Conversion;
using Mustask.Domain.Process;
using Mustask.Shared.Exceptions;
using Mustask.Application.Abstractions.Organizations;

namespace Mustask.Application.Features.Imports.GetCatalogue;

public sealed record GetCatalogueQuery(string OrganizationId);

/// <summary>The fields, converters, date formats and limits the constructor offers (api-contract § 1). Static data of the domain.</summary>
public sealed class GetCatalogueHandler(IOrganizationAccess access)
{
    private static readonly CatalogueParamView[] None = [];

    private static readonly CatalogueConverterView[] Converters =
    [
        new(ConverterIds.Trim, "Обрезать пробелы", "text", false, [new("collapse", "bool", false)]),
        new(ConverterIds.Truncate, "Обрезать до длины", "text", false, [new("length", "int", true)]),
        new(ConverterIds.Number, "Число", "number", false, None),
        new(ConverterIds.ExcelDate, "Дата из числа Excel", "date", false, [new("system", "string", false)]),
        new(ConverterIds.DateFormat, "Дата по формату", "date", false, [new("format", "string", true)]),
        new(ConverterIds.IsoDate, "Дата из ISO 8601", "date", false, None),
        new(ConverterIds.Split, "Разделить", "list", false, [new("delimiter", "string", true)]),
        new(ConverterIds.Hyperlink, "URL из гиперссылки", "link", false, None),
        new(ConverterIds.Lookup, "По таблице", "lookup", false, [new("entries", "table", true), new("policy", "string", false), new("defaultValue", "string", false)]),
        new(ConverterIds.Extract, "Извлечь по шаблону", "pattern", false, [new("pattern", "string", true)]),
        new(ConverterIds.Merge, "Объединить", "pattern", true, [new("pattern", "string", true)]),
        new(ConverterIds.Constant, "Константа", "constant", true, [new("value", "string", true)]),
    ];

    private static readonly CatalogueDateFormatView[] DateFormats =
    [
        new(DateFormatIds.DmyDot, "ДД.ММ.ГГГГ"),
        new(DateFormatIds.YmdDash, "ГГГГ-ММ-ДД"),
        new(DateFormatIds.DmySlash, "ДД/ММ/ГГГГ"),
        new(DateFormatIds.MdySlash, "ММ/ДД/ГГГГ"),
    ];

    public Task<CatalogueView> Handle(GetCatalogueQuery query, CancellationToken cancellationToken)
    {
        _ = ImportAddress.Organization(query.OrganizationId);

        if (!access.Holds(PermissionCatalog.ImportsTemplateRead))
        {
            throw new ForbiddenException(ImportErrorCodes.AccessForbidden);
        }

        var limits = new CatalogueLimitsView(
            ImportLimits.MaxFileBytes, ImportLimits.MaxDataRows, ImportLimits.MaxColumns, ImportLimits.MaxSheets, ImportLimits.MaxHeaderRow,
            ImportLimits.MaxTemplates, ImportLimits.MaxChainLength, ImportLimits.MaxRecodingEntries, ImportLimits.MaxPatternLength,
            ImportLimits.PreviewRows, ImportLimits.SampleRows);

        var fields = ImportFieldCatalogue.All.Select(f => new CatalogueFieldView(
            f.Id, f.Name, f.Group, f.Required, f.MultiValued, f.Recoding.ToString().ToLowerInvariant(), f.AllowsEmptyPolicy, f.SupportsClear,
            f.TypeKinds?.Select(k => k.ToWireName()).Order(StringComparer.Ordinal).ToList(), ValueKindName(f.ValueKind))).ToList();

        return Task.FromResult(new CatalogueView(limits, fields, Converters, DateFormats));
    }

    private static string ValueKindName(FieldValueKind kind) => kind switch
    {
        FieldValueKind.Key => "text",
        FieldValueKind.Identifier => "identifier",
        _ => kind.ToString().ToLowerInvariant(),
    };
}
