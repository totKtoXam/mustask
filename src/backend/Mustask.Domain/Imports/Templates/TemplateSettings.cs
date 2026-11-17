using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mustask.Domain.Imports.Templates;

/// <summary>The names the template's settings use for their closed sets (api-contract § 2.1). Strings in JSON, as the contract says.</summary>
public static class TemplateTokens
{
    public const string FormatXlsx = "xlsx";
    public const string FormatCsv = "csv";

    public const string SourceColumn = "column";
    public const string SourceConstant = "constant";
    public const string SourceMerge = "merge";

    public const string EmptyKeep = "keep";
    public const string EmptyClear = "clear";

    public const string AbsentError = "error";
    public const string AbsentDrop = "drop";

    public const string PolicyError = "error";
    public const string PolicyDefault = "default";
    public const string PolicyEmpty = "empty";

    public const string IdentifierCreate = "create";
    public const string IdentifierSkip = "skip";
}

/// <summary>Where and how the sample file was read (BR-IE-3); <see cref="Headers"/> are the sample file's header texts (BR-IE-4).</summary>
public sealed record TemplateSource
{
    public string Format { get; init; } = TemplateTokens.FormatXlsx;

    public string? Sheet { get; init; }

    public int HeaderRow { get; init; } = 1;

    public int FirstDataRow { get; init; } = 2;

    public string? Encoding { get; init; }

    public string? Delimiter { get; init; }

    public IReadOnlyList<string> Headers { get; init; } = [];
}

/// <summary>A mapping's source: a column, a constant or a merge pattern (BR-IE-4).</summary>
public sealed record MappingSource
{
    public string Kind { get; init; } = TemplateTokens.SourceColumn;

    public string? Column { get; init; }

    public string? Value { get; init; }

    public string? Pattern { get; init; }
}

/// <summary>One converter of a chain with its parameters (api-contract § 2.1).</summary>
public sealed record ChainStepSettings
{
    public string Converter { get; init; } = string.Empty;

    public Dictionary<string, JsonElement>? Params { get; init; }
}

/// <summary>One field's mapping: a source, a chain, and what an empty cell and an absent field mean (BR-IE-4..-6, -9).</summary>
public sealed record FieldMapping
{
    public Guid Id { get; init; }

    public string Field { get; init; } = string.Empty;

    public MappingSource Source { get; init; } = new();

    public IReadOnlyList<ChainStepSettings> Chain { get; init; } = [];

    public string EmptyCell { get; init; } = TemplateTokens.EmptyKeep;

    public string FieldAbsent { get; init; } = TemplateTokens.AbsentError;
}

/// <summary>One recoding entry: a file value to a target; <see cref="TypeId"/> narrows a state entry to one type (BR-IE-7).</summary>
public sealed record RecodingEntrySettings
{
    public string FileValue { get; init; } = string.Empty;

    public Guid? TypeId { get; init; }

    public string Target { get; init; } = string.Empty;

    public bool OnExport { get; init; }
}

/// <summary>A mapping's recoding table and the policy for a value that is not in it (BR-IE-7).</summary>
public sealed record RecodingTableSettings
{
    public Guid MappingId { get; init; }

    public string Policy { get; init; } = TemplateTokens.PolicyError;

    public string? DefaultValue { get; init; }

    public IReadOnlyList<RecodingEntrySettings> Entries { get; init; } = [];
}

/// <summary>What a row with an empty identifier does (BR-IE-10).</summary>
public sealed record IdentifierSettings
{
    public string EmptyIdentifier { get; init; } = TemplateTokens.IdentifierCreate;
}

/// <summary>
/// A template's settings: the source, the mappings with their chains, the recoding tables, the identifier setting and the columns
/// the template leaves unmapped on purpose (<see cref="IgnoredColumns"/>, BR-IE-10: a column set to «Не переносить» is mapped, and is
/// not taken for the export's «Идентификатор» column). Stored as one document.
/// </summary>
public sealed record TemplateSettings
{
    public TemplateSource Source { get; init; } = new();

    public IReadOnlyList<FieldMapping> Mappings { get; init; } = [];

    public IReadOnlyList<RecodingTableSettings> Recoding { get; init; } = [];

    public IdentifierSettings Identifier { get; init; } = new();

    public IReadOnlyList<string> IgnoredColumns { get; init; } = [];

    /// <summary>The mapping that feeds the field, or <c>null</c>.</summary>
    public FieldMapping? MappingOf(string field) => Mappings.FirstOrDefault(m => string.Equals(m.Field, field, StringComparison.Ordinal));

    /// <summary>The recoding table of a mapping, or <c>null</c>.</summary>
    public RecodingTableSettings? RecodingOf(Guid mappingId) => Recoding.FirstOrDefault(r => r.MappingId == mappingId);
}

/// <summary>The one JSON shape of the stored and served settings: camelCase, enums as strings.</summary>
public static class TemplateJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    public static string Serialize(TemplateSettings settings) => JsonSerializer.Serialize(settings, Options);

    public static TemplateSettings Deserialize(string json) => JsonSerializer.Deserialize<TemplateSettings>(json, Options) ?? new TemplateSettings();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
