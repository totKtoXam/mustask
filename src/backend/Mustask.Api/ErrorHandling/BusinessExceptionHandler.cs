using Mustask.Api.ErrorHandling.Catalog;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Mustask.Api.ErrorHandling;

/// <summary>
/// Ядро решения: маппит ожидаемые бизнес-ошибки (<see cref="MustaskException"/>)
/// в ProblemDetails. Статус берётся из <see cref="MustaskException.StatusCode"/>,
/// а не через switch по конкретным типам — новый наследник работает без
/// правок здесь (см. ADR-0004). Не <see cref="MustaskException"/> — не наша
/// ответственность, передаём дальше по цепочке.
/// </summary>
/// <remarks>
/// <b>[ИЗМЕНЕНИЕ vs ADR-0004]</b> Раньше <c>Detail</c> — это
/// <c>mustaskException.Message</c> дословно; теперь единственный источник
/// текста — <see cref="ErrorCatalog"/> (ADR-0006): и верхнеуровневый
/// <c>detail</c>, и <c>detail</c> каждой причины поля рендерятся из каталога
/// по коду и параметрам исключения, а не хранятся рядом с местом броска.
/// </remarks>
public sealed class BusinessExceptionHandler : IExceptionHandler
{
    private readonly ILogger<BusinessExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ErrorCatalog _catalog;

