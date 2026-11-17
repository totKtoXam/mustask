using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Application.Abstractions.Images;
using Mustask.Domain.Images;
using Mustask.Infrastructure;

namespace Mustask.Api.IntegrationTests.Features.Images;

/// <summary>
/// <see cref="IImageBinding"/> against the real database (feat-078 plan-backend § 7): the rows are locked in id order whatever the
/// order of the request, a held lock is waited for, and a refusal updates nothing.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ImageBindingTests(MustaskApiFactory factory)
{
    private const string Writer = "writer-1";

    private async Task<(Guid ItemId, Guid First, Guid Second)> TwoUnboundAsync()
    {
        var (_, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        await ImagesTestData.StoreUnboundAsync(factory, item.Id, first, Writer);
        await ImagesTestData.StoreUnboundAsync(factory, item.Id, second, Writer);

        return (item.Id, first, second);
    }

    private async Task<bool> BindAsync(Guid itemId, Guid[] ids, string writer = Writer, TimeSpan? holdFor = null)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var result = await scope.ServiceProvider.GetRequiredService<IImageBinding>()
            .LockAndBindAsync(ids, ImageBindingRule.ForWorkItemDescription(itemId, writer), DateTimeOffset.UtcNow, CancellationToken.None);

        if (holdFor is { } hold)
        {
            await Task.Delay(hold);
        }

        await transaction.CommitAsync();

        return result;
    }

    // Plan § 7.3 · "images (id order)": two saves that name the same two images in opposite orders do not deadlock.
    [Fact]
    public async Task LockAndBind_OppositeOrders_NeverDeadlock()
    {
        var (itemId, first, second) = await TwoUnboundAsync();

        var results = await Task.WhenAll(
            BindAsync(itemId, [first, second], holdFor: TimeSpan.FromMilliseconds(300)),
            BindAsync(itemId, [second, first], holdFor: TimeSpan.FromMilliseconds(300)));

        Assert.All(results, Assert.True);
        Assert.NotNull((await ImagesTestData.FindAsync(factory, first))!.BoundAt);
        Assert.NotNull((await ImagesTestData.FindAsync(factory, second))!.BoundAt);
    }

    // Plan § 7 · a row another transaction holds is waited for, not skipped: the bind sees the committed state.
    [Fact]
    public async Task LockAndBind_RowHeldByAnotherTransaction_WaitsForIt()
    {
        var (itemId, first, _) = await TwoUnboundAsync();
        using var holder = factory.Services.CreateScope();
        var holderContext = holder.ServiceProvider.GetRequiredService<MustaskDbContext>();
        await using var transaction = await holderContext.Database.BeginTransactionAsync();
        await holderContext.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM images.images WHERE id = {first} FOR UPDATE");

        var waiting = BindAsync(itemId, [first]);
        await Task.Delay(700);
        Assert.False(waiting.IsCompleted);

        await transaction.CommitAsync();

        Assert.True(await waiting);
        Assert.NotNull((await ImagesTestData.FindAsync(factory, first))!.BoundAt);
    }

    // BR-IM-6 · one image that is not the writer's own refuses the whole set and updates nothing.
    [Fact]
    public async Task LockAndBind_OneUnavailable_UpdatesNothing()
    {
        var (itemId, first, second) = await TwoUnboundAsync();
        await ImagesTestData.StoreUnboundAsync(factory, itemId, second, "someone-else");

        var accepted = await BindAsync(itemId, [first, second]);

        Assert.False(accepted);
        Assert.Null((await ImagesTestData.FindAsync(factory, first))!.BoundAt);
        Assert.Null((await ImagesTestData.FindAsync(factory, second))!.BoundAt);
    }

    // BR-IM-6 · an id with no row refuses the same way.
    [Fact]
    public async Task LockAndBind_MissingId_Refused()
    {
        var (itemId, first, _) = await TwoUnboundAsync();

        Assert.False(await BindAsync(itemId, [first, Guid.NewGuid()]));
        Assert.Null((await ImagesTestData.FindAsync(factory, first))!.BoundAt);
    }

    // US-3 · "Re-saving keeps images already bound": bound to the item already is accepted, and the stamp does not move.
    [Fact]
    public async Task LockAndBind_AlreadyBound_AcceptedAndStampKept()
    {
        var (itemId, first, _) = await TwoUnboundAsync();
        Assert.True(await BindAsync(itemId, [first]));
        var stamp = (await ImagesTestData.FindAsync(factory, first))!.BoundAt;

        Assert.True(await BindAsync(itemId, [first], writer: "another-writer"));

        Assert.Equal(stamp, (await ImagesTestData.FindAsync(factory, first))!.BoundAt);
    }
}
