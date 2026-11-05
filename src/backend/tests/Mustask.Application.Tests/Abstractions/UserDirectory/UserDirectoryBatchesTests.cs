using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Abstractions.UserDirectory;

/// <summary>
/// mod-016 spec § Volumes, US-1 "Past 500 ids the list and the member searches still answer" (mod-014 BR-UP-17): a consumer of
/// the user directory splits above 500 ids into batches and merges — it never refuses. The one definition is
/// <see cref="UserDirectoryBatches"/>; the order of a list that names people is <see cref="UserRefOrder"/>.
/// </summary>
public sealed class UserDirectoryBatchesTests
{
    private static string Id(int i) => $"u-{i:D4}";

    private static FakeUserDirectory DirectoryOf(int count)
    {
        var directory = new FakeUserDirectory();

        for (var i = 0; i < count; i++)
        {
            directory.Add(Id(i), $"Участник {i:D4}");
        }

        return directory;
    }

    [Fact]
    public async Task LookupAll_1200Ids_IsThreeStatements_OfAtMost500_AndMergesKnownAndUnknown()
    {
        var directory = DirectoryOf(1000);
        var ids = Enumerable.Range(0, 1200).Select(Id).ToList();

        var result = await UserDirectoryBatches.LookupAllAsync(directory, ids, CancellationToken.None);

        Assert.Equal([500, 500, 200], directory.LookupSizes);
        Assert.Equal(1000, result.Known.Count);
        Assert.Equal(200, result.Unknown.Count);
        Assert.Equal("Участник 0999", result.Known[Id(999)].DisplayName);
        Assert.Contains(Id(1000), result.Unknown);
    }

    [Fact]
    public async Task LookupAll_ExactlyFiveHundred_IsOneStatement()
    {
        var directory = DirectoryOf(500);

        await UserDirectoryBatches.LookupAllAsync(directory, Enumerable.Range(0, 500).Select(Id), CancellationToken.None);

        Assert.Equal([500], directory.LookupSizes);
    }

    [Fact]
    public async Task LookupAll_501_IsTwoStatements()
    {
        var directory = DirectoryOf(501);

        await UserDirectoryBatches.LookupAllAsync(directory, Enumerable.Range(0, 501).Select(Id), CancellationToken.None);

        Assert.Equal([500, 1], directory.LookupSizes);
    }

    [Fact]
    public async Task LookupAll_RepeatedIds_AreAskedOnce()
    {
        var directory = DirectoryOf(3);

        var result = await UserDirectoryBatches.LookupAllAsync(directory, [Id(0), Id(1), Id(0), Id(0), Id(2)], CancellationToken.None);

        Assert.Equal([3], directory.LookupSizes);
        Assert.Equal(3, result.Known.Count);
    }

    [Fact]
    public async Task LookupAll_NothingToAsk_AsksNothing()
    {
        var directory = DirectoryOf(3);

        var result = await UserDirectoryBatches.LookupAllAsync(directory, [], CancellationToken.None);

        Assert.Empty(directory.LookupSizes);
        Assert.Empty(result.Known);
        Assert.Empty(result.Unknown);
    }

    // US-1 · "a search in «Участник организации» ... finds a member among all 501, at most 20 in name order".
    [Fact]
    public async Task SearchAmong_1200Ids_SplitsTheRestriction_AndMergesAtMostTwentyInOrder_WithMoreMatched()
    {
        var directory = DirectoryOf(1200);
        var ids = Enumerable.Range(0, 1200).Select(Id).ToList();

        var result = await UserDirectoryBatches.SearchAmongAsync(directory, "участник", ids, CancellationToken.None);

        Assert.Equal([500, 500, 200], directory.SearchSizes);
        Assert.Equal(20, result.Users.Count);
        Assert.True(result.MoreMatched);
        Assert.Equal(Enumerable.Range(0, 20).Select(Id), result.Users.Select(user => user.Id));
    }

    [Fact]
    public async Task SearchAmong_AMatchInTheLastBatch_IsFound()
    {
        var directory = DirectoryOf(600);
        directory.Add("u-needle", "Иголкин Уникальный");
        var ids = Enumerable.Range(0, 600).Select(Id).Append("u-needle").ToList();

        var result = await UserDirectoryBatches.SearchAmongAsync(directory, "иголкин", ids, CancellationToken.None);

        Assert.Equal(["u-needle"], result.Users.Select(user => user.Id));
        Assert.False(result.MoreMatched);
    }

    // An empty restriction is nobody, and still goes to the directory once so that its own refusal of a text applies.
    [Fact]
    public async Task SearchAmong_NoIds_AnswersNobody_AskingOnce()
    {
        var directory = DirectoryOf(3);

        var result = await UserDirectoryBatches.SearchAmongAsync(directory, "участник", [], CancellationToken.None);

        Assert.Empty(result.Users);
        Assert.Equal([0], directory.SearchSizes);
    }

    [Fact]
    public async Task SearchAmong_NoIdsAndATextOver100_IsMod014sValidationFailure()
    {
        var directory = DirectoryOf(3);

        await Assert.ThrowsAsync<ValidationException>(
            () => UserDirectoryBatches.SearchAmongAsync(directory, new string('я', 101), [], CancellationToken.None));
    }

    [Fact]
    public async Task SearchAmong_InactiveUsersAreNeverOffered()
    {
        var directory = DirectoryOf(2);
        directory.Add("u-off", "Участник Отключённый", isActive: false);

        var result = await UserDirectoryBatches.SearchAmongAsync(directory, "участник", [Id(0), Id(1), "u-off"], CancellationToken.None);

        Assert.DoesNotContain(result.Users, user => user.Id == "u-off");
    }

    // api-contract § 0.5: known users by display name, then unknown ones last by id.
    [Fact]
    public async Task UserRefOrder_KnownByNameThenId_UnknownLastById()
    {
        var directory = new FakeUserDirectory().Add("b", "Борис").Add("a2", "Анна").Add("a1", "Анна").Add("c", "ёлка");
        var known = (await directory.LookupAsync(["b", "a2", "a1", "c"], CancellationToken.None)).Known;

        var ordered = UserRefOrder.Order(["z-ghost", "b", "a2", "c", "a-ghost", "a1"], id => id, known);

        Assert.Equal(["a1", "a2", "b", "c", "a-ghost", "z-ghost"], ordered);
    }

    // BR-UP-15: ids compare by Unicode code point, not by UTF-16 unit: U+FF5E precedes a supplementary character (U+1F600).
    [Fact]
    public void IdComparer_OrdersByCodePoint_NotByUtf16Unit()
    {
        var supplementary = "\U0001F600";
        var fullwidth = "～";

        Assert.True(DirectoryUserOrder.IdComparer.Compare(fullwidth, supplementary) < 0);
        Assert.True(string.CompareOrdinal(fullwidth, supplementary) > 0, "Ordinal UTF-16 order would put the surrogate first.");
        Assert.True(DirectoryUserOrder.IdComparer.Compare("B", "a") < 0);
        Assert.Equal(0, DirectoryUserOrder.IdComparer.Compare("same", "same"));
    }
}
