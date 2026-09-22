using Gridify;

namespace Mustask.Application.Features.Dictionaries.GetLanguages;

/// <summary>
/// Итог обработчика — просто страница: тело ответа не зависит от языка
/// запроса (у языка нет переводов), поэтому этот хендлер его не
/// резолвит. `Content-Language` при этом всё равно выставляется — заголовком
/// endpoint'а (`DictionaryEndpoints.GetLanguagesAsync`), не
/// здесь.
/// </summary>
public sealed record GetLanguagesResult(Paging<LanguageListItemResponse> Page);
