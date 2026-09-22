using System.Globalization;
using Mustask.Shared.Errors;

namespace Mustask.Shared.Exceptions;

/// <summary>
/// Базовый класс для всех ожидаемых бизнес-ошибок: домен или application-слой
/// бросает наследника осознанно, а не в результате дефекта. Централизованный
/// маппинг в <c>Mustask.Api/ErrorHandling</c> превращает его в ProblemDetails,
/// используя <see cref="StatusCode"/> — конкретный подкласс HTTP-маппинг не
/// определяет, поэтому новый наследник работает без правок в Api
/// (см. ADR-0004).
/// </summary>
/// <remarks>
/// <b>[ИЗМЕНЕНИЕ vs ADR-0004] Контракт <see cref="Exception.Message"/> —
/// см. ADR-0006.</b> Раньше <c>Message</c> уходил клиенту дословно как текст
/// для показа; теперь он — технический диагностический след (код + параметры,
/// инвариантная культура), печатается везде, где исключение всплывает вне
/// нашего обработчика, но пользователю никогда не показывается. Показываемый
/// текст (<c>detail</c>) строит <c>BusinessExceptionHandler</c> из
/// <c>ErrorCatalog</c> по <see cref="Code"/> и <see cref="Params"/> — сервер
/// не хранит текст рядом с местом броска (ADR-0006). Требование
/// безопасности не ослабляется: в след попадают только код и параметры,
/// которые и так уходят клиенту в ответе.
/// </remarks>
public abstract class MustaskException : Exception
{
    /// <summary>
    /// Машинный код ошибки в формате <c>&lt;module&gt;.&lt;reason&gt;</c>,
    /// например <c>orders.not_found</c>. Уходит клиенту в <c>errorCode</c>
    /// и используется как структурное поле лога.
    /// </summary>
    public string Code { get; }

    /// <summary>HTTP-статус, который задаёт конкретный подкласс.</summary>
    public int StatusCode { get; }

    /// <summary>
    /// Сырые параметры ошибки — уходят клиенту в <c>params</c> и
    /// подставляются обработчиком в текст каталога для <c>detail</c>.
    /// Пуст, если код параметров не принимает.
    /// </summary>
    public ErrorParams Params { get; }

    protected MustaskException(
        string code,
        int statusCode,
        ErrorParams? parameters = null,
        Exception? innerException = null)
        : base(BuildDiagnosticMessage(code, parameters), innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        if (statusCode is < 400 or > 599)
        {
            throw new ArgumentOutOfRangeException(
                nameof(statusCode),
                statusCode,
                "HTTP-статус ошибки должен быть в диапазоне 400..599.");
        }

        Code = code;
        StatusCode = statusCode;
        Params = parameters ?? new ErrorParams();
    }

    /// <summary>
    /// Строит диагностический след для <see cref="Exception.Message"/>:
    /// код плюс параметры в инвариантной культуре, без обращения к каталогу
    /// (каталог живёт в <c>Mustask.Api</c>, сюда вниз по Onion не виден — да и
    /// на пути диагностики каталог не нужен: это не текст для показа).
    /// </summary>
    private static string BuildDiagnosticMessage(string code, ErrorParams? parameters)
    {
        var codeText = string.IsNullOrWhiteSpace(code) ? "?" : code;

        if (parameters is null || parameters.Count == 0)
        {
            return codeText;
        }

        var pairs = parameters
            .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
            .Select(parameter => $"{parameter.Key}={FormatForDiagnostics(parameter.Value)}");

        return $"{codeText} [{string.Join(", ", pairs)}]";
    }

    private static string FormatForDiagnostics(ErrorParam param) => param.Type switch
    {
        ErrorParamType.Text => (string)param.Value,
        ErrorParamType.Number => ((decimal)param.Value).ToString(CultureInfo.InvariantCulture),
        ErrorParamType.Currency => ((decimal)param.Value).ToString(CultureInfo.InvariantCulture),
        ErrorParamType.Date => ((DateOnly)param.Value).ToString("O", CultureInfo.InvariantCulture),
        ErrorParamType.DateTime => ((DateTimeOffset)param.Value).ToString("O", CultureInfo.InvariantCulture),
        _ => throw new NotSupportedException($"Неизвестный тип параметра ошибки: {param.Type}."),
    };
}
