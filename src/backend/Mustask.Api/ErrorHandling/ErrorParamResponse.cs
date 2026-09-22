using System.Text.Json.Serialization;

namespace Mustask.Api.ErrorHandling;

/// <summary>
/// Форма одного параметра в ответе: сырое значение плюс
/// закрытая подсказка типа. Явные <see cref="JsonPropertyNameAttribute"/> —
/// чтобы форма не зависела от глобальной политики именования JSON.
/// </summary>
public sealed record ErrorParamResponse(
    [property: JsonPropertyName("value")] object Value,
    [property: JsonPropertyName("type")] string Type);
