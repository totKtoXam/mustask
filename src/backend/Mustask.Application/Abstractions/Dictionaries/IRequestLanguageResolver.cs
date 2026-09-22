namespace Mustask.Application.Abstractions.Dictionaries;

/// <summary>
/// Единственное место, знающее правило `Accept-Language`: полное совпадение тега, затем первичный подтег,
/// только среди активных языков, иначе — язык по умолчанию.
/// `Mustask.Api` разбирает заголовок в упорядоченный список тегов
/// (`StringWithQualityHeaderValue.TryParseList`) — `Mustask.Application` не
/// знает про HTTP.
/// </summary>
public interface IRequestLanguageResolver
{
    Task<ResolvedLanguage> ResolveAsync(IReadOnlyList<string> acceptedTags, CancellationToken cancellationToken);
}

/// <summary>Итог разрешения языка запроса — сам разрешённый язык плюс язык по умолчанию (для отката на язык по умолчанию).</summary>
public sealed record ResolvedLanguage(Guid Id, string Code, Guid DefaultLanguageId, string DefaultLanguageCode);
