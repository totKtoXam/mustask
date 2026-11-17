using static Mustask.Infrastructure.Tests.RichText.RichTextTestSupport;

namespace Mustask.Infrastructure.Tests.RichText;

/// <summary>The reducer's image element beyond the shared fixture file (feat-078 US-3, BR-IM-7, -9): the ids it reports and what counts.</summary>
public sealed class ReducerImageTests
{
    private static readonly Guid First = Guid.Parse("0b9f6a2e-5c3d-4a7e-8f1b-2d4c6e8a0b1c");
    private static readonly Guid Second = Guid.Parse("1c8e7b3f-6d4e-4b8f-9a2c-3e5d7f9b1c2d");

    private static string Img(Guid id, string alt = "") => $"<img data-image-id=\"{id}\" alt=\"{alt}\">";

    // US-3 · the save counts elements and binds the distinct ids: the reducer reports every kept image, in order, duplicates kept.
    [Fact]
    public void Reduce_Images_ReportsIdsInDocumentOrder_DuplicatesKept()
    {
        var result = Reduce($"<p>a</p>{Img(Second)}<p>b</p>{Img(First)}{Img(Second)}");

        Assert.Equal([Second, First, Second], result.ImageIds);
    }

    // US-3 · "An image alone is a description": content, and no visible character.
    [Fact]
    public void Reduce_ImageAlone_HasContent_ZeroVisible()
    {
        var result = Reduce(Img(First, "x"));

        Assert.True(result.HasContent);
        Assert.Equal(0, result.VisibleLength);
        Assert.Equal(Img(First, "x"), result.Html);
    }

    // US-3 · empty paragraphs and no image is still "" (BR-RT-6).
    [Fact]
    public void Reduce_EmptyParagraphs_NoImage_HasNoContent()
    {
        var result = Reduce("<p></p><p></p>");

        Assert.False(result.HasContent);
        Assert.Empty(result.ImageIds);
    }

    // US-3 · dropped images are not reported.
    [Fact]
    public void Reduce_DroppedImages_NotReported()
    {
        var result = Reduce($"<p>a<img src=\"https://e.org/x.png\"></p><table><tbody><tr><td>{Img(First)}</td></tr></tbody></table><pre><code>{Img(Second)}</code></pre>");

        Assert.Empty(result.ImageIds);
    }

    // US-3 · "Images add nothing to the visible count": 50 images with 255-code-point alternatives.
    [Fact]
    public void Reduce_FiftyImagesWithLongAlt_AddNoVisibleCharacter()
    {
        var alt = new string('я', 255);
        var html = "<p>a</p>" + string.Concat(Enumerable.Range(0, 50).Select(_ => Img(First, alt))) + "<p>b</p>";

        var result = Reduce(html);

        Assert.Equal(3, result.VisibleLength);
        Assert.Equal(50, result.ImageIds.Count);
    }

    // BR-IM-7 · an alternative is text: its angle brackets are not kept, so it never holds markup.
    [Fact]
    public void Reduce_AltWithMarkup_IsEscapedInTheAttribute()
    {
        var result = Reduce(Img(First, "x&lt;b&gt; &amp; y"));

        Assert.Equal(Img(First, "xb &amp; y"), result.Html);
        Assert.DoesNotContain("<b>", result.Html);
    }

    // BR-IM-7 · the reference is lower case, in the D form, whatever the writer's case.
    [Fact]
    public void Reduce_UpperCaseId_WrittenLowerCase()
    {
        var result = Reduce($"<img data-image-id=\"{First.ToString().ToUpperInvariant()}\">");

        Assert.Equal(Img(First), result.Html);
        Assert.Equal([First], result.ImageIds);
    }
}
