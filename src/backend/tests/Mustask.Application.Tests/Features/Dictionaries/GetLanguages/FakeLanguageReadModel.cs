using Gridify;
using Mustask.Application.Abstractions.Dictionaries;

namespace Mustask.Application.Tests.Features.Dictionaries.GetLanguages;

/// <summary>
/// Локальный фейк <see cref="ILanguageReadModel"/> для слайса `GetLanguages`.
/// Этот порт нужен только этому use case — `CreateLanguage`/`UpdateLanguage`/
/// `SetDefaultLanguage` работают через `ILanguageRepository`, — поэтому фейк
/// не выносится в общий `TestDoubles/`.
/// </summary>
public sealed class FakeLanguageReadModel : ILanguageReadModel
{
    public Paging<LanguageRow> NextResult { get; set; } = new(0, []);

    public IGridifyQuery? LastQuery { get; private set; }

    public Task<Paging<LanguageRow>> GetPageAsync(IGridifyQuery query, CancellationToken cancellationToken)
    {
        LastQuery = query;
        return Task.FromResult(NextResult);
    }
}
