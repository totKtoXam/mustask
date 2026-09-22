namespace Mustask.Shared.Errors;

/// <summary>
/// Закрытый набор подсказок типа для параметра ошибки.
/// Клиенту нечем форматировать неизвестный тип, поэтому расширение набора —
/// осознанное изменение контракта, а не правка на месте (ADR-0006).
/// </summary>
public enum ErrorParamType
{
    /// <summary>Строка, показывается как есть, без форматирования.</summary>
    Text,

    /// <summary>Число без единиц измерения.</summary>
    Number,

    /// <summary>Денежная сумма в тенге, без разделителей разрядов и без символа валюты.</summary>
    Currency,

    /// <summary>Дата без времени (<see cref="System.DateOnly"/>).</summary>
    Date,

    /// <summary>Момент времени (<see cref="System.DateTimeOffset"/>), отдаётся в UTC.</summary>
    DateTime,
}
