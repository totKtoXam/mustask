using Gridify;
using Mustask.Application.Abstractions.Dictionaries;
using Mustask.Application.Features.Dictionaries.GetLanguages;

namespace Mustask.Application.Tests.Features.Dictionaries.GetLanguages;

/// <summary>"Список языков": straight projection over `ILanguageReadModel`, no language resolution.</summary>
public sealed class GetLanguagesHandlerTests
{
    private static GetLanguagesHandler NewHandler(FakeLanguageReadModel readModel) => new(readModel);

    [Fact]
    public async Task Handle_PassesTheQueryToTheReadModel()
    {
        var readModel = new FakeLanguageReadModel();
        var handler = NewHandler(readModel);
        var query = new GridifyQuery(2, 20, "sortOrder", "isActive=true");

        await handler.Handle(new GetLanguagesQuery(query), CancellationToken.None);

        Assert.Same(query, readModel.LastQuery);
    }

    [Fact]
    public async Task Handle_MapsEachRowToAResponseItem()
    {
        // "Он видит записи с кодом языка, собственным названием,
        // признаками «по умолчанию» и активности; числового значения у языка нет".
        var readModel = new FakeLanguageReadModel
        {
            NextResult = new Paging<LanguageRow>(1, [
                new LanguageRow("ru", "Русский", true, true, 10, "5", DateTimeOffset.UtcNow, "admin"),
            ]),
        };
        var handler = NewHandler(readModel);

        var result = await handler.Handle(new GetLanguagesQuery(new GridifyQuery()), CancellationToken.None);

        Assert.Equal(1, result.Page.Count);
        var item = Assert.Single(result.Page.Data);
        Assert.Equal("ru", item.Code);
        Assert.Equal("Русский", item.Endonym);
        Assert.True(item.IsDefault);
        Assert.True(item.IsActive);
        Assert.Equal(10, item.SortOrder);
        Assert.Equal("5", item.Version);
        Assert.Equal("admin", item.UpdatedBy);
    }

    [Fact]
    public async Task Handle_EmptyPage_ReturnsEmptyList()
    {
        var handler = NewHandler(new FakeLanguageReadModel());

        var result = await handler.Handle(new GetLanguagesQuery(new GridifyQuery()), CancellationToken.None);

        Assert.Equal(0, result.Page.Count);
        Assert.Empty(result.Page.Data);
    }
}
