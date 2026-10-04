using System.Globalization;
using Mustask.Application.Abstractions.UserDirectory;

namespace Mustask.Application.Tests.Abstractions.UserDirectory;

/// <summary>mod-014 BR-UP-17: merging the answers of one search split into batches of ids.</summary>
public sealed class UserSearchResultMergeTests
{
    private static DirectoryUser User(string id, string displayName) => new(id, displayName, string.Empty, true);

    [Fact]
    public void Merge_DeduplicatesById_AndOrders()
    {
        var first = new UserSearchResult([User("2", "Борис"), User("1", "Анна")], MoreMatched: false);
        var second = new UserSearchResult([User("1", "Анна"), User("3", "Alex")], MoreMatched: false);

        var merged = UserSearchResult.Merge([first, second]);

        Assert.Equal(["1", "2", "3"], merged.Users.Select(user => user.Id).ToArray());
        Assert.False(merged.MoreMatched);
    }

    [Fact]
    public void Merge_MoreThanTwentyCameBack_KeepsTheFirstTwentyAndSaysMoreMatched()
    {
        var batch1 = new UserSearchResult(
            Enumerable.Range(0, 15).Select(i => User($"a{i:D2}", "Анна")).ToList(),
            MoreMatched: false);
        var batch2 = new UserSearchResult(
            Enumerable.Range(0, 15).Select(i => User($"b{i:D2}", "Анна")).ToList(),
            MoreMatched: false);

        var merged = UserSearchResult.Merge([batch2, batch1]);

        Assert.Equal(20, merged.Users.Count);
        Assert.True(merged.MoreMatched);
        Assert.Equal("a00", merged.Users[0].Id);
        Assert.Equal("b04", merged.Users[19].Id);
    }

    [Fact]
    public void Merge_AnyBatchSaidMoreMatched_SaysMoreMatched()
    {
        var merged = UserSearchResult.Merge(
        [
            new UserSearchResult([User("1", "Анна")], MoreMatched: true),
            new UserSearchResult([], MoreMatched: false),
        ]);

        Assert.True(merged.MoreMatched);
        Assert.Single(merged.Users);
    }

    [Fact]
    public void Merge_NoBatches_IsEmpty()
    {
        var merged = UserSearchResult.Merge([]);

        Assert.Empty(merged.Users);
        Assert.False(merged.MoreMatched);
    }

    [Fact]
    public void Merge_OrderEqualsTheSingleSearchOrder()
    {
        var all = Enumerable.Range(0, 30)
            .Select(i => User(i.ToString("D2", CultureInfo.InvariantCulture), i % 2 == 0 ? "Zoe" : "Юлия"))
            .ToList();

        var merged = UserSearchResult.Merge(all.Chunk(7).Select(chunk => new UserSearchResult(chunk, false)));

        Assert.Equal(all.Order(DirectoryUserOrder.Comparer).Take(20).ToArray(), merged.Users.ToArray());
    }
}
