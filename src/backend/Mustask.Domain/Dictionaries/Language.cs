using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Dictionaries;

/// <summary>
/// Запись открытого справочника языков (ADR-0021 §1, §2). У языка нет
/// перевода — только эндоним, собственное имя на себе самом:
/// «переводить» язык не на что. Ровно один язык в справочнике —
/// <see cref="IsDefault"/> (гарантируется дополнительно частичным уникальным
/// индексом на уровне хранилища).
/// </summary>
public sealed class Language
{
    public Guid Id { get; private set; }

    public string Code { get; private set; } = null!;

    public string Endonym { get; private set; } = string.Empty;

    public bool IsDefault { get; private set; }

    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string UpdatedBy { get; private set; } = null!;

    private Language()
    {
        // EF materialization.
    }

    private Language(Guid id, string code, string endonym, bool isDefault, bool isActive, int sortOrder, DateTimeOffset now, string author)
    {
        Id = id;
        Code = code;
        Endonym = endonym;
        IsDefault = isDefault;
        IsActive = isActive;
        SortOrder = sortOrder;
        UpdatedAt = now;
        UpdatedBy = author;
    }

    /// <summary>
    /// Создание всегда даёт неактивную запись без эндонима — активация
    /// происходит отдельным вызовом (<see cref="Replace"/>) once один
    /// администратор её заполнит.
    /// </summary>
    public static Language Create(Guid id, string code, string? endonym, int sortOrder, DateTimeOffset now, string author)
    {
        var validatedCode = LanguageCode.Create(code);
        var normalizedEndonym = NormalizeEndonym(endonym);

        return new Language(id, validatedCode.Value, normalizedEndonym, isDefault: false, isActive: false, sortOrder, now, author);
    }

    /// <summary>
    /// Восстанавливает запись сида (ADR-0021 §5) в полном, уже согласованном
    /// состоянии — минуя правила создания через API (сид не обязан идти
    /// через них).
    /// </summary>
    public static Language Seed(Guid id, string code, string endonym, bool isDefault, bool isActive, int sortOrder, DateTimeOffset now, string author) =>
        new(id, LanguageCode.FromTrusted(code).Value, endonym, isDefault, isActive, sortOrder, now, author);

    /// <summary>
    /// `PUT /api/dictionaries/languages/{code}`: полная замена изменяемого
    /// состояния одним вызовом. Активация требует непустого эндонима;
    /// деактивация языка по умолчанию запрещена — сам факт "я язык по умолчанию" знает только эта
    /// запись, поэтому проверка здесь, а не в хендлере.
    /// </summary>
    public void Replace(string? endonym, int sortOrder, bool isActive, DateTimeOffset now, string author)
    {
        var normalizedEndonym = NormalizeEndonym(endonym);

        if (isActive && normalizedEndonym.Length == 0)
        {
            throw new ValidationException(
                DictionaryErrorCodes.ValidationFailed,
                new Dictionary<string, IReadOnlyList<FieldError>>
                {
                    ["endonym"] = [new FieldError(DictionaryErrorCodes.LanguagesEndonymRequired)],
                });
        }

        if (!isActive && IsDefault)
        {
            throw new ConflictException(DictionaryErrorCodes.LanguagesDefaultCannotBeDeactivated);
        }

        Endonym = normalizedEndonym;
        SortOrder = sortOrder;
        IsActive = isActive;
        UpdatedAt = now;
        UpdatedBy = author;
    }

    /// <summary>Только активный язык может стать языком по умолчанию.</summary>
    public void AssertCanBecomeDefault()
    {
        if (!IsActive)
        {
            throw new ConflictException(
                DictionaryErrorCodes.LanguagesDefaultMustBeActive,
                new ErrorParams { ["code"] = ErrorParam.Text(Code) });
        }
    }

    public void MakeDefault(DateTimeOffset now, string author)
    {
        IsDefault = true;
        UpdatedAt = now;
        UpdatedBy = author;
    }

    public void ClearDefault(DateTimeOffset now, string author)
    {
        IsDefault = false;
        UpdatedAt = now;
        UpdatedBy = author;
    }

    /// <summary>
    /// The one place that decides what a missing default language means --
    /// if every reader of the dictionary spelled this lookup itself (a bare
    /// `Single`/`SingleOrDefault` predicate), a database left by the
    /// runbook's migrate-without-seed path (`local-migrations.md`) would
    /// answer either a `NullReferenceException` or an undecorated
    /// `InvalidOperationException` depending on which call site got there
    /// first. A missing default is an invariant failure the seed is
    /// supposed to make impossible, not a defect in the caller -- this
    /// throws one descriptive exception naming that, from one place.
    /// </summary>
    public static Language GetDefault(IReadOnlyList<Language> languages) =>
        languages.SingleOrDefault(l => l.IsDefault)
            ?? throw new InvalidOperationException(
                "В справочнике языков нет ни одного языка по умолчанию — сид ещё не отработал.");

    private static string NormalizeEndonym(string? endonym)
    {
        var normalized = (endonym ?? string.Empty).Trim();

        if (normalized.Length > DictionaryLimits.EndonymMaxLength)
        {
            throw new ValidationException(
                DictionaryErrorCodes.ValidationFailed,
                new Dictionary<string, IReadOnlyList<FieldError>>
                {
                    ["endonym"] = [new FieldError(
                        DictionaryErrorCodes.ValueTooLong,
                        new ErrorParams { ["limit"] = ErrorParam.Number(DictionaryLimits.EndonymMaxLength) })],
                });
        }

        return normalized;
    }
}
