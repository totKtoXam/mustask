namespace Mustask.Domain.Dictionaries;

/// <summary>
/// Именованные пределы длины полей справочников, общие для всех текущих и
/// будущих справочников этого модуля — не переменная бизнес-величина (нет ни
/// одной формы её изменить без релиза), поэтому константы, а не строка
/// настроек (docs/conventions/backend.md, «Именованные значения», п. 1).
/// </summary>
public static class DictionaryLimits
{
    /// <summary>Максимальная длина бизнес-кода записи справочника (`Code`).</summary>
    public const int CodeMaxLength = 64;

    /// <summary>Максимальная длина эндонима языка (`endonym`).</summary>
    public const int EndonymMaxLength = 200;
}
