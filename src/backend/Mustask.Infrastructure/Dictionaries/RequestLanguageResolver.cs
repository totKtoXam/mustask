using Mustask.Application.Abstractions.Dictionaries;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Dictionaries;

/// <summary>
/// Единственное место, знающее правило `Accept-Language`. Один запрос на
/// ≤ 10 строк, дальше — сравнение в памяти; `Mustask.Api` уже разобрал заголовок в
/// упорядоченный по качеству список тегов.
/// </summary>
public sealed class RequestLanguageResolver(MustaskDbContext context) : IRequestLanguageResolver
{
    public async Task<ResolvedLanguage> ResolveAsync(IReadOnlyList<string> acceptedTags, CancellationToken cancellationToken)
    {
        var languages = await context.Languages.AsNoTracking().ToListAsync(cancellationToken);

        return Resolve(languages, acceptedTags);
    }

    /// <summary>
    /// The whole decision -- once the candidate rows are in
    /// memory, none of it needs a database -- pulled out of
    /// <see cref="ResolveAsync"/> so `Mustask.Infrastructure.Tests` can exercise
    /// the quality-order walk, the inactive-language skip and a malformed/
    /// empty tag list directly, without Docker.
    /// </summary>
    internal static ResolvedLanguage Resolve(IReadOnlyList<Domain.Dictionaries.Language> languages, IReadOnlyList<string> acceptedTags)
    {
        // The descriptive-failure lookup lives on the aggregate
        // (Language.GetDefault) so every caller shares one message instead
        // of ad hoc spellings of the same check.
        var defaultLanguage = Domain.Dictionaries.Language.GetDefault(languages);

        // Для каждого тега, в порядке качества (уже отсортирован вызывающим
        // кодом, `RequestLanguageHeader.ParseAcceptedTags`): сначала полное
        // совпадение, затем — первичный подтег (`ru-RU` -> `ru`); только
        // среди активных языков; первый успех останавливает поиск.
        foreach (var tag in acceptedTags)
        {
            var normalized = (tag ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized.Length == 0)
            {
                continue;
            }

            var byFullTag = languages.FirstOrDefault(l => l.IsActive && l.Code == normalized);
            if (byFullTag is not null)
            {
                return ToResolved(byFullTag, defaultLanguage);
            }

            var primarySubtag = PrimarySubtag(normalized);
            if (primarySubtag != normalized)
            {
                var byPrimarySubtag = languages.FirstOrDefault(l => l.IsActive && l.Code == primarySubtag);
                if (byPrimarySubtag is not null)
                {
                    return ToResolved(byPrimarySubtag, defaultLanguage);
                }
            }
        }

        return ToResolved(defaultLanguage, defaultLanguage);
    }

    private static string PrimarySubtag(string normalizedTag)
    {
        var dashIndex = normalizedTag.IndexOf('-');
        return dashIndex < 0 ? normalizedTag : normalizedTag[..dashIndex];
    }

    private static ResolvedLanguage ToResolved(Domain.Dictionaries.Language language, Domain.Dictionaries.Language defaultLanguage) =>
        new(language.Id, language.Code, defaultLanguage.Id, defaultLanguage.Code);
}
