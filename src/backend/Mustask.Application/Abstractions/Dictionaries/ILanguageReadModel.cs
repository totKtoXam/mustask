using Gridify;

namespace Mustask.Application.Abstractions.Dictionaries;

/// <summary>`GET /api/dictionaries/languages` (слайс `GetLanguages`, ADR-0015).</summary>
public interface ILanguageReadModel
{
    Task<Paging<LanguageRow>> GetPageAsync(IGridifyQuery query, CancellationToken cancellationToken);
}

/// <summary>Одна строка списка языков.</summary>
public sealed record LanguageRow(
    string Code,
    string Endonym,
    bool IsDefault,
    bool IsActive,
    int SortOrder,
    string Version,
    DateTimeOffset UpdatedAt,
    string UpdatedBy);
