namespace Mustask.Application.Features.Dictionaries.CreateLanguage;

/// <summary>Итог обработчика — всегда `201`: здесь нет ветки замены существующей записи.</summary>
public sealed record CreateLanguageResult(LanguageResponse Response);
