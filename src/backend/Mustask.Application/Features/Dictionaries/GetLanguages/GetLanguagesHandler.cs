using Gridify;
using Mustask.Application.Abstractions.Dictionaries;

namespace Mustask.Application.Features.Dictionaries.GetLanguages;

/// <summary>
/// `GET /api/dictionaries/languages`. A straight projection over
/// <see cref="ILanguageReadModel"/> -- there is no language resolution here:
/// a language has no translations, the endonym is its own name in itself, so
/// `Accept-Language` plays no role in the body.
/// </summary>
public sealed class GetLanguagesHandler(ILanguageReadModel readModel)
{
    public async Task<GetLanguagesResult> Handle(GetLanguagesQuery query, CancellationToken cancellationToken)
    {
        var page = await readModel.GetPageAsync(query.Paging, cancellationToken);

        var items = page.Data
            .Select(row => new LanguageListItemResponse(
                row.Code, row.Endonym, row.IsDefault, row.IsActive, row.SortOrder, row.Version, row.UpdatedAt, row.UpdatedBy))
            .ToList();

        return new GetLanguagesResult(new Paging<LanguageListItemResponse>(page.Count, items));
    }
}
