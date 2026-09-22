using System.Text.RegularExpressions;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Dictionaries;

/// <summary>
/// Код языка: BCP-47, 2–8 символов после нормализации (trim + нижний
/// регистр). Справочник языков — открытый (ADR-0021 §2): код вводит
/// администратор, а не выбирает из перечисления кода.
/// </summary>
public sealed partial class LanguageCode : IEquatable<LanguageCode>
{
    public const int MinLength = 2;
    public const int MaxLength = 8;

    [GeneratedRegex(@"^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$", RegexOptions.ExplicitCapture)]
    private static partial Regex Bcp47Pattern { get; }

    public string Value { get; }

    private LanguageCode(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Нормализует и валидирует ввод администратора. Бросает <see cref="ValidationException"/> с полем `code` и
    /// причиной <see cref="DictionaryErrorCodes.LanguagesInvalidCode"/>.
    /// </summary>
    public static LanguageCode Create(string? raw, string fieldName = "code")
    {
        var normalized = (raw ?? string.Empty).Trim().ToLowerInvariant();

        if (normalized.Length is < MinLength or > MaxLength || !Bcp47Pattern.IsMatch(normalized))
        {
            throw new ValidationException(
                DictionaryErrorCodes.ValidationFailed,
                new Dictionary<string, IReadOnlyList<FieldError>>
                {
                    [fieldName] = [new FieldError(DictionaryErrorCodes.LanguagesInvalidCode)],
                });
        }

        return new LanguageCode(normalized);
    }

    /// <summary>Восстанавливает значение из хранилища или из сида — уже нормализовано и проверено CHECK'ом.</summary>
    public static LanguageCode FromTrusted(string value) => new(value);

    public bool Equals(LanguageCode? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as LanguageCode);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
