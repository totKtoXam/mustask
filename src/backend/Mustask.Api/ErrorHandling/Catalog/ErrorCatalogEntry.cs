using Mustask.Shared.Errors;

namespace Mustask.Api.ErrorHandling.Catalog;

/// <summary>
/// Одна запись каталога <c>contracts/errors/&lt;module&gt;.json</c>, разобранная
/// в память. Единственный источник текста ошибки на обоих языках (ADR-0006).
/// </summary>
/// <param name="Code">Полный код, формат <c>&lt;module&gt;.&lt;reason&gt;</c> (ADR-0005).</param>
/// <param name="Module">Модуль слайса либо <c>"http"</c> для платформенных кодов.</param>
/// <param name="Origin">Платформенный код (назначается по статусу) или код слайса.</param>
/// <param name="Statuses">HTTP-статусы, в которых встречается код. Пустой список — только у кода-«последней надежды».</param>
/// <param name="Params">Имя параметра → закрытая подсказка типа.</param>
/// <param name="Ru">Текст на русском, плейсхолдеры <c>{имя}</c> — ключи <see cref="Params"/>.</param>
/// <param name="Kk">Текст на казахском, тот же набор плейсхолдеров.</param>
/// <param name="Deprecated">Код выведен из обращения, но занят навсегда (ADR-0005).</param>
/// <param name="KkNeedsReview">Казахский текст не вычитан носителем языка (см. contracts/README.md).</param>
public sealed record ErrorCatalogEntry(
    string Code,
    string Module,
    ErrorCatalogOrigin Origin,
    IReadOnlyList<int> Statuses,
    IReadOnlyDictionary<string, ErrorParamType> Params,
    string Ru,
    string Kk,
    bool Deprecated,
    bool KkNeedsReview);
