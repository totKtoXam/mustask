namespace Mustask.Application.Features.Dictionaries.GetLanguages;

/// <summary>Один элемент списка — форма `LanguageResponse`.</summary>
public sealed record LanguageListItemResponse(
    string Code,
    string Endonym,
    bool IsDefault,
    bool IsActive,
    int SortOrder,
    string Version,
    DateTimeOffset UpdatedAt,
    string UpdatedBy);
