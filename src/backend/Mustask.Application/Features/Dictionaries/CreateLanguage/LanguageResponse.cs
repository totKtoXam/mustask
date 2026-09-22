namespace Mustask.Application.Features.Dictionaries.CreateLanguage;

/// <summary>
/// Тело ответа `LanguageResponse`. Слайсы не ссылаются друг
/// на друга (`VerticalSliceRules`), поэтому это своя копия формы, а не тип,
/// разделяемый с `GetLanguages`/`UpdateLanguage`/`SetDefaultLanguage`.
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
