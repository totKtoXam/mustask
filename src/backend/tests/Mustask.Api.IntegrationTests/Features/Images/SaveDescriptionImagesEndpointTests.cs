using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Images;

/// <summary>
/// <c>PATCH …/workitems/{identifier}</c> with images in the description (feat-078 US-3, BR-IM-6, -8, -10): the save binds the
/// writer's own uploads for the item, keeps the item's bound ones, refuses every other reference with one reason and counts elements.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SaveDescriptionImagesEndpointTests(MustaskApiFactory factory)
{
    private const string Unavailable = "workitems.workitem.description_image_unavailable";
    private const string TooMany = "workitems.workitem.description_too_many_images";

    private static string Img(Guid id, string alt = "") => $"<img data-image-id=\"{id}\" alt=\"{alt}\">";

    private static Guid IdOf(ImageAnswer upload)
    {
        Assert.Equal(HttpStatusCode.Created, upload.Status);

        return upload.Body.GetProperty("id").GetGuid();
    }

    private Task<Answer> PatchAsync(BoardsWorld world, string identifier, object body, string? token = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Patch, WorkItemsTestData.Item(world.Items, identifier), body, token ?? OrganizationsTokens.SignedIn());

    private static string? Reason(Answer answer) =>
        answer.Body.GetProperty("errors").GetProperty("description").EnumerateArray().Single().GetProperty("code").GetString();

    private async Task<string> StoredDescriptionAsync(BoardsWorld world, string identifier) =>
        (await WorkItemsTestData.ReadAsync(factory, world.Items, identifier)).GetProperty("description").GetString() ?? string.Empty;

    // US-3 · "The writer's own uploads are bound by the save".
    [Fact]
    public async Task Save_OwnUploads_StoredAndBoundAndReadable()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var first = IdOf(await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(40, 30)));
        var second = IdOf(await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(50, 30)));
        var description = $"<p>a</p>{Img(first, "один")}{Img(second, "два")}";

        var answer = await PatchAsync(world, item.Identifier, new { description, descriptionFormat = "html" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(description, answer.Body.GetProperty("description").GetString());
        Assert.All([first, second], id => Assert.NotNull(ImagesTestData.FindAsync(factory, id).GetAwaiter().GetResult()?.BoundAt));

        var viewer = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        Assert.Equal(HttpStatusCode.OK, (await ImagesTestData.GetAsync(factory, first, viewer.Token)).Status);
        Assert.Equal(HttpStatusCode.OK, (await ImagesTestData.GetAsync(factory, second, viewer.Token)).Status);
    }

    // US-3 · "Re-saving keeps images already bound": another member's image stays through a third member's edit.
    [Fact]
    public async Task Save_ImageBoundByAnotherMember_KeptByAThirdMembersEdit()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var author = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);
        var editor = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);
        var id = IdOf(await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(40, 30), author.Token));
        Assert.Equal(HttpStatusCode.OK, (await PatchAsync(world, item.Identifier, new { description = $"<p>a</p>{Img(id)}", descriptionFormat = "html" }, author.Token)).Status);

        var edit = await PatchAsync(world, item.Identifier, new { description = $"<p>b</p>{Img(id)}", descriptionFormat = "html" }, editor.Token);

        Assert.Equal(HttpStatusCode.OK, edit.Status);
        Assert.Equal($"<p>b</p>{Img(id)}", await StoredDescriptionAsync(world, item.Identifier));
    }

    // US-3 · "A reference that is not the item's own refuses the whole save": every kind gives the same reason, nothing is stored.
    [Theory]
    [InlineData("bound-to-another-item")]
    [InlineData("another-members-unbound")]
    [InlineData("own-upload-for-another-item")]
    [InlineData("own-upload-swept")]
    [InlineData("made-up")]
    public async Task Save_ForeignReference_RefusesTheWholeSave_NothingBound(string kind)
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var other = await BoardsTestData.CreateAsync(factory, world, "US", "Другая", world.PlatformAreaId);
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);
        var good = IdOf(await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(40, 30)));
        Assert.Equal(HttpStatusCode.OK, (await PatchAsync(world, item.Identifier, new { description = "<p>kept</p>", descriptionFormat = "html" })).Status);

        var bad = kind switch
        {
            "bound-to-another-item" => await BoundTo(world, other),
            "another-members-unbound" => IdOf(await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(30, 30), member.Token)),
            "own-upload-for-another-item" => IdOf(await ImagesTestData.UploadAsync(factory, world.Items, other.Identifier, ImagesTestData.Png(30, 30))),
            "own-upload-swept" => await SweptAsync(world, item),
            _ => Guid.NewGuid(),
        };

        var answer = await PatchAsync(world, item.Identifier, new { description = $"<p>new</p>{Img(good)}{Img(bad)}", descriptionFormat = "html" });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal(Unavailable, Reason(answer));
        Assert.Equal("<p>kept</p>", await StoredDescriptionAsync(world, item.Identifier));
        Assert.Null((await ImagesTestData.FindAsync(factory, good))!.BoundAt);
    }

    private async Task<Guid> BoundTo(BoardsWorld world, Item other)
    {
        var id = IdOf(await ImagesTestData.UploadAsync(factory, world.Items, other.Identifier, ImagesTestData.Png(30, 30)));
        Assert.Equal(HttpStatusCode.OK, (await PatchAsync(world, other.Identifier, new { description = Img(id), descriptionFormat = "html" })).Status);

        return id;
    }

    private async Task<Guid> SweptAsync(BoardsWorld world, Item item)
    {
        var id = IdOf(await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(30, 30)));
        await ImagesTestData.DeleteAsync(factory, id);

        return id;
    }

    // US-3 · "Over 50 images": 51 elements, duplicates counted.
    [Fact]
    public async Task Save_FiftyOneImageElements_IsTooManyImages_NothingStored()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var id = IdOf(await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(30, 30)));

        var answer = await PatchAsync(world, item.Identifier, new { description = string.Concat(Enumerable.Repeat(Img(id), 51)), descriptionFormat = "html" });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal(TooMany, Reason(answer));
        Assert.Null((await ImagesTestData.FindAsync(factory, id))!.BoundAt);
    }

    // US-3 · 50 elements are accepted, the same image repeated.
    [Fact]
    public async Task Save_FiftyElementsOfOneImage_Accepted()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var id = IdOf(await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(30, 30)));

        var answer = await PatchAsync(world, item.Identifier, new { description = string.Concat(Enumerable.Repeat(Img(id), 50)), descriptionFormat = "html" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.NotNull((await ImagesTestData.FindAsync(factory, id))!.BoundAt);
    }

    // US-3 · "An image alone is a description": stored as the image, not as "".
    [Fact]
    public async Task Save_ImageAlone_StoredAsHtml()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var id = IdOf(await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(30, 30)));

        var answer = await PatchAsync(world, item.Identifier, new { description = Img(id), descriptionFormat = "html" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(Img(id), answer.Body.GetProperty("description").GetString());
        Assert.Equal("html", answer.Body.GetProperty("descriptionFormat").GetString());
    }

    // US-3 · "The stored markup names nothing but the image": the element has the opaque reference and the alt, a v4 id.
    [Fact]
    public async Task Save_StoredMarkup_NamesNothingButTheImage()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var id = IdOf(await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(30, 30)));
        await PatchAsync(world, item.Identifier, new { description = $"<p>x</p>{Img(id, "вид")}", descriptionFormat = "html" });

        var stored = await StoredDescriptionAsync(world, item.Identifier);

        Assert.Matches("^<p>x</p><img data-image-id=\"[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\" alt=\"[^\"]*\">$", stored);
        Assert.DoesNotContain(world.OrganizationId.ToString(), stored, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(world.ProjectId.ToString(), stored, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(item.Id.ToString(), stored, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(item.Identifier, stored, StringComparison.Ordinal);
        Assert.DoesNotContain("src=", stored, StringComparison.Ordinal);
    }

    // US-3 · "A plain-format write cannot carry images": stored as characters, binds nothing.
    [Fact]
    public async Task Save_PlainFormat_StoredAsCharacters_BindsNothing()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var id = IdOf(await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(30, 30)));
        var text = Img(id);

        var answer = await PatchAsync(world, item.Identifier, new { description = text, descriptionFormat = "plain" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(text, answer.Body.GetProperty("description").GetString());
        Assert.Null((await ImagesTestData.FindAsync(factory, id))!.BoundAt);
    }

    // US-3 · "A save that does not touch Описание binds nothing": the upload stays for the sweep.
    [Fact]
    public async Task Save_WithoutDescription_BindsNothing()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var id = IdOf(await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(30, 30)));

        var answer = await PatchAsync(world, item.Identifier, new { title = "Новый заголовок" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Null((await ImagesTestData.FindAsync(factory, id))!.BoundAt);
    }

    // BR-IM-6 · a viewer cannot save, so cannot bind; a refused save binds nothing either (the transaction ends with it).
    [Fact]
    public async Task Save_RefusedForAnotherField_BindsNothing()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var id = IdOf(await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(30, 30)));

        var answer = await PatchAsync(world, item.Identifier, new { title = new string('я', 400), description = Img(id), descriptionFormat = "html" });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Null((await ImagesTestData.FindAsync(factory, id))!.BoundAt);
    }

    // BR-IM-6 · a deleted item takes no save, so binds nothing (the item's own 409 comes first).
    [Fact]
    public async Task Save_DeletedItem_409_BindsNothing()
    {
        var (world, item) = await ImagesTestData.WorldWithItemAsync(factory);
        var id = IdOf(await ImagesTestData.UploadAsync(factory, world.Items, item.Identifier, ImagesTestData.Png(30, 30)));
        await LinksTestDataDelete(world, item);

        var answer = await PatchAsync(world, item.Identifier, new { description = Img(id), descriptionFormat = "html" });

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Null((await ImagesTestData.FindAsync(factory, id))!.BoundAt);
    }

    private Task LinksTestDataDelete(BoardsWorld world, Item item) => LinksTestData.DeleteAsync(factory, world.Items, item);
}
