using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Infrastructure.Images.Intake;

namespace Mustask.Api.IntegrationTests.Features.Images;

/// <summary>
/// ADR-0045 decision 5 · US-6 "Gate": with one permit and no queue, a second heavy request is answered 503 <c>images.busy</c> with
/// <c>Retry-After</c> while the permit is held. A serial class: it builds its own host with the gate's settings; the test takes the
/// permit itself, so no wall-clock race decides the outcome.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class IntakeGateTests(MustaskApiFactory factory)
{
    [Fact]
    public async Task Reduce_WhilePermitsAndQueueAreExhausted_503Busy_RetryAfter()
    {
        using var narrow = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Images:Intake:MaxConcurrentDecodes", "1");
            builder.UseSetting("Images:Intake:QueueLength", "0");
            builder.UseSetting("Images:Intake:MaxWaitSeconds", "1");
        });
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var heavy = ImagesTestData.Jpeg(4500, 3375, quality: 100, sigma: 60);
        var token = OrganizationsTokens.SignedIn();

        async Task<ImageAnswer> SendAsync() => await ImagesSend(narrow, ImagesTestData.ReductionPath(world.Items, item.Identifier), heavy, token);

        // The permit is held by the test, not by a slow request: nothing races the clock, and the busy answer is the only way out.
        var gate = narrow.Services.GetRequiredService<DecodeGate>();
        var held = await gate.EnterAsync(CancellationToken.None);
        Assert.NotNull(held);

        ImageAnswer second;
        try
        {
            second = await SendAsync();
        }
        finally
        {
            held.Dispose();
        }

        Assert.Equal(HttpStatusCode.ServiceUnavailable, second.Status);
        Assert.Equal("images.busy", second.ErrorCode);
        Assert.Equal("5", Assert.Single(second.Headers.GetValues("Retry-After")));

        // The permit is back: the same heavy upload now runs.
        Assert.Equal(HttpStatusCode.Created, (await SendAsync()).Status);
    }

    private static async Task<ImageAnswer> ImagesSend(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host, string path, byte[] body, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new ByteArrayContent(body) };
        request.WithBearer(token);

        using var response = await host.CreateClient().SendAsync(request);

        return new ImageAnswer(response.StatusCode, response.Headers, response.Content.Headers, await response.Content.ReadAsByteArrayAsync());
    }
}
