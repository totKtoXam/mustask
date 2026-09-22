namespace Mustask.Shared.Errors;

/// <summary>
/// Причина ошибки одного поля в <c>ValidationException.Errors</c> (ADR-0006):
/// обычный код реестра (тот же формат <c>&lt;module&gt;.&lt;reason&gt;</c>,
/// ADR-0005), а не вторая семья идентификаторов. У причины могут быть свои
/// параметры — те же правила, что и у кода верхнего уровня.
/// </summary>
/// <param name="Code">Код причины, формат <c>&lt;module&gt;.&lt;reason&gt;</c>.</param>
/// <param name="Params">Параметры причины; <see cref="ErrorParams.None"/>, если параметров нет.</param>
public sealed record FieldError(string Code, ErrorParams Params)
{
    public FieldError(string code)
        : this(code, ErrorParams.None)
    {
    }
}
