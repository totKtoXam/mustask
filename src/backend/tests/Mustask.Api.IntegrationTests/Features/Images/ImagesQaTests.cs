using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Domain.Images;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure;
using Mustask.Infrastructure.Images;

namespace Mustask.Api.IntegrationTests.Features.Images;

/// <summary>
/// feat-078 backend QA (qa.md, Backend): the boundaries, orders of answers, non-disclosure cases and the forced bind/sweep race
/// the developer's suite left open.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ImagesQaTests(MustaskApiFactory factory)
{
    private const string Unavailable = "workitems.workitem.description_image_unavailable";

    private static string Img(Guid id) => $"<img data-image-id=\"{id}\" alt=\"\">";

    private static Guid IdOf(ImageAnswer upload)
    {
        Assert.Equal(HttpStatusCode.Created, upload.Status);

        return upload.Body.GetProperty("id").GetGuid();
    }

    private Task<Answer> PatchAsync(BoardsWorld world, string identifier, object body, string? token = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Patch, WorkItemsTestData.Item(world.Items, identifier), body, token ?? OrganizationsTokens.SignedIn());

    private static string? CodeOf(Answer answer) =>
        answer.Body.ValueKind == System.Text.Json.JsonValueKind.Object && answer.Body.TryGetProperty("errorCode", out var code) ? code.GetString() : null;

    /// <summary>A body with no declared length: the server has to count what it reads (HttpUploadBody's loop, not the header).</summary>
    private sealed class UnsizedContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;

            return false;
        }
    }

    private async Task<ImageAnswer> SendUnsizedAsync(string path, byte[] body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new UnsizedContent(body) };
        request.WithBearer(OrganizationsTokens.SignedIn());

        using var response = await factory.CreateClient().SendAsync(request);

        return new ImageAnswer(response.StatusCode, response.Headers, response.Content.Headers, await response.Content.ReadAsByteArrayAsync());
    }

    // qa.md A-2 · US-2 "Caps": without a Content-Length the cap is still 5 242 880 bytes on the upload and 20 971 520 on the reduction.
    [Fact]
    public async Task UnsizedBody_OverEachCap_IsRefusedForSize_NothingStored()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var png = ImagesTestData.Png(40, 30);

        var upload = await SendUnsizedAsync(ImagesTestData.UploadPath(world.Items, item.Identifier), ImagesTestData.PaddedTo(png, ImageLimits.MaxStoredBytes + 1));
        var reduction = await SendUnsizedAsync(ImagesTestData.ReductionPath(world.Items, item.Identifier), ImagesTestData.PaddedTo(png, ImageLimits.MaxReductionInputBytes + 1));

        Assert.Equal(HttpStatusCode.BadRequest, upload.Status);
        Assert.Equal("images.upload.too_large", upload.ErrorCode);
        Assert.Equal(HttpStatusCode.BadRequest, reduction.Status);
        Assert.Equal("images.reduction.too_large", reduction.ErrorCode);
        Assert.Equal(0, await ImagesTestData.CountForAsync(factory, item.Id));
    }

    // qa.md US-6 "The server holds the line" · the reduction intake's upper boundary, 20 971 520 bytes, is taken in; the answer's
    // original is the body as sent, the stored image is within 5 MB.
    [Fact]
    public async Task Reduce_Exactly20971520Bytes_IsTakenIn()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);

        var answer = await ImagesTestData.ReduceAsync(
            factory, world.Items, item.Identifier, ImagesTestData.PaddedTo(ImagesTestData.Png(40, 30), ImageLimits.MaxReductionInputBytes));

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(ImageLimits.MaxReductionInputBytes, answer.Body.GetProperty("original").GetProperty("byteLength").GetInt32());
        Assert.InRange(answer.Body.GetProperty("image").GetProperty("byteLength").GetInt32(), 1, ImageLimits.MaxStoredBytes);
    }

    // qa.md A-1 · an empty body is a format refusal on both routes, never a 500.
    [Fact]
    public async Task EmptyBody_IsAFormatRefusal_OnBothRoutes()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);

        var upload = await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, []);
        var reduction = await ImagesTestData.ReduceAsync(factory, world.Items, item.Identifier, []);

        Assert.Equal(HttpStatusCode.BadRequest, upload.Status);
        Assert.Equal("images.upload.format_unsupported", upload.ErrorCode);
        Assert.Equal(HttpStatusCode.BadRequest, reduction.Status);
        Assert.Equal("images.upload.format_unsupported", reduction.ErrorCode);
        Assert.Equal(0, await ImagesTestData.CountForAsync(factory, item.Id));
    }

    // qa.md A-3, A-4 · api-contract § 1 order of answers: the right and the item's state come before the body.
    [Fact]
    public async Task OrderOfAnswers_RightAndDeletedState_BeforeTheBody()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var viewer = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        var oversized = ImagesTestData.PaddedTo(ImagesTestData.Png(40, 30), ImageLimits.MaxStoredBytes + 1);

        var forbidden = await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, oversized, viewer.Token);
        await LinksTestData.DeleteAsync(factory, world.Items, item);
        var deleted = await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, "not an image"u8.ToArray());

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.Status);
        Assert.Equal(HttpStatusCode.Conflict, deleted.Status);
        Assert.Equal("workitems.workitem.deleted", deleted.ErrorCode);
    }

    // US-2 "Who may upload" · a deactivated project and a deactivated organization: the upload answers what an edit of the item's
    // Описание answers (BR-IM-5), and stores nothing.
    [Theory]
    [InlineData("project")]
    [InlineData("organization")]
    public async Task Upload_Deactivated_AnswersAsTheDescriptionEdit(string what)
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);

        if (what == "project")
        {
            await OrganizationsTestData.SetProjectStatusAsync(factory, world.OrganizationId, world.ProjectId, ProjectStatus.Deactivated);
        }
        else
        {
            await OrganizationsTestData.SetOrganizationStatusAsync(factory, world.OrganizationId, OrganizationStatus.Deactivated);
        }

        var edit = await PatchAsync(world, item.Identifier, new { description = "<p>x</p>", descriptionFormat = "html" });
        var upload = await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(40, 30));

        Assert.NotEqual(HttpStatusCode.OK, edit.Status);
        Assert.Equal(edit.Status, upload.Status);
        Assert.Equal(CodeOf(edit), upload.ErrorCode);
        Assert.Equal(0, await ImagesTestData.CountForAsync(factory, item.Id));
    }

    // NFR "Non-disclosure", US-4 "The answers tell nothing": a foreign item's image, a swept upload, another user's unbound upload,
    // a purged item's image, a made-up id and a braced id answer the one 404 with the one body.
    [Fact]
    public async Task EveryRefusal_IncludingSweptPurgedAndForeignUnbound_IsTheSame404()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var doomed = await BoardsTestData.CreateAsync(factory, world, "US", "Удаляемая", world.PlatformAreaId);
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);
        var bound = await ImagesTestData.UploadBoundAsync(factory, world.Items, item.Identifier);
        var swept = IdOf(await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(20, 20)));
        await ImagesTestData.DeleteAsync(factory, swept);
        var creatorsUnbound = IdOf(await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(21, 21)));
        var purged = await ImagesTestData.UploadBoundAsync(factory, world.Items, doomed.Identifier);
        await LinksTestData.DeleteAsync(factory, world.Items, doomed);
        var purge = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{WorkItemsTestData.Item(world.Items, doomed.Identifier)}/purge", token: OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.NoContent, purge.Status);

        var answers = new[]
        {
            await ImagesTestData.GetAsync(factory, bound, OrganizationsTokens.Stranger()),
            await ImagesTestData.GetAsync(factory, swept, OrganizationsTokens.SignedIn()),
            await ImagesTestData.GetAsync(factory, creatorsUnbound, member.Token),
            await ImagesTestData.GetAsync(factory, purged, OrganizationsTokens.SignedIn()),
            await ImagesTestData.GetAsync(factory, Guid.NewGuid(), OrganizationsTokens.SignedIn()),
            await ImagesTestData.SendAsync(factory, HttpMethod.Get, $"/api/images/{{{bound}}}", null, OrganizationsTokens.SignedIn()),
        };

        Assert.All(answers, answer => Assert.Equal(HttpStatusCode.NotFound, answer.Status));
        Assert.All(answers, answer => Assert.Equal("images.image.not_found", answer.ErrorCode));
        Assert.Single(answers.Select(answer => answer.BodyWithoutTrace()).Distinct(StringComparer.Ordinal));
    }

    // US-4 "a user whose right to read X was revoked a moment ago" · the membership itself removed: the next read is 404, and a
    // revalidation with the entity tag the reader kept is 404 too, never 304.
    [Fact]
    public async Task MembershipRemoved_NextRead404_AndNo304()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var id = await ImagesTestData.UploadBoundAsync(factory, world.Items, item.Identifier);
        var reader = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        var first = await ImagesTestData.GetAsync(factory, id, reader.Token);
        Assert.Equal(HttpStatusCode.OK, first.Status);
        var etag = first.Headers.ETag!.Tag;

        Assert.True(await MembershipTestData.RemoveMemberAsync(factory, world.OrganizationId, reader.Id));

        Assert.Equal(HttpStatusCode.NotFound, (await ImagesTestData.GetAsync(factory, id, reader.Token)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await ImagesTestData.GetAsync(factory, id, reader.Token, etag)).Status);
    }

    // US-5 "A save and the sweep at the same moment", forced end to end: the sweep's delete holds the row, the PATCH that references
    // the upload waits on it; when the delete commits the save is refused and the earlier description stays — never a stored
    // reference to a removed image.
    [Fact]
    public async Task SaveWaitingOnTheSweepsDelete_IsRefused_NothingDangling()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        Assert.Equal(HttpStatusCode.OK, (await PatchAsync(world, item.Identifier, new { description = "<p>before</p>", descriptionFormat = "html" })).Status);
        var id = IdOf(await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(30, 30)));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        await using var sweep = await context.Database.BeginTransactionAsync();
        Assert.Equal(1, await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM images.images WHERE id = {id} AND bound_at IS NULL"));

        var save = PatchAsync(world, item.Identifier, new { description = $"<p>after</p>{Img(id)}", descriptionFormat = "html" });
        await Task.Delay(800);
        Assert.False(save.IsCompleted, "the save waits for the row the sweep holds");

        await sweep.CommitAsync();
        var answer = await save.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal(Unavailable, answer.Body.GetProperty("errors").GetProperty("description").EnumerateArray().Single().GetProperty("code").GetString());
        var stored = (await WorkItemsTestData.ReadAsync(factory, world.Items, item.Identifier)).GetProperty("description").GetString();
        Assert.Equal("<p>before</p>", stored);
        Assert.Null(await ImagesTestData.FindAsync(factory, id));
    }

    // US-5 "hourly" · BR-IM-16: the hosted sweep runs once at start and then every hour, removing an upload 2 h after it was made;
    // with a run at start and one per hour, a due upload goes at most 3 h after the upload.
    [Fact]
    public async Task HostedSweep_RunsAtStart_HourlyWithATwoHourLifetime()
    {
        Assert.Equal(TimeSpan.FromHours(2), ImageLimits.UnboundLifetime);
        Assert.Equal(TimeSpan.FromHours(1), ImageLimits.SweepInterval);

        var (_, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var due = Guid.CreateVersion7();
        await ImagesTestData.StoreUnboundAsync(factory, item.Id, due, uploadedAt: DateTimeOffset.UtcNow - TimeSpan.FromMinutes(121));

        using var sweeper = new UnboundImageSweeper(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), new ImagesOptions(), TimeProvider.System, NullLogger<UnboundImageSweeper>.Instance);
        await sweeper.StartAsync(CancellationToken.None);

        try
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);

            while (await ImagesTestData.FindAsync(factory, due) is not null && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
            }

            Assert.Null(await ImagesTestData.FindAsync(factory, due));
        }
        finally
        {
            await sweeper.StopAsync(CancellationToken.None);
        }
    }
}
