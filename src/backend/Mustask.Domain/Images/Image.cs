namespace Mustask.Domain.Images;

/// <summary>The measured facts of a stored picture (the stored form, orientation applied).</summary>
public sealed record StoredImageFacts(ImageFormat Format, int Width, int Height, int FrameCount, int ByteLength);

/// <summary>
/// An image of the store (ADR-0045): metadata only, the bytes are <see cref="ImageContent"/>. It is made unbound by its
/// uploader and bound once by a write that references it; nothing else changes it (spec BR-IM-5, -8, -16).
/// </summary>
public sealed class Image
{
    private Image()
    {
        // EF materialization.
    }

    public Guid Id { get; private set; }

    public ImagePurpose Purpose { get; private set; }

    public ImageFormat Format { get; private set; }

    public int ByteLength { get; private set; }

    public int? Width { get; private set; }

    public int? Height { get; private set; }

    public int FrameCount { get; private set; }

    /// <summary>The uploader's subject (as <c>created_by</c> elsewhere).</summary>
    public string UploadedBy { get; private set; } = null!;

    public DateTimeOffset UploadedAt { get; private set; }

    /// <summary>When a write bound it; <c>null</c> while it is an unbound upload.</summary>
    public DateTimeOffset? BoundAt { get; private set; }

    public Guid? WorkItemId { get; private set; }

    /// <summary>A new unbound upload of the caller for a work item's Описание.</summary>
    public static Image ForWorkItemDescription(
        Guid id, StoredImageFacts facts, string uploadedBy, Guid workItemId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadedBy);

        if (facts.ByteLength is < 1 or > ImageLimits.MaxStoredBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(facts), "A stored image is 1..5 242 880 bytes.");
        }

        return new Image
        {
            Id = id,
            Purpose = ImagePurpose.WorkItemDescription,
            Format = facts.Format,
            ByteLength = facts.ByteLength,
            Width = facts.Width,
            Height = facts.Height,
            FrameCount = facts.FrameCount,
            UploadedBy = uploadedBy,
            UploadedAt = now,
            WorkItemId = workItemId,
        };
    }

    /// <summary>Binds the image once; on a bound image it does nothing.</summary>
    public void Bind(DateTimeOffset now) => BoundAt ??= now;

    /// <summary>May a write under <paramref name="rule"/> reference this image? One verdict for every refusal.</summary>
    public ImageBindingVerdict CheckBinding(ImageBindingRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (Purpose != rule.Purpose || WorkItemId != rule.WorkItemId)
        {
            return ImageBindingVerdict.Unavailable;
        }

        if (BoundAt is not null)
        {
            return ImageBindingVerdict.Bound;
        }

        return string.Equals(UploadedBy, rule.WriterSubject, StringComparison.Ordinal)
            ? ImageBindingVerdict.Bindable
            : ImageBindingVerdict.Unavailable;
    }
}
