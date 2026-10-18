using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Application.Abstractions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// mod-005 BR-DR-5 (US-1 "Retention has no clock", US-4): nothing removes or restores an item by itself. No job, no time-to-live, no
/// reminder: the host runs exactly the background services listed here, none of which touches a work item, so a new one forces a
/// decision; and an item deleted a year ago is as it was — still deleted, with its record, restorable with nothing lost.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class RetentionTests(MustaskApiFactory factory)
{
    /// <summary>
    /// The background services of the host and why none of them reads or writes a work item. A line that adds one adds it here with
    /// its reason, or deletes this test's assumption on purpose (BR-DR-5).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> HostedServices = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DictionarySeedHostedService"] = "seeds the languages dictionary at start; no work item",
        ["RealmReadinessMonitor"] = "probes the identity realm's metadata; no work item",
        ["PushChannelStartupNotice"] = "writes one start-up notice about the user push channel; no work item",
        ["TelemetryHostedService"] = "OpenTelemetry's own start-up of the telemetry pipeline (Aspire service defaults); no work item",
    };

    [Fact]
    public void NoHostedService_WritesWorkItems_TheRegisteredOnesAreTheListedSet()
    {
        var registered = factory.Services.GetServices<IHostedService>()
            .Select(service => service.GetType())
            .Where(type => type.Namespace?.StartsWith("Microsoft.", StringComparison.Ordinal) != true)
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(HostedServices.Keys.Order(StringComparer.Ordinal), registered);
        Assert.All(HostedServices.Values, reason => Assert.False(string.IsNullOrWhiteSpace(reason)));
    }

    /// <summary>A clock a test moves; the host reads it for every deletion and restore.</summary>
    private sealed class MovableClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> HostWith(MovableClock clock) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IClock>(clock)));

    // US-1 · "Retention has no clock": a year later the item is still deleted, with the record it had, unchanged.
    [Fact]
    public async Task Delete_ThenALongTime_ItemIsStillDeletedUnchanged()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Давно удалённая", world.PlatformAreaId);
        var clock = new MovableClock(DateTimeOffset.UtcNow);
        await using var host = HostWith(clock);

        var deleted = await OrganizationsApi.SendAsync(
            host, HttpMethod.Post, $"{WorkItemsTestData.Item(world.Items, story.Identifier)}/delete", token: OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        clock.UtcNow = clock.UtcNow.AddYears(1);

        var later = await OrganizationsApi.SendAsync(
            host, HttpMethod.Get, WorkItemsTestData.Item(world.Items, story.Identifier), token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, later.Status);
        Assert.Equal(deleted.Body.GetRawText(), later.Body.GetRawText());
        Assert.Equal("Removed", later.Body.GetProperty("state").GetProperty("category").GetProperty("code").GetString());
    }

    // US-4 · "Retention has no clock": a restore a year later loses nothing — the held state returns, the marks clear, «Изменён» is the
    // restore's own time.
    [Fact]
    public async Task Restore_AfterAYear_NothingLost()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Год в корзине", world.PlatformAreaId);
        Assert.Equal(HttpStatusCode.OK, (await BoardsTestData.SetStateAsync(factory, world, story, "US", "Разрешена")).Status);
        var before = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);
        var clock = new MovableClock(DateTimeOffset.UtcNow);
        await using var host = HostWith(clock);
        Assert.Equal(
            HttpStatusCode.OK,
            (await OrganizationsApi.SendAsync(host, HttpMethod.Post, $"{WorkItemsTestData.Item(world.Items, story.Identifier)}/delete", token: OrganizationsTokens.SignedIn())).Status);
        clock.UtcNow = clock.UtcNow.AddYears(1);

        var restored = await OrganizationsApi.SendAsync(
            host, HttpMethod.Post, $"{WorkItemsTestData.Item(world.Items, story.Identifier)}/restore", token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, restored.Status);
        foreach (var property in before.EnumerateObject().Where(property => property.Name is not ("changedAt" or "deletion")))
        {
            Assert.True(
                property.Value.GetRawText() == restored.Body.GetProperty(property.Name).GetRawText(),
                $"{property.Name} lost in a year: {property.Value.GetRawText()} -> {restored.Body.GetProperty(property.Name).GetRawText()}.");
        }

        Assert.Equal(JsonValueKind.Null, restored.Body.GetProperty("deletion").ValueKind);
        Assert.Equal(clock.UtcNow.UtcDateTime, restored.Body.GetProperty("changedAt").GetDateTimeOffset().UtcDateTime, TimeSpan.FromMilliseconds(5));
    }
}
