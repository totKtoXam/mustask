using Mustask.Shared.Errors;

namespace Mustask.Domain.Dictionaries;

/// <summary>
/// Коды ошибок модуля справочников — реестр ADR-0005. Живёт в
/// `Mustask.Domain`, а не рядом со слайсом в `Mustask.Application`:
/// <see cref="Language"/> и <see cref="LanguageCode"/> бросают эти коды из
/// домена, а по Onion домен не может ссылаться на Application.
/// </summary>
/// <remarks>
/// Каждая константа — «код приложения» (ADR-0005, п. 5), помечен
/// <see cref="ErrorCodesAttribute"/> и обязан иметь запись в
/// <c>contracts/errors/dictionaries.json</c> с текстом на русском и
/// казахском (проверяется <c>tests/Mustask.Contracts.Tests</c>).
/// </remarks>
[ErrorCodes]
public static class DictionaryErrorCodes
{
    /// <summary>Верхнеуровневый код любой <see cref="Mustask.Shared.Exceptions.ValidationException"/> модуля.</summary>
    public const string ValidationFailed = "dictionaries.validation_failed";

    /// <summary>Причина поля: обязательное поле запроса пусто или отсутствует.</summary>
    public const string ValueRequired = "dictionaries.value_required";

    /// <summary>Причина поля: значение превышает предел длины (`params.limit`).</summary>
    public const string ValueTooLong = "dictionaries.value_too_long";

    /// <summary>Причина поля `ifMatch`: цель уже существует, `If-Match` не передан.</summary>
    public const string VersionRequired = "dictionaries.version_required";

    /// <summary>Кто-то уже изменил запись (устаревший `If-Match`).</summary>
    public const string VersionConflict = "dictionaries.version_conflict";

    /// <summary>`pageSize` выше потолка ADR-0015 (`params.limit`).</summary>
    public const string InvalidPageSize = "dictionaries.invalid_page_size";

    /// <summary>Поле вне белого списка Gridify (`params.field`).</summary>
    public const string UnknownQueryField = "dictionaries.unknown_query_field";

    /// <summary>`filter`/`orderBy` не разбирается Gridify вовсе (`params.field`) — не путать с полем вне белого списка выше.</summary>
    public const string InvalidQuerySyntax = "dictionaries.invalid_query_syntax";

    /// <summary>Такого языка нет в справочнике.</summary>
    public const string LanguagesNotFound = "dictionaries.languages.not_found";

    /// <summary>Код языка уже занят (`params.code`).</summary>
    public const string LanguagesAlreadyExists = "dictionaries.languages.already_exists";

    /// <summary>Причина поля `code`: длина или форма BCP-47 не соблюдены.</summary>
    public const string LanguagesInvalidCode = "dictionaries.languages.invalid_code";

    /// <summary>Причина поля `endonym`: у активного языка обязан быть эндоним.</summary>
    public const string LanguagesEndonymRequired = "dictionaries.languages.endonym_required";

    /// <summary>Язык по умолчанию нельзя деактивировать.</summary>
    public const string LanguagesDefaultCannotBeDeactivated = "dictionaries.languages.default_cannot_be_deactivated";

    /// <summary>Языком по умолчанию может стать только активный язык (`params.code`).</summary>
    public const string LanguagesDefaultMustBeActive = "dictionaries.languages.default_must_be_active";
}
