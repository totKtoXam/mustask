using Mustask.Domain.Images;

namespace Mustask.Application.Features.Images;

/// <summary>A stored picture as the upload and the reduction answer it (api-contract § 1).</summary>
public sealed record ImageUploadView(Guid Id, string Format, int ByteLength, int Width, int Height);

/// <summary>The file as sent, for the reduction's «Было» (api-contract § 2): no id, it is not stored.</summary>
public sealed record OriginalImageView(string Format, int ByteLength, int Width, int Height)
{
    public static OriginalImageView Of(StoredImageFacts facts) =>
        new(ImageFormats.WireName(facts.Format), facts.ByteLength, facts.Width, facts.Height);
}

/// <summary>What the flow stored: the new image and the file as sent.</summary>
public sealed record StoredDescriptionImage(ImageUploadView Image, OriginalImageView Original);
