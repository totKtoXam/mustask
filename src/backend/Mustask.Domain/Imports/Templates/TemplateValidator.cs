using System.Text.Json;
using Mustask.Domain.Imports.Conversion;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Imports.Templates;

/// <summary>Reads a chain step's parameters (api-contract § 2.1) and turns a chain into the engine's steps.</summary>
public static class ChainParams
{
    public static string? Text(ChainStepSettings step, string name) =>
        step.Params is not null && step.Params.TryGetValue(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static int? Int(ChainStepSettings step, string name)
    {
        if (step.Params is null || !step.Params.TryGetValue(name, out var v))
        {
            return null;
        }

        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n))
        {
            return n;
        }

        return v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var parsed) ? parsed : null;
    }

    public static bool Bool(ChainStepSettings step, string name) =>
        step.Params is not null && step.Params.TryGetValue(name, out var v) && v.ValueKind == JsonValueKind.True;

    public static IReadOnlyList<KeyValuePair<string, string>> Entries(ChainStepSettings step)
    {
        var list = new List<KeyValuePair<string, string>>();

        if (step.Params is null || !step.Params.TryGetValue("entries", out var v) || v.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var e in v.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var from = e.TryGetProperty("from", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
            var to = e.TryGetProperty("to", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : string.Empty;

            if (!string.IsNullOrWhiteSpace(from))
            {
                list.Add(new(from, to ?? string.Empty));
            }
        }

        return list;
    }

    /// <summary>The engine's step for the stored one.</summary>
    public static ConverterStep ToEngine(ChainStepSettings step) => step.Converter switch
    {
        ConverterIds.Trim => new(step.Converter, Collapse: Bool(step, "collapse")),
        ConverterIds.Truncate => new(step.Converter, Length: Int(step, "length")),
        ConverterIds.DateFormat => new(step.Converter, Format: Text(step, "format")),
        ConverterIds.Split => new(step.Converter, Separator: Text(step, "delimiter")),
        ConverterIds.Extract => new(step.Converter, Pattern: Text(step, "pattern")),
        ConverterIds.Lookup => new(step.Converter, Entries: Entries(step), Policy: Text(step, "policy") ?? TemplateTokens.PolicyError, Default: Text(step, "defaultValue")),
        _ => new(step.Converter),
    };
}

/// <summary>
/// The structural validation of a template (api-contract § 2.5): every reason at once, keyed by part. The checks that need the organization's
/// data (a recoding target that still qualifies) are the handler's; this one is pure.
/// </summary>
public static class TemplateValidator
{
    private static readonly string[] DateFormats = [DateFormatIds.DmyDot, DateFormatIds.YmdDash, DateFormatIds.DmySlash, DateFormatIds.MdySlash];

    /// <summary>The trimmed name, after checking it.</summary>
    public static string NormalizeName(string? name) => (name ?? string.Empty).Trim();

    /// <summary>Validates and throws a <see cref="ValidationException"/> with all reasons.</summary>
    public static void EnsureValid(string? name, TemplateSettings? settings)
    {
        var errors = Validate(name, settings);

        if (errors.Count > 0)
        {
            throw new ValidationException(
                ImportErrorCodes.ValidationFailed,
                errors.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<FieldError>)pair.Value));
        }
    }

    public static Dictionary<string, List<FieldError>> Validate(string? name, TemplateSettings? settings)
    {
        var errors = new Dictionary<string, List<FieldError>>(StringComparer.Ordinal);
        var trimmed = NormalizeName(name);

        if (trimmed.Length == 0)
        {
            Add(errors, "name", ImportErrorCodes.TemplateNameRequired);
        }
        else if (trimmed.Length > ImportLimits.MaxTemplateNameLength)
        {
            Add(errors, "name", ImportErrorCodes.TemplateNameTooLong);
        }

        settings ??= new TemplateSettings();
        var headers = settings.Source.Headers.Select(h => (h ?? string.Empty).Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        ValidateSource(settings.Source, errors);

        var seenFields = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < settings.Mappings.Count; i++)
        {
            ValidateMapping(settings.Mappings[i], i, headers, seenFields, errors);
        }

        foreach (var required in ImportFieldCatalogue.All.Where(f => f.Required))
        {
            if (!settings.Mappings.Any(m => m.Field == required.Id))
            {
                var p = new ErrorParams { ["field"] = ErrorParam.Text(required.Name) };
                Add(errors, "settings.mappings", ImportErrorCodes.MappingSourceRequired, p);
            }
        }

        for (var i = 0; i < settings.Recoding.Count; i++)
        {
            ValidateRecoding(settings.Recoding[i], i, settings, errors);
        }

        return errors;
    }

    private static void ValidateSource(TemplateSource source, Dictionary<string, List<FieldError>> errors)
    {
        const string key = "settings.source";

        if (source.Format is not (TemplateTokens.FormatXlsx or TemplateTokens.FormatCsv))
        {
            Add(errors, key, ImportErrorCodes.FileUnsupportedFormat);
        }

        if (source.HeaderRow < 1 || source.HeaderRow > ImportLimits.MaxHeaderRow)
        {
            Add(errors, key, ImportErrorCodes.HeaderRowInvalid);
        }

        if (source.FirstDataRow <= source.HeaderRow)
        {
            Add(errors, key, ImportErrorCodes.FirstDataRowInvalid);
        }
    }

    private static void ValidateMapping(
        FieldMapping mapping, int index, HashSet<string> headers, HashSet<string> seenFields, Dictionary<string, List<FieldError>> errors)
    {
        string Key(string part) => $"settings.mappings[{index}].{part}";

        var field = ImportFieldCatalogue.Find(mapping.Field);

        if (field is null)
        {
            Add(errors, Key("field"), ImportErrorCodes.MappingFieldUnknown);
        }
        else if (!seenFields.Add(field.Id))
        {
            Add(errors, Key("field"), ImportErrorCodes.MappingFieldTaken);
        }

        var source = mapping.Source;
        var isSource = false;

        switch (source.Kind)
        {
            case TemplateTokens.SourceColumn:
                if (string.IsNullOrWhiteSpace(source.Column))
                {
                    Add(errors, Key("source"), ImportErrorCodes.MappingSourceRequired);
                }
                else if (!headers.Contains(source.Column.Trim()))
                {
                    Add(errors, Key("source"), ImportErrorCodes.MappingColumnUnknown, ColumnParam(source.Column));
                }

                break;
            case TemplateTokens.SourceConstant:
                isSource = true;

                if (string.IsNullOrWhiteSpace(source.Value))
                {
                    Add(errors, Key("source"), ImportErrorCodes.MappingSourceRequired);
                }

                break;
            case TemplateTokens.SourceMerge:
                isSource = true;

                if (string.IsNullOrWhiteSpace(source.Pattern))
                {
                    Add(errors, Key("source"), ImportErrorCodes.MappingSourceRequired);
                }
                else if (source.Pattern.Length > ImportLimits.MaxPatternLength)
                {
                    Add(errors, Key("source"), ImportErrorCodes.MappingPatternTooLong);
                }
                else
                {
                    foreach (var placeholder in ConverterEngine.MergePlaceholders(source.Pattern).Where(p => !headers.Contains(p.Trim())))
                    {
                        Add(errors, Key("source"), ImportErrorCodes.MappingColumnUnknown, ColumnParam(placeholder));
                    }
                }

                break;
            default:
                Add(errors, Key("source"), ImportErrorCodes.MappingSourceRequired);
                break;
        }

        if (mapping.Chain.Count > ImportLimits.MaxChainLength)
        {
            Add(errors, Key("chain"), ImportErrorCodes.MappingChainTooLong);
        }

        for (var i = 0; i < mapping.Chain.Count; i++)
        {
            ValidateStep(mapping.Chain[i], isSource, field, Key("chain"), errors);
        }

        if (mapping.EmptyCell == TemplateTokens.EmptyClear && field is { SupportsClear: false })
        {
            Add(errors, Key("emptyCell"), ImportErrorCodes.MappingClearNotAllowed);
        }
    }

    private static void ValidateStep(ChainStepSettings step, bool sourceMapping, ImportFieldDefinition? field, string key, Dictionary<string, List<FieldError>> errors)
    {
        if (sourceMapping || !ConverterIds.Steps.Contains(step.Converter))
        {
            Add(errors, key, ImportErrorCodes.MappingConverterUnknown);
            return;
        }

        switch (step.Converter)
        {
            case ConverterIds.Truncate:
                if (ChainParams.Int(step, "length") is not > 0)
                {
                    Add(errors, key, ImportErrorCodes.MappingConverterParamInvalid);
                }

                break;
            case ConverterIds.DateFormat:
                if (!DateFormats.Contains(ChainParams.Text(step, "format")))
                {
                    Add(errors, key, ImportErrorCodes.MappingConverterParamInvalid);
                }

                break;
            case ConverterIds.ExcelDate:
                if (step.Params is not null && step.Params.ContainsKey("system") && ChainParams.Text(step, "system") is not ("1900" or "1904") && ChainParams.Int(step, "system") is not (1900 or 1904))
                {
                    Add(errors, key, ImportErrorCodes.MappingConverterParamInvalid);
                }

                break;
            case ConverterIds.Split:
                if (string.IsNullOrEmpty(ChainParams.Text(step, "delimiter")))
                {
                    Add(errors, key, ImportErrorCodes.MappingConverterParamInvalid);
                }

                if (field is not null)
                {
                    Add(errors, key, ImportErrorCodes.MappingListIntoSingle, new ErrorParams { ["field"] = ErrorParam.Text(field.Name) });
                }

                break;
            case ConverterIds.Extract:
                var pattern = ChainParams.Text(step, "pattern");

                if (string.IsNullOrEmpty(pattern))
                {
                    Add(errors, key, ImportErrorCodes.MappingConverterParamInvalid);
                }
                else if (pattern.Length > ImportLimits.MaxPatternLength)
                {
                    Add(errors, key, ImportErrorCodes.MappingPatternTooLong);
                }
                else if (!ConverterEngine.IsValidPattern(pattern))
                {
                    Add(errors, key, ImportErrorCodes.MappingPatternInvalid);
                }

                break;
            case ConverterIds.Lookup:
                var entries = ChainParams.Entries(step);
                var policy = ChainParams.Text(step, "policy") ?? TemplateTokens.PolicyError;

                if (entries.Count == 0 || policy is not (TemplateTokens.PolicyError or TemplateTokens.PolicyDefault or TemplateTokens.PolicyEmpty))
                {
                    Add(errors, key, ImportErrorCodes.MappingConverterParamInvalid);
                }
                else if (entries.Count > ImportLimits.MaxRecodingEntries)
                {
                    Add(errors, key, ImportErrorCodes.RecodingTooManyEntries);
                }
                else if (policy == TemplateTokens.PolicyDefault && ChainParams.Text(step, "defaultValue") is null)
                {
                    Add(errors, key, ImportErrorCodes.RecodingDefaultRequired);
                }

                break;
        }
    }

    private static void ValidateRecoding(RecodingTableSettings table, int index, TemplateSettings settings, Dictionary<string, List<FieldError>> errors)
    {
        var key = $"settings.recoding[{index}]";
        var mapping = settings.Mappings.FirstOrDefault(m => m.Id == table.MappingId);
        var field = mapping is null ? null : ImportFieldCatalogue.Find(mapping.Field);

        if (field is null || field.Recoding == RecodingKind.None)
        {
            Add(errors, key, ImportErrorCodes.MappingFieldUnknown);
            return;
        }

        if (table.Entries.Count > ImportLimits.MaxRecodingEntries)
        {
            Add(errors, key, ImportErrorCodes.RecodingTooManyEntries);
        }

        switch (table.Policy)
        {
            case TemplateTokens.PolicyDefault:
                if (string.IsNullOrWhiteSpace(table.DefaultValue))
                {
                    Add(errors, key, ImportErrorCodes.RecodingDefaultRequired);
                }

                break;
            case TemplateTokens.PolicyEmpty:
                if (!field.AllowsEmptyPolicy)
                {
                    Add(errors, key, ImportErrorCodes.RecodingEmptyNotAllowed);
                }

                break;
            case TemplateTokens.PolicyError:
                break;
            default:
                Add(errors, key, ImportErrorCodes.RecodingDefaultRequired);
                break;
        }

        foreach (var entry in table.Entries)
        {
            if (!ValidTargetShape(field.Recoding, entry.Target))
            {
                Add(errors, key, ImportErrorCodes.RecodingTargetUnknown);
                break;
            }
        }

        if (table.Policy == TemplateTokens.PolicyDefault && table.DefaultValue is not null && !ValidTargetShape(field.Recoding, table.DefaultValue))
        {
            Add(errors, key, ImportErrorCodes.RecodingTargetUnknown);
        }
    }

    /// <summary>The shape of a target: a priority 1..4, or a GUID for the other kinds.</summary>
    public static bool ValidTargetShape(RecodingKind kind, string? target) => kind switch
    {
        RecodingKind.Priority => int.TryParse(target, out var n) && n is >= 1 and <= 4,
        _ => Guid.TryParse(target, out _),
    };

    private static ErrorParams ColumnParam(string column) => new() { ["column"] = ErrorParam.Text(column.Trim()) };

    private static void Add(Dictionary<string, List<FieldError>> errors, string key, string code, ErrorParams? parameters = null)
    {
        if (!errors.TryGetValue(key, out var list))
        {
            errors[key] = list = [];
        }

        list.Add(new FieldError(code, parameters ?? ErrorParams.None));
    }
}
