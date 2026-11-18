namespace Mustask.Domain.Images;

/// <summary>The bytes of an <see cref="Image"/>, a separate row so a tracked image never loads them (ADR-0045 decision 1).</summary>
public sealed class ImageContent
{
    private ImageContent()
    {
        // EF materialization.
    }

    public ImageContent(Guid imageId, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        ImageId = imageId;
        Content = content;
    }

    public Guid ImageId { get; private set; }

    public byte[] Content { get; private set; } = null!;
}
