namespace Mustask.Application.Features.Dictionaries.SetDefaultLanguage;

/// <summary>Итог обработчика — всегда `200`, включая идемпотентный no-op, когда назначаемый язык уже является языком по умолчанию.</summary>
public sealed record SetDefaultLanguageResult(LanguageResponse Response);
