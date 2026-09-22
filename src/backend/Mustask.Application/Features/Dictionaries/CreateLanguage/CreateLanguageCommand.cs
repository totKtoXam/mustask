namespace Mustask.Application.Features.Dictionaries.CreateLanguage;

/// <summary>`POST /api/dictionaries/languages` .</summary>
public sealed record CreateLanguageCommand(string? Code, string? Endonym, int? SortOrder);
