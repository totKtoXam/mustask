using Mustask.Shared.Errors;

namespace Mustask.Shared.Exceptions;

/// <summary>
/// Ошибка валидации входных данных: поле → список причин. Единственный
/// подкласс со структурой в теле ответа. HTTP 400.
/// </summary>
/// <remarks>
/// <b>[ИЗМЕНЕНИЕ vs ADR-0004] Форма <see cref="Errors"/> — см.
/// ADR-0006.</b> Раньше значением была <c>string[]</c> (готовый текст на
/// показ); причина ошибки поля теперь — обычный код реестра ADR-0005
/// (<see cref="FieldError"/>: код + собственные параметры), а не вторая
/// семья идентификаторов. Ключ словаря не меняется — технический
/// идентификатор поля запроса, как и раньше.
/// </remarks>
public sealed class ValidationException : MustaskException
{
    private readonly Dictionary<string, IReadOnlyList<FieldError>> _errors;

    public ValidationException(
        string code,
        IReadOnlyDictionary<string, IReadOnlyList<FieldError>> errors,
        Exception? innerException = null)
        : base(code, 400, parameters: null, innerException)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var snapshot = new Dictionary<string, IReadOnlyList<FieldError>>(errors.Count, StringComparer.Ordinal);

        foreach (var (field, fieldErrors) in errors)
        {
            // null здесь превратился бы в "errors": {"field": null} у клиента
            // — ломает контракт (тот же дефект, что чинили в ADR-0004).
            // Отвергаем на границе, а не молча пропускаем.
            if (fieldErrors is null)
            {
                throw new ArgumentException(
                    $"Список причин валидации для поля \"{field}\" не может быть null.",
                    nameof(errors));
            }

            // Копируем список здесь же: источник (вызывающий код) мог бы
            // позже мутировать свой список, и без копии это просочилось бы
            // в уже созданное исключение (та же дисциплина, что была у
            // string[] до этой задачи).
            snapshot[field] = fieldErrors.ToArray();
        }

        _errors = snapshot;
    }

    /// <summary>
    /// Поле → причины. И словарь, и каждый список-значение — независимая
    /// копия на каждое обращение: мутация коллекции (добавление/удаление
    /// поля, замена элемента списка, присваивание нового массива) не влияет
    /// ни на исключение, ни на результат следующего обращения к этому
    /// свойству (та же дисциплина копирования сохраняется для новой формы).
    /// </summary>
    /// <remarks>
    /// <b>Уточнение:</b> копируются структура
    /// словаря и список, но не сами объекты <see cref="FieldError"/> внутри
    /// него — <c>record</c> без глубокого клонирования, и его
    /// <see cref="FieldError.Params"/> — тот же изменяемый
    /// <see cref="ErrorParams"/>, что хранит исключение. Значит
    /// <c>exception.Errors["field"][0].Params["x"] = ...</c> реально меняет
    /// состояние исключения — в отличие от структурной мутации самого
    /// словаря/списка, о которой говорит абзац выше. Это та же дисциплина,
    /// что описана у <see cref="ErrorParams"/>: набор параметров создаётся и
    /// сразу передаётся конструктору (сюда — через <see cref="FieldError"/>),
    /// не сохраняется вызывающим для повторного использования и не
    /// мутируется после. Более честно документировать эту границу, чем
    /// платить за глубокое копирование ради случая, которого дисциплина и
    /// так не допускает.
    /// </remarks>
    public IReadOnlyDictionary<string, IReadOnlyList<FieldError>> Errors =>
        _errors.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<FieldError>)kvp.Value.ToArray(),
            StringComparer.Ordinal);
}
