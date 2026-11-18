using System.Net;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Images;

/// <summary><c>GET /api/images/{imageId}</c> (feat-078 api-contract § 3, US-4): headers, 304, one 404 for every refusal.</summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetImageEndpointTests(MustaskApiFactory factory)
{
    [Fact]
    public async Task Get_ReadOnlyMember_200_BytesAndHeaders()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var id = await ImagesTestData.UploadBoundAsync(factory, world.Items, item.Identifier);
        var viewer = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);

        var answer = await ImagesTestData.GetAsync(factory, id, viewer.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("image/png", answer.ContentHeaders.ContentType?.MediaType);
        Assert.Equal($"\"{id}\"", answer.Headers.ETag?.Tag);
        Assert.True(answer.Headers.CacheControl is { NoCache: true, Private: true });
        Assert.Equal("nosniff", answer.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("default-src 'none'; sandbox", answer.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("same-origin", answer.Headers.GetValues("Cross-Origin-Resource-Policy").Single());
        Assert.NotEmpty(answer.Bytes);
        Assert.Equal(answer.Bytes.Length, answer.ContentHeaders.ContentLength);
    }

    [Fact]
    public async Task Get_IfNoneMatch_304_NoBody_ButOnlyAfterTheAccessCheck()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var id = await ImagesTestData.UploadBoundAsync(factory, world.Items, item.Identifier);
        var etag = $"\"{id}\"";

        var current = await ImagesTestData.GetAsync(factory, id, OrganizationsTokens.SignedIn(), etag);
        var stranger = await ImagesTestData.GetAsync(factory, id, OrganizationsTokens.Stranger(), etag);

        Assert.Equal(HttpStatusCode.NotModified, current.Status);
        Assert.Empty(current.Bytes);
        Assert.Equal(HttpStatusCode.NotFound, stranger.Status);
    }

    [Fact]
    public async Task Get_Unbound_UploaderOnly()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var uploaded = await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(30, 30));
        var id = uploaded.Body.GetProperty("id").GetGuid();
        var other = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);

        Assert.Equal(HttpStatusCode.OK, (await ImagesTestData.GetAsync(factory, id, OrganizationsTokens.SignedIn())).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await ImagesTestData.GetAsync(factory, id, other.Token)).Status);
    }

    [Fact]
    public async Task Get_EveryRefusal_SameStatusAndBody()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var bound = await ImagesTestData.UploadBoundAsync(factory, world.Items, item.Identifier);
        var token = OrganizationsTokens.Stranger();

        var foreign = await ImagesTestData.GetAsync(factory, bound, token);
        var madeUp = await ImagesTestData.GetAsync(factory, Guid.NewGuid(), token);
        var malformed = await ImagesTestData.SendAsync(factory, HttpMethod.Get, "/api/images/not-a-guid", null, token);

        Assert.Equal(HttpStatusCode.NotFound, foreign.Status);
        Assert.Equal("images.image.not_found", foreign.ErrorCode);
        Assert.Equal(foreign.Status, madeUp.Status);
        Assert.Equal(foreign.Status, malformed.Status);
        Assert.Equal(foreign.BodyWithoutTrace(), madeUp.BodyWithoutTrace());
        Assert.Equal(foreign.BodyWithoutTrace(), malformed.BodyWithoutTrace());
    }

    [Fact]
    public async Task Get_NoToken_401()
    {
        var answer = await ImagesTestData.GetAsync(factory, Guid.NewGuid(), null);

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
    }

    [Fact]
    public async Task Get_RightRevoked_404()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var id = await ImagesTestData.UploadBoundAsync(factory, world.Items, item.Identifier);
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        Assert.Equal(HttpStatusCode.OK, (await ImagesTestData.GetAsync(factory, id, member.Token)).Status);

        await OrganizationsTestData.SetOrganizationStatusAsync(factory, world.OrganizationId, OrganizationStatus.Deactivated);

        Assert.Equal(HttpStatusCode.NotFound, (await ImagesTestData.GetAsync(factory, id, member.Token)).Status);
    }
}