    public BusinessExceptionHandler(
        ILogger<BusinessExceptionHandler> logger,
        IProblemDetailsService problemDetailsService,
        ErrorCatalog catalog)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
        _catalog = catalog;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not MustaskException mustaskException)
        {
            return false;
        }

        if (TryFindMissingCatalogEntry(mustaskException, out var missingCode))
        {
            // Код брошен местом в приложении (слайс, опечатка, код мимо
            // [ErrorCodes] — или рассинхрон с
            // константой), но не зарегистрирован в каталоге. Это дефект
            // приложения, а не ожидаемая бизнес-ошибка — алертит через
            // Error, а не тихий Warning ниже. Ничего не пишем в
            // httpContext и не рендерим из каталога этим кодом: отдаём
            // исключение дальше по цепочке — GlobalExceptionHandler,
            // ничего не зная про код, безусловно построит обезличенный
            // 500 из гарантированно присутствующей записи
            // http.internal_error (её наличие проверяется на старте, см.
            // PlatformErrorCodes.EnsureRequiredCodesArePresent).
            _logger.LogError(
                exception,
                "Error catalog: code {ErrorCode} thrown at {Method} {Path} has no matching catalog entry; falling back to the generic 500 handler.",
                missingCode,
                httpContext.Request.Method,
                httpContext.Request.Path);

            return false;
        }

        var detail = RenderDetail(mustaskException.Code, mustaskException.Params);

        // Ожидаемая ошибка, не дефект приложения: Warning, не алертит.
        // InnerException (если автор слайса его передал) уходит сюда как
        // причина — наружу он не рендерится, конвенция обещает именно это.
        // Собственный стек MustaskException не логируется: у исключения,
        // выброшенного как штатный поток, он диагностической ценности не
        // несёт — поэтому передаётся InnerException, а не mustaskException.
        // Русский текст ошибки уходит в ту же запись журнала вместе с
        // кодом — раньше этого не было.
        //
        // Раньше сюда шёл уже отрендеренный detail — шаблон, склеенный со
        // значениями params. Значения params могут быть персональными данными
        // (ИИН/БИН), и склейка в одну свободную строку — прямой канал для
        // подделки строки лога (CWE-117), даже после того как ErrorParam.Text
        // стал вырезать управляющие символы на входе (оборона в глубину, а не
        // замена друг друга). Вместо этого в лог идут два отдельных структурных
        // поля: сырой (неподставленный) русский шаблон записи каталога — он не
        // содержит ничего, кроме того, что есть в каталоге, — и params тем же
        // типом, что уходит в ответ ({value, type} на каждый параметр), а не
        // текстом внутри сообщения.
        var errorTemplate = _catalog.Get(mustaskException.Code).Ru;
        var logParams = RenderParams(mustaskException.Params);

        _logger.LogWarning(
            mustaskException.InnerException,
            "Business error {ErrorCode} handling {Method} {Path}: {ErrorTemplate} params={ErrorParams}",
            mustaskException.Code,
            httpContext.Request.Method,
            httpContext.Request.Path,
            errorTemplate,
            logParams);

        httpContext.Response.StatusCode = mustaskException.StatusCode;

        var problemDetails = BuildProblemDetails(mustaskException, detail);

        // Статус и решение "обработано" не зависят от результата записи тела:
        // TryWriteAsync возвращает false, если DefaultProblemDetailsWriter не
        // может согласовать content type с заголовком Accept клиента —
        // тогда исключение раньше уходило дальше в
        // GlobalExceptionHandler, который перетирал уже выставленный статус
        // на 500 и писал ложный Error-алерт. Статус, выставленный этим
        // обработчиком выше, — источник истины; так же безусловно поступает
        // GlobalExceptionHandler.
        await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });

        return true;
    }

    private ProblemDetails BuildProblemDetails(MustaskException exception, string detail)
    {
        var problemDetails = new ProblemDetails
        {
            Status = exception.StatusCode,
            Detail = detail,
        };

        problemDetails.Extensions["errorCode"] = exception.Code;

        var renderedParams = RenderParams(exception.Params);

        if (renderedParams is not null)
        {
            problemDetails.Extensions["params"] = renderedParams;
        }

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = BuildErrors(validationException.Errors);
        }

        return problemDetails;
    }

    /// <summary>
    /// Проверяет заранее, до любого побочного эффекта (лог, статус
    /// ответа), все коды, которые ниже пойдут через
    /// <see cref="ErrorCatalog.RenderRu"/> (сам код исключения и, для
    /// <see cref="ValidationException"/>, код каждой причины поля) — чтобы
    /// <see cref="RenderDetail"/> и <see cref="BuildErrors"/> уже не могли
    /// наткнуться на отсутствующую запись и бросить
    /// <see cref="KeyNotFoundException"/> изнутри обработчика.
    /// Возвращает <see langword="true"/> и код первой отсутствующей записи,
    /// если каталог рассинхронизирован с местом броска; <see langword="false"/>,
    /// если все коды на месте.
    /// </summary>
    /// <remarks>
    /// <b>Осознанная цена:</b> проверка
    /// применяется ко всей ошибке целиком, а не к отдельной причине поля —
    /// рассинхрон реестра для одной-единственной причины среди многих
    /// (например, <see cref="ValidationException"/> с пятью полями, из
    /// которых только у одного код опечатан) деградирует весь ответ до
    /// обезличенного <c>http.internal_error</c> (500), а не только эту одну
    /// причину до частичного 400. Это принятое поведение: рассинхрон
    /// реестра — дефект приложения, ответ намеренно 500, а не попытка
    /// правдоподобно собрать частичный 400 из наполовину рассинхронизированного
    /// каталога.
    /// </remarks>
    private bool TryFindMissingCatalogEntry(MustaskException exception, out string missingCode)
    {
        if (!_catalog.TryGet(exception.Code, out _))
        {
            missingCode = exception.Code;
            return true;
        }

        if (exception is ValidationException validationException)
        {
            foreach (var fieldErrors in validationException.Errors.Values)
            {
                foreach (var fieldError in fieldErrors)
                {
                    if (!_catalog.TryGet(fieldError.Code, out _))
                    {
                        missingCode = fieldError.Code;
                        return true;
                    }
                }
            }
        }

        missingCode = string.Empty;
        return false;
    }

    /// <summary>
    /// Рендерит русский текст записи каталога по коду и параметрам.
    /// Расхождение места броска с каталогом (недостающий параметр) не
    /// роняет ответ — уходит отдельной записью в лог (страховка).
    /// </summary>
    private string RenderDetail(string code, ErrorParams parameters)
    {
        var result = _catalog.RenderRu(code, parameters);

        if (result.HasMissingParams)
        {
            _logger.LogError(
                "Error catalog: code {ErrorCode} got no values for placeholders {MissingParams}.",
                code,
                string.Join(", ", result.MissingParamNames));
        }

        return result.Text;
    }

    private static Dictionary<string, ErrorParamResponse>? RenderParams(ErrorParams parameters)
    {
        if (parameters.Count == 0)
        {
            // Ключ "params" отсутствует целиком, а не пуст.
            return null;
        }

        var result = new Dictionary<string, ErrorParamResponse>(StringComparer.Ordinal);

        foreach (var (name, value) in parameters)
        {
            result[name] = new ErrorParamResponse(SerializableValue(value), TypeHint(value.Type));
        }

        return result;
    }

    private Dictionary<string, FieldErrorResponse[]> BuildErrors(
        IReadOnlyDictionary<string, IReadOnlyList<FieldError>> errors)
    {
        var result = new Dictionary<string, FieldErrorResponse[]>(StringComparer.Ordinal);

        foreach (var (field, fieldErrors) in errors)
        {
            result[field] = fieldErrors
                .Select(fieldError => new FieldErrorResponse(
                    fieldError.Code,
                    RenderParams(fieldError.Params),
                    RenderDetail(fieldError.Code, fieldError.Params)))
                .ToArray();
        }

        return result;
    }

    private static object SerializableValue(ErrorParam param) => param.Type switch
    {
        ErrorParamType.Text => (string)param.Value,
        ErrorParamType.Number => (decimal)param.Value,
        ErrorParamType.Currency => (decimal)param.Value,
        ErrorParamType.Date => (DateOnly)param.Value,
        ErrorParamType.DateTime => ((DateTimeOffset)param.Value).ToUniversalTime(),
        _ => throw new NotSupportedException($"Неизвестный тип параметра ошибки: {param.Type}."),
    };

    private static string TypeHint(ErrorParamType type) => type switch
    {
        ErrorParamType.Text => "string",
        ErrorParamType.Number => "number",
        ErrorParamType.Currency => "currency",
        ErrorParamType.Date => "date",
        ErrorParamType.DateTime => "datetime",
        _ => throw new NotSupportedException($"Неизвестный тип параметра ошибки: {type}."),
    };
}
