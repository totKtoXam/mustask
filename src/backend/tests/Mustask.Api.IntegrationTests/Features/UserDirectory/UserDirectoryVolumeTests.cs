using System.Diagnostics;
using System.Globalization;
using Mustask.Application.Abstractions.UserDirectory;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014 US-1 "Reads answer in time at the target volume" and US-4 "Reads
/// keep answering during a reconciliation" (NFR Volumes):
/// at 10 000 projected users a search and a 500-id lookup each answer within
/// 500 ms. The live stand repeats it at QA (plan § 13); this is the proof that
/// two <c>LIKE</c>s over the pre-normalised columns need no trigram index at
/// this size (ADR-0025, "Revisit when").
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class UserDirectoryVolumeTests(MustaskApiFactory factory) : IAsyncLifetime
{
    internal const int TargetVolume = 10_000;

    private static readonly TimeSpan ReadBudget = TimeSpan.FromMilliseconds(500);

    private static readonly string[] FirstNames = ["Анна", "Иван", "Мария", "Пётр", "Ольга", "Сергей", "Елена", "Дмитрий", "Alex", "Zoe"];

    private static readonly string[] LastNames = ["Смирнова", "Петров", "Кузнецова", "Волков", "Белова", "Орлов", "Лебедева", "Новиков", "Brown", "Adams"];

    private List<string> _ids = [];

    public async Task InitializeAsync()
    {
        await ResetAsync(factory);
        _ids = await SeedVolumeAsync(factory);
    }

    public Task DisposeAsync() => ResetAsync(factory);

    /// <summary>10 000 users, a tenth of them disabled, names drawn from a small set so a 3-character text matches many.</summary>
    internal static async Task<List<string>> SeedVolumeAsync(MustaskApiFactory factory)
    {
        var pushes = Enumerable.Range(0, TargetVolume)
            .Select(i => Push(
                NewId(),
                FirstNames[i % FirstNames.Length],
                $"{LastNames[(i / FirstNames.Length) % LastNames.Length]}{i.ToString(CultureInfo.InvariantCulture)}",
                $"user{i.ToString(CultureInfo.InvariantCulture)}@volume.test",
                enabled: i % 10 != 0))
            .ToList();

        foreach (var chunk in pushes.Chunk(2_000))
        {
            await SeedAsync(factory, chunk);
        }

        return pushes.Select(push => push.Id).ToList();
    }

    [Fact]
    public async Task SearchAndLookup_AtTenThousandUsers_EachAnswerWithin500Ms()
    {
        Assert.Equal(TargetVolume, await CountAsync(factory));

        // Warm-up: the first call pays for EF's query compilation, not the database.
        await SearchAsync(factory, new UserSearch("анн"));
        await LookupAsync(factory, _ids.Take(500).ToArray());

        var search = await TimedAsync(() => SearchAsync(factory, new UserSearch("мир")));
        var lookup = await TimedAsync(() => LookupAsync(factory, _ids.Skip(1_000).Take(500).ToArray()));

        Assert.Equal(20, search.Result.Users.Count);
        Assert.True(search.Result.MoreMatched);
        Assert.Equal(500, lookup.Result.Known.Count);

        Assert.True(search.Elapsed < ReadBudget, $"A search at {TargetVolume} users took {search.Elapsed.TotalMilliseconds:F0} ms.");
        Assert.True(lookup.Elapsed < ReadBudget, $"A 500-id lookup at {TargetVolume} users took {lookup.Elapsed.TotalMilliseconds:F0} ms.");
    }

    // US-4 "Reads keep answering during a reconciliation of 10 000 users" (NFR Volumes): a whole
    // 20-page snapshot is applied while searches and lookups run; each still answers in time.
    [Fact]
    public async Task SearchAndLookup_DuringATenThousandUserReconciliation_EachAnswerWithin500Ms()
    {
        var push = new PushTestClient(factory);
        var entries = _ids
            .Select((id, i) => Push(id, "Житель", $"Сверенный{i.ToString(CultureInfo.InvariantCulture)}", null, moment: BaseMoment + 1_000 + i))
            .ToList();

        await SearchAsync(factory, new UserSearch("анн"));
        await LookupAsync(factory, _ids.Take(500).ToArray());

        var reconciling = Task.Run(() => push.ReconcileAsync(BaseMoment + 1_000, entries));
        var slowest = TimeSpan.Zero;
        var reads = 0;

        while (!reconciling.IsCompleted)
        {
            var search = await TimedAsync(() => SearchAsync(factory, new UserSearch("жит", IncludeInactive: true)));
            var lookup = await TimedAsync(() => LookupAsync(factory, _ids.Skip(reads * 7 % 9_000).Take(500).ToArray()));

            Assert.Equal(500, lookup.Result.Known.Count);
            slowest = new[] { slowest, search.Elapsed, lookup.Elapsed }.Max();
            reads++;
        }

        var counts = await reconciling;

        Assert.Equal(TargetVolume, counts.Received);
        Assert.Equal(TargetVolume, counts.Changed);
        Assert.Equal(20, counts.PageCount);
        Assert.True(reads > 0, "No read ran while the reconciliation was being applied.");
        Assert.True(slowest < ReadBudget, $"A read during the reconciliation took {slowest.TotalMilliseconds:F0} ms ({reads} read pairs).");
    }

    internal static async Task<(T Result, TimeSpan Elapsed)> TimedAsync<T>(Func<Task<T>> read)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await read();
        stopwatch.Stop();

        return (result, stopwatch.Elapsed);
    }
}
