using System.Collections;
using System.Text.RegularExpressions;

namespace Mustask.Shared.Errors;

/// <summary>
/// Набор параметров ошибки: имя → <see cref="ErrorParam"/>. Поддерживает
/// синтаксис инициализатора коллекции по индексатору —
/// <c>new ErrorParams { ["used"] = ErrorParam.Number(50) }</c> — это
/// единственный предусмотренный способ заполнения: индексатор используется
/// сразу после конструктора, до того как ссылка на экземпляр куда-либо
/// уходит, поэтому мутация здесь не тот риск, что был у
/// <c>ValidationException.Errors</c> —
/// там мутировали уже переданный наружу объект. <see cref="MustaskException"/>
/// хранит переданный экземпляр как есть (см. ADR-0006;
/// «неизменяемость снимка обеспечивается типами, а не копированием») —
/// <see cref="ErrorParam"/> сам по себе readonly-struct с закрытым набором
/// типов значений, поэтому дальнейшая мутация конкретного значения
/// невозможна; риск остаётся только за структурной мутацией уже
/// сконструированного набора (добавление/замена ключа) — от этого
/// защищает дисциплина: экземпляр создаётся и сразу передаётся в
/// конструктор исключения, не сохраняется в переменную для повторного
/// использования.
/// </summary>
public sealed partial class ErrorParams : IEnumerable<KeyValuePair<string, ErrorParam>>
{
    private readonly Dictionary<string, ErrorParam> _values = new(StringComparer.Ordinal);

    /// <summary>Новый пустой набор — эквивалент кода без параметров.</summary>
    public static ErrorParams None => new();

    public int Count => _values.Count;

    public ErrorParam this[string name]
    {
        get => _values[name];
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            // Рендерер (ErrorTextRenderer) подставляет только плейсхолдеры
            // вида {[A-Za-z0-9_]+}: имя вне этого набора никогда не будет
            // сопоставлено с местом в шаблоне, и подстановка тихо не
            // произойдёт — {name} останется в тексте у пользователя, а
            // отказ произойдёт не там и не тогда, где его проще найти
            // Проверяем на границе, а не
            // полагаемся на совпадение с последующим рендером.
            if (!PlaceholderNamePattern().IsMatch(name))
            {
                throw new ArgumentException(
                    $"Имя параметра \"{name}\" не соответствует набору символов плейсхолдера " +
                    "(допустимы латиница, цифры и подчёркивание).",
                    nameof(name));
            }

            _values[name] = value;
        }
    }

    public bool ContainsKey(string name) => _values.ContainsKey(name);

    public bool TryGetValue(string name, out ErrorParam value) => _values.TryGetValue(name, out value);

    public IEnumerator<KeyValuePair<string, ErrorParam>> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    [GeneratedRegex(@"^[A-Za-z0-9_]+$")]
    private static partial Regex PlaceholderNamePattern();
}
