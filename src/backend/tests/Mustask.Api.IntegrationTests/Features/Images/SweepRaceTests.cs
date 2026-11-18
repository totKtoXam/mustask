using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Mustask.Application.Abstractions.Images;
using Mustask.Domain.Images;
using Mustask.Infrastructure;
using Mustask.Infrastructure.Images;

namespace Mustask.Api.IntegrationTests.Features.Images;

/// <summary>
/// The save that binds against the sweep that deletes (feat-078 ADR-0045 decision 3, plan-backend § 8.2): whoever locks the row first
/// decides, the other sees the result — a bound row is kept, a swept row makes the bind refuse — and neither waits on the other in a cycle.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SweepRaceTests(MustaskApiFactory factory)
{
    private const string Writer = "race-writer";

    private async Task<(Guid ItemId, Guid ImageId)> DueImageAsync()
    {
        var (_, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var id = Guid.CreateVersion7();
        await ImagesTestData.StoreUnboundAsync(factory, item.Id, id, Writer, DateTimeOffset.UtcNow - TimeSpan.FromHours(3));

        return (item.Id, id);
    }

    private UnboundImageSweeper Sweeper() => new(
        factory.Services.GetRequiredService<IServiceScopeFactory>(), new ImagesOptions(), TimeProvider.System, NullLogger<UnboundImageSweeper>.Instance);

    // ADR-0045 decision 3 · the save locked first: the sweep's batch skips the row, the single-row pass waits, and on the save's
    // commit finds it bound — kept.
    [Fact]
    public async Task SaveLockedFirst_SweepKeepsTheBoundRow()
    {
        var (itemId, imageId) = await DueImageAsync();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        await using var transaction = await context.Database.BeginTransactionAsync();
        Assert.True(await scope.ServiceProvider.GetRequiredService<IImageBinding>()
            .LockAndBindAsync([imageId], ImageBindingRule.ForWorkItemDescription(itemId, Writer), DateTimeOffset.UtcNow, CancellationToken.None));

        var sweep = Sweeper().RunOnceAsync(CancellationToken.None);
        await Task.Delay(700);
        Assert.False(sweep.IsCompleted, "the sweep waits for the row the save holds");

        await transaction.CommitAsync();
        await sweep;

        var row = await ImagesTestData.FindAsync(factory, imageId);
        Assert.NotNull(row);
        Assert.NotNull(row.BoundAt);
    }

    // ADR-0045 decision 3 · the sweep deleted first: the save finds the row missing and is refused.
    [Fact]
    public async Task SweepDeletedFirst_BindIsRefused()
    {
        var (itemId, imageId) = await DueImageAsync();
        await Sweeper().RunOnceAsync(CancellationToken.None);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var accepted = await scope.ServiceProvider.GetRequiredService<IImageBinding>()
            .LockAndBindAsync([imageId], ImageBindingRule.ForWorkItemDescription(itemId, Writer), DateTimeOffset.UtcNow, CancellationToken.None);
        await transaction.CommitAsync();

        Assert.False(accepted);
        Assert.Null(await ImagesTestData.FindAsync(factory, imageId));
    }

    // ADR-0045 decision 3 · both want the row while a third holds it: whichever gets it first, the outcome is one of the two
    // consistent ones — bound and kept, or swept and refused — never a bound row that is gone nor a refusal of a row that stays.
    [Fact]
    public async Task BothWaitForARow_TheOutcomeIsConsistent()
    {
        var (itemId, imageId) = await DueImageAsync();
        using var holder = factory.Services.CreateScope();
        var holderContext = holder.ServiceProvider.GetRequiredService<MustaskDbContext>();
        await using var held = await holderContext.Database.BeginTransactionAsync();
        await holderContext.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM images.images WHERE id = {imageId} FOR UPDATE");

        var sweep = Sweeper().RunOnceAsync(CancellationToken.None);
        var bind = Task.Run(async () =>
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
            await using var transaction = await context.Database.BeginTransactionAsync();
            var accepted = await scope.ServiceProvider.GetRequiredService<IImageBinding>()
                .LockAndBindAsync([imageId], ImageBindingRule.ForWorkItemDescription(itemId, Writer), DateTimeOffset.UtcNow, CancellationToken.None);
            await transaction.CommitAsync();

            return accepted;
        });
        await Task.Delay(500);
        await held.CommitAsync();

        var accepted = await bind.WaitAsync(TimeSpan.FromSeconds(30));
        await sweep.WaitAsync(TimeSpan.FromSeconds(30));

        var row = await ImagesTestData.FindAsync(factory, imageId);

        if (accepted)
        {
            Assert.NotNull(row);
            Assert.NotNull(row.BoundAt);
        }
        else
        {
            Assert.Null(row);
        }
    }
}
