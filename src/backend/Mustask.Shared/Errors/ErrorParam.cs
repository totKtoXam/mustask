using System.Text;

namespace Mustask.Shared.Errors;

/// <summary>
/// Один параметр ошибки: сырое значение плюс подсказка типа. Клиент
/// форматирует значение по своей локали — сервер не форматирует его вообще.
/// Значение хранится как <see cref="object"/>, но конструктор не публичный —
/// создать <see cref="ErrorParam"/> можно только фабричным методом,
/// соответствующим <see cref="ErrorParamType"/>, поэтому связка
/// значение/тип не может рассинхронизироваться.
/// </summary>
public readonly struct ErrorParam : IEquatable<ErrorParam>
{
    private ErrorParam(object value, ErrorParamType type)
    {
        Value = value;
        Type = type;
    }

    /// <summary>Сырое значение — <see cref="string"/>, <see cref="decimal"/>, <see cref="DateOnly"/> или <see cref="DateTimeOffset"/> в зависимости от <see cref="Type"/>.</summary>
    public object Value { get; }

    /// <summary>Закрытая подсказка типа, определяющая, как клиенту форматировать <see cref="Value"/>.</summary>
    public ErrorParamType Type { get; }

    /// <summary>
    /// Пустая строка — допустимое бизнес-значение (например, пустой ввод
    /// пользователя), отклоняется только <see langword="null"/>: бизнес-
    /// ошибка с пустым параметром не должна сама по себе превращаться в 500. Управляющие символы (включая CR/LF)
    /// вырезаются молча, а не отклоняются — значение всё ещё может попасть
    /// в лог одной строкой, где CR/LF подделывали бы границу записи
    /// (CWE-117); отказ ради символа, который клиент никогда не вводит
    /// осознанно, был бы той же излишней ценой, что и отказ на пустой
    /// строке.
    /// </summary>
    public static ErrorParam Text(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new ErrorParam(RemoveControlCharacters(value), ErrorParamType.Text);
    }

    public static ErrorParam Number(decimal value) => new(value, ErrorParamType.Number);

    /// <summary>Денежная сумма в тенге. Отдельный фабричный метод, а не перегрузка <see cref="Number"/> — значение то же (decimal), но подсказка типа другая.</summary>
    public static ErrorParam Money(decimal value) => new(value, ErrorParamType.Currency);

    public static ErrorParam Date(DateOnly value) => new(value, ErrorParamType.Date);

    /// <summary>Момент времени. Хранится как получен — в UTC его переводит рендерер ответа, а не место броска.</summary>
    public static ErrorParam Moment(DateTimeOffset value) => new(value, ErrorParamType.DateTime);

    public bool Equals(ErrorParam other) => Type == other.Type && Value.Equals(other.Value);

    public override bool Equals(object? obj) => obj is ErrorParam other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Value, Type);

    public static bool operator ==(ErrorParam left, ErrorParam right) => left.Equals(right);

    public static bool operator !=(ErrorParam left, ErrorParam right) => !left.Equals(right);

    private static string RemoveControlCharacters(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        var hasControlCharacters = false;

        foreach (var c in value)
        {
            if (char.IsControl(c))
            {
                hasControlCharacters = true;
                break;
            }
        }

        if (!hasControlCharacters)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);

        foreach (var c in value)
        {
            if (!char.IsControl(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
