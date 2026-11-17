using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Infrastructure;
using Mustask.Infrastructure.Images;

namespace Mustask.Api.IntegrationTests.Features.Images;

/// <summary>
/// <see cref="UnboundImageSweeper"/> (feat-078 US-5, BR-IM-16): unbound uploads older than two hours go with their bytes; bound ones,
/// young ones and every work item row stay; the one log line has a count and nothing else. The timer is off in the test hosts
/// (<c>Images:Sweep:Enabled=false</c>), so a run is a call.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class UnboundImageSweeperTests(MustaskApiFactory factory)
{
    private static readonly TimeSpan Old = TimeSpan.FromHours(3);

    private async Task<int> ContentRowsAsync(Guid imageId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().ImageContents.CountAsync(content => content.ImageId == imageId);
    }

    private Task<int> RunAsync(int batchSize = 100, ILogger<UnboundImageSweeper>? logger = null)
    {
        var sweeper = new UnboundImageSweeper(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            new ImagesOptions { Sweep = { BatchSize = batchSize } },
            TimeProvider.System,
            logger ?? NullLogger<UnboundImageSweeper>.Instance);

        return sweeper.RunOnceAsync(CancellationToken.None);
    }

    // US-5 · "Old unbound uploads are removed": the row and its bytes; the young one and the bound one stay.
    [Fact]
    public async Task RunOnce_RemovesOldUnbound_KeepsYoungAndBound_BytesGoWithTheRow()
    {
        var (_, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var old = Guid.CreateVersion7();
        var young = Guid.CreateVersion7();
        var oldBound = Guid.CreateVersion7();
        await ImagesTestData.StoreUnboundAsync(factory, item.Id, old, uploadedAt: DateTimeOffset.UtcNow - Old);
        await ImagesTestData.StoreUnboundAsync(factory, item.Id, young, uploadedAt: DateTimeOffset.UtcNow - TimeSpan.FromMinutes(119));
        await ImagesTestData.StoreUnboundAsync(factory, item.Id, oldBound, uploadedAt: DateTimeOffset.UtcNow - TimeSpan.FromDays(5));
        await ImagesTestData.BindAsync(factory, oldBound);

        var removed = await RunAsync();

        Assert.True(removed >= 1);
        Assert.Null(await ImagesTestData.FindAsync(factory, old));
        Assert.Equal(0, await ContentRowsAsync(old));
        Assert.NotNull(await ImagesTestData.FindAsync(factory, young));
        Assert.Equal(1, await ContentRowsAsync(young));
        Assert.NotNull(await ImagesTestData.FindAsync(factory, oldBound));
        Assert.Equal(1, await ContentRowsAsync(oldBound));
    }

    // US-5 · "just over two hours": the line is the cutoff, a minute either side of it.
    [Theory]
    [InlineData(121, false)]
    [InlineData(119, true)]
    public async Task RunOnce_TwoHourLine(int ageMinutes, bool kept)
    {
        var (_, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var id = Guid.CreateVersion7();
        await ImagesTestData.StoreUnboundAsync(factory, item.Id, id, uploadedAt: DateTimeOffset.UtcNow - TimeSpan.FromMinutes(ageMinutes));

        await RunAsync();

        Assert.Equal(kept, await ImagesTestData.FindAsync(factory, id) is not null);
    }

    // Plan § 8.2 · every batch takes at most BatchSize rows and the run goes on until none is due.
    [Fact]
    public async Task RunOnce_MoreDueThanOneBatch_RemovesAll()
    {
        var (_, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var ids = Enumerable.Range(0, 7).Select(_ => Guid.CreateVersion7()).ToList();

        foreach (var id in ids)
        {
            await ImagesTestData.StoreUnboundAsync(factory, item.Id, id, uploadedAt: DateTimeOffset.UtcNow - Old);
        }

        await RunAsync(batchSize: 2);

        foreach (var id in ids)
        {
            Assert.Null(await ImagesTestData.FindAsync(factory, id));
        }
    }

    // US-5 · "the sweep writes no work item row": the item reads as it did, its change stamp unmoved.
    [Fact]
    public async Task RunOnce_TouchesNoWorkItem()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        await ImagesTestData.StoreUnboundAsync(factory, item.Id, Guid.CreateVersion7(), uploadedAt: DateTimeOffset.UtcNow - Old);
        var before = await WorkItemsTestData.ReadAsync(factory, world.Items, item.Identifier);

        await RunAsync();

        var after = await WorkItemsTestData.ReadAsync(factory, world.Items, item.Identifier);
        Assert.Equal(before.GetProperty("changedAt").GetDateTimeOffset(), after.GetProperty("changedAt").GetDateTimeOffset());
        Assert.Equal(before.GetProperty("description").GetString(), after.GetProperty("description").GetString());
    }

    // BR-IM-16 · the one Information line carries a count: no id, no user, no item.
    [Fact]
    public async Task RunOnce_LogsOneLine_WithTheCountOnly()
    {
        var (_, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var id = Guid.CreateVersion7();
        await ImagesTestData.StoreUnboundAsync(factory, item.Id, id, uploadedBy: "user-secret-subject", uploadedAt: DateTimeOffset.UtcNow - Old);
        var logger = new ListLogger();

        await RunAsync(logger: logger);

        var line = Assert.Single(logger.Lines, entry => entry.Level == LogLevel.Information);
        Assert.Matches("^Image sweep removed \\d+ unbound uploads$", line.Text);
        Assert.DoesNotContain(id.ToString(), line.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("user-secret-subject", line.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(item.Id.ToString(), line.Text, StringComparison.OrdinalIgnoreCase);
    }

    // US-5 · a second run finds nothing more.
    [Fact]
    public async Task RunOnce_Twice_SecondFindsNothingOfOurs()
    {
        var (_, item) = await ImagesTestData.WorldWithItemAsync(factory);
        await ImagesTestData.StoreUnboundAsync(factory, item.Id, Guid.CreateVersion7(), uploadedAt: DateTimeOffset.UtcNow - Old);
        await RunAsync();

        Assert.Equal(0, await RunAsync());
    }

    private sealed class ListLogger : ILogger<UnboundImageSweeper>
    {
        public List<(LogLevel Level, string Text)> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add((logLevel, formatter(state, exception)));
    }
}
