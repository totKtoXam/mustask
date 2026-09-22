namespace Mustask.Application.Features.Dictionaries.SetDefaultLanguage;

/// <summary>`PUT /api/dictionaries/languages/default` .</summary>
public sealed record SetDefaultLanguageCommand(string? Code);
