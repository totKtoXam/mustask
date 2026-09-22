using System.Text.Json.Serialization;

namespace Mustask.Api.ErrorHandling;

/// <summary>
/// Форма одной причины ошибки поля в <c>errors</c>: код
/// реестра, свои параметры и русский текст запасного варианта — та же
/// схема, что у ошибки верхнего уровня. <see cref="Params"/>
/// опускается из JSON целиком, если причина без параметров — то же правило,
/// что и у <c>params</c> верхнего уровня.
/// </summary>
public sealed record FieldErrorResponse(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("params"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, ErrorParamResponse>? Params,
    [property: JsonPropertyName("detail")] string Detail);
