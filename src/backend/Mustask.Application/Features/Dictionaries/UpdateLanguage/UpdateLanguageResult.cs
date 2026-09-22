namespace Mustask.Application.Features.Dictionaries.UpdateLanguage;

/// <summary>Итог обработчика — всегда `200`: этот `PUT` никогда не создаёт запись, только заменяет уже существующую.</summary>
public sealed record UpdateLanguageResult(LanguageResponse Response);
