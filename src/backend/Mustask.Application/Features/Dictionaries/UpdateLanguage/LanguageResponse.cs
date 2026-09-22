namespace Mustask.Application.Features.Dictionaries.UpdateLanguage;

/// <summary>
/// Тело ответа `LanguageResponse`. Слайсы не ссылаются друг
/// на друга (`VerticalSliceRules`), поэтому это своя копия формы, а не тип,
/// разделяемый с `GetLanguages`/`CreateLanguage`/`SetDefaultLanguage`.
/// </summary>
public sealed record LanguageResponse(
    string Code,
    string Endonym,
    bool IsDefault,
    bool IsActive,
    int SortOrder,
    string Version,
    DateTimeOffset UpdatedAt,
    string UpdatedBy);
