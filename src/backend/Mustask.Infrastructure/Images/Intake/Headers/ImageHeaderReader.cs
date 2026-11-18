namespace Mustask.Infrastructure.Images.Intake.Headers;

/// <summary>
/// Reads what a file says about itself without decoding it (ADR-0045 decision 2 step 1; plan-backend § 4.1). The format is
/// recognised by its magic bytes only; the declared <c>Content-Type</c> and any file name are never consulted.
/// </summary>
internal static class ImageHeaderReader
{
    /// <summary>Reads the header, or says why the file is refused. Over-cap sizes are refused here, before any decode.</summary>
    public static bool TryRead(ReadOnlySpan<byte> data, out ImageHeader header, out ImageRefusal refusal)
    {
        if (data.Length >= 8 && data[..8].SequenceEqual(PngHeaderReader.Signature))
        {
            return PngHeaderReader.TryRead(data, out header, out refusal);
        }

        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return JpegHeaderReader.TryRead(data, out header, out refusal);
        }

        if (GifHeaderReader.IsGif(data))
        {
            return GifHeaderReader.TryRead(data, out header, out refusal);
        }

        if (WebpHeaderReader.IsWebp(data))
        {
            return WebpHeaderReader.TryRead(data, out header, out refusal);
        }

        header = null!;
        refusal = ImageRefusal.Format;

        return false;
    }
}
