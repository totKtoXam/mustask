namespace Mustask.Domain.Images;

/// <summary>The four stored formats (BR-IM-1). The value is read from the content, never from a name or a declared type. Stored as text.</summary>
public enum ImageFormat
{
    Png,
    Jpeg,
    Gif,
    Webp,
}

/// <summary>The wire names and media types of <see cref="ImageFormat"/>.</summary>
public static class ImageFormats
{
    /// <summary>The lower-case name in the upload answer (<c>png</c>, <c>jpeg</c>, <c>gif</c>, <c>webp</c>).</summary>
    public static string WireName(ImageFormat format) => format switch
    {
        ImageFormat.Png => "png",
        ImageFormat.Jpeg => "jpeg",
        ImageFormat.Gif => "gif",
        ImageFormat.Webp => "webp",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>The media type of the read (BR-IM-11).</summary>
    public static string MediaType(ImageFormat format) => "image/" + WireName(format);
}
