using System.Text.Json;

namespace Mustask.Domain.Imports.DryRun;

/// <summary>The result of one row in a dry run (BR-IE-13).</summary>
public static class RowResults
{
    public const string Created = "created";
    public const string Updated = "updated";
    public const string Unchanged = "unchanged";
    public const string Skipped = "skipped";
    public const string Error = "error";
    public const string All = "all";

    public static readonly IReadOnlyList<string> Filters = [All, Created, Updated, Unchanged, Skipped, Error];

    /// <summary>The Russian label of a result (BR-IE-13).</summary>
    public static string LabelOf(string result) => result switch
    {
        Created => "Будет создан",
        Updated => "Будет обновлён",
        Unchanged => "Без изменений",
        Skipped => "Пропущен",
        _ => "Ошибка",
    };
}

/// <summary>One reason or warning on a row; <see cref="Params"/> are the catalog parameters of <see cref="Code"/>.</summary>
public sealed record DryRunReason
{
    public string Severity { get; init; } = "error";

    public string Code { get; init; } = string.Empty;

    public Dictionary<string, string> Params { get; init; } = [];

    public string? Column { get; init; }

    public string? Cell { get; init; }

    public string? Value { get; init; }
}

public sealed record DryRunProject(Guid Id, string Name);

/// <summary>A field the row would write: its new value and, on an update, the old one.</summary>
public sealed record DryRunFieldValue(string Field, string Name, string? Value, string? Was);

public sealed record DryRunRowResult
{
    public int Row { get; init; }

    public string Result { get; init; } = RowResults.Error;

    public string? Title { get; init; }

    public string? Identifier { get; init; }

    public DryRunProject? Project { get; init; }

    public IReadOnlyList<DryRunFieldValue> Fields { get; init; } = [];

    public IReadOnlyList<DryRunReason> Reasons { get; init; } = [];
}

public sealed record DryRunTotals(int Created, int Updated, int Unchanged, int Skipped, int Error)
{
    public int All => Created + Updated + Unchanged + Skipped + Error;

    public static DryRunTotals Of(IEnumerable<DryRunRowResult> rows)
    {
        int created = 0, updated = 0, unchanged = 0, skipped = 0, error = 0;

        foreach (var row in rows)
        {
            switch (row.Result)
            {
                case RowResults.Created: created++; break;
                case RowResults.Updated: updated++; break;
                case RowResults.Unchanged: unchanged++; break;
                case RowResults.Skipped: skipped++; break;
                default: error++; break;
            }
        }

        return new(created, updated, unchanged, skipped, error);
    }
}

/// <summary>The stored JSON of the dry run's columns.</summary>
public static class DryRunJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;
}
