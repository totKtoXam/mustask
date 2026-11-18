using Mustask.Domain.Images;
using Mustask.Domain.WorkItems;

namespace Mustask.Domain.Tests.Images;

public sealed class ImageTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid ItemX = Guid.NewGuid();
    private static readonly Guid ItemY = Guid.NewGuid();

    private static Image Unbound(string by = "alice", Guid? item = null) =>
        Image.ForWorkItemDescription(Guid.NewGuid(), new StoredImageFacts(ImageFormat.Png, 100, 50, 1, 1234), by, item ?? ItemX, Now);

    // US-3 · "The image is not the item's own": own unbound upload for this item is bindable.
    [Fact]
    public void CheckBinding_OwnUnboundForThisItem_IsBindable()
    {
        Assert.Equal(ImageBindingVerdict.Bindable, Unbound().CheckBinding(ImageBindingRule.ForWorkItemDescription(ItemX, "alice")));
    }

    // US-3 · "Not the item's own": another writer's unbound upload.
    [Fact]
    public void CheckBinding_AnotherWritersUnbound_IsUnavailable()
    {
        Assert.Equal(ImageBindingVerdict.Unavailable, Unbound("alice").CheckBinding(ImageBindingRule.ForWorkItemDescription(ItemX, "bob")));
    }

    // US-3 · "Not the item's own": own upload for another item.
    [Fact]
    public void CheckBinding_OwnUploadForAnotherItem_IsUnavailable()
    {
        Assert.Equal(ImageBindingVerdict.Unavailable, Unbound(item: ItemY).CheckBinding(ImageBindingRule.ForWorkItemDescription(ItemX, "alice")));
    }

    // US-3 · "Re-saving keeps bound images": bound to this item, any writer.
    [Fact]
    public void CheckBinding_BoundToThisItem_IsBoundForAnyWriter()
    {
        var image = Unbound("alice");
        image.Bind(Now);

        Assert.Equal(ImageBindingVerdict.Bound, image.CheckBinding(ImageBindingRule.ForWorkItemDescription(ItemX, "carol")));
    }

    // US-3 · "Not the item's own": bound to another item.
    [Fact]
    public void CheckBinding_BoundToAnotherItem_IsUnavailable()
    {
        var image = Unbound(item: ItemY);
        image.Bind(Now);

        Assert.Equal(ImageBindingVerdict.Unavailable, image.CheckBinding(ImageBindingRule.ForWorkItemDescription(ItemX, "alice")));
    }

    [Fact]
    public void Bind_IsIdempotent_TheFirstMomentStays()
    {
        var image = Unbound();

        image.Bind(Now);
        image.Bind(Now.AddHours(1));

        Assert.Equal(Now, image.BoundAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5_242_881)]
    public void ForWorkItemDescription_BytesOutsideTheCap_Throws(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Image.ForWorkItemDescription(
            Guid.NewGuid(), new StoredImageFacts(ImageFormat.Png, 1, 1, 1, length), "alice", ItemX, Now));
    }

    // US-3 · "Over 50": the limit is on the count of elements, duplicates counted.
    [Theory]
    [InlineData(0, null)]
    [InlineData(50, null)]
    [InlineData(51, "workitems.workitem.description_too_many_images")]
    public void ValidateImages_CountAgainstFifty(int count, string? expected)
    {
        Assert.Equal(expected, WorkItemText.ValidateImages(count));
    }

    [Theory]
    [InlineData(ImageFormat.Png, "png", "image/png")]
    [InlineData(ImageFormat.Jpeg, "jpeg", "image/jpeg")]
    [InlineData(ImageFormat.Gif, "gif", "image/gif")]
    [InlineData(ImageFormat.Webp, "webp", "image/webp")]
    public void Formats_HaveTheirWireNameAndMediaType(ImageFormat format, string name, string media)
    {
        Assert.Equal(name, ImageFormats.WireName(format));
        Assert.Equal(media, ImageFormats.MediaType(format));
    }
}
