using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.UserDirectory;
using Mustask.Shared.Exceptions;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014, tests added at QA (qa.md, Backend, "Additional scenarios"), the read model's part: the 20/21 edge of a search
/// answer and repeats counting toward the 500 of a restriction. The push channel's own scenarios went with the channel
/// (tech-025, ADR-0034).
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class UserDirectoryQaTests(MustaskApiFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => ResetAsync(factory);

    public Task DisposeAsync() => ResetAsync(factory);

    // qa.md A-10: exactly 20 matches is not "more matched"; 21 is.
    [Theory]
    [InlineData(20, false)]
    [InlineData(21, true)]
    public async Task Search_AtTheEdgeOfTheAnswerSize_SaysMoreMatchedOnlyAboveTwenty(int matches, bool moreMatched)
    {
        await SeedAsync(factory, Enumerable.Range(0, matches).Select(i => Person(NewId(), "Край", $"Ответа{i:D2}", null)));

        var result = await SearchAsync(factory, new UserSearch("край"));

        Assert.Equal(UserDirectoryLimits.SearchAnswerSize, result.Users.Count);
        Assert.Equal(moreMatched, result.MoreMatched);
    }

    // qa.md A-6, BR-UP-14: a repeated id counts toward the 500 — 501 entries made of repeats are refused,
    // 500 entries with repeats are answered like their distinct ids.
    [Fact]
    public async Task Search_RestrictionOfRepeatedIds_CountsEveryEntryTowardTheLimit()
    {
        var anna = NewId();
        await SeedAsync(factory, [Person(anna, "Анна", "Повторова", null)]);
        var distinct = Enumerable.Range(0, 249).Select(_ => NewId()).Append(anna).ToList();

        var fiveHundredAndOne = distinct.Concat(distinct).Append(anna).ToList();
        var ex = await Assert.ThrowsAsync<ValidationException>(() => SearchAsync(factory, new UserSearch("анна", AmongIds: fiveHundredAndOne)));
        Assert.Equal(UserDirectoryErrorCodes.TooManyIds, Assert.Single(ex.Errors["amongIds"]).Code);

        var fiveHundred = distinct.Concat(distinct).ToList();
        var answered = await SearchAsync(factory, new UserSearch("анна", AmongIds: fiveHundred));
        Assert.Equal(anna, Assert.Single(answered.Users).Id);
    }
}
