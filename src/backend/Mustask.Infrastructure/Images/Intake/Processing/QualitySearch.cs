using Mustask.Domain.Images;

namespace Mustask.Infrastructure.Images.Intake.Processing;

/// <summary>The lowest quality that kept the similarity, its encoding and the similarity measured.</summary>
internal sealed record SearchResult(byte[] Bytes, int Quality, double Similarity);

/// <summary>
/// BR-IM-4 (4): a binary search over the quality of a lossy encoder for the lowest <c>Q</c> whose decoded result is similar
/// enough to the reference (plan-backend § 5.2). At most seven encodes (61 values, one optional probe at a hint first).
/// </summary>
internal static class QualitySearch
{
    public const int MinQuality = 40;
    public const int MaxQuality = 100;

    /// <summary>The smallest passing encoding, or <c>null</c> when even the highest quality is not similar enough.</summary>
    public static SearchResult? Find(
        IImageCodec codec, IPicture picture, ImageFormat format, double threshold, byte[] referenceLuma, int? hint = null)
    {
        var low = MinQuality;
        var high = MaxQuality;
        SearchResult? best = null;
        var first = hint is >= MinQuality and <= MaxQuality ? hint : null;

        while (low <= high)
        {
            var quality = first ?? ((low + high) / 2);
            first = null;

            var encoded = picture.Encode(new PictureEncoding(format, quality, Lossless: false));
            var luma = codec.DecodeLuma(encoded, format, out var width, out var height);

            if (width != picture.Width || height != picture.Height)
            {
                throw new InvalidOperationException("The candidate changed size.");
            }

            var similarity = Ssim.Mean(referenceLuma, luma, width, height);

            if (similarity >= threshold)
            {
                best = new SearchResult(encoded, quality, similarity);
                high = quality - 1;
            }
            else
            {
                low = quality + 1;
            }
        }

        return best;
    }
}

/// <summary>
/// BR-IM-20 (2): after the upload's own steps, the reduction goes to the lower similarity at the same size, then scales the
/// sides by 0.85, 0.72, 0.61, 0.52 and exactly 0.5 of the <b>original's</b> sides, never lower. A lossy format searches the
/// lowest quality that holds 0.95 at each size, starting from the previous step's quality; a lossless one only scales. The
/// first result within the stored cap wins; none is <c>null</c>.
/// </summary>
internal static class ReductionLadder
{
    private static readonly double[] Steps = [0.85, 0.72, 0.61, 0.52, ImageLimits.ReductionSideFloor];

    public static byte[]? Run(
        IImageCodec codec, IPicture picture, ImageFormat format, bool lossy, int originalWidth, int originalHeight, int? qualityHint)
    {
        var hint = qualityHint;

        if (lossy)
        {
            var found = QualitySearch.Find(codec, picture, format, ImageLimits.MinReductionSimilarity, picture.Luma(), hint);
            if (found is { Bytes.Length: <= ImageLimits.MaxStoredBytes })
            {
                return found.Bytes;
            }

            hint = found?.Quality ?? hint;
        }

        foreach (var step in Steps)
        {
            // Never below half of the original's sides; the half itself rounds up.
            var width = step == ImageLimits.ReductionSideFloor ? (int)Math.Ceiling(originalWidth * step) : (int)Math.Round(originalWidth * step);
            var height = step == ImageLimits.ReductionSideFloor ? (int)Math.Ceiling(originalHeight * step) : (int)Math.Round(originalHeight * step);

            if ((long)width * height >= (long)picture.Width * picture.Height || width < 1 || height < 1)
            {
                continue;
            }

            using var scaled = picture.Resize(width, height, format);

            if (lossy)
            {
                var found = QualitySearch.Find(codec, scaled, format, ImageLimits.MinReductionSimilarity, scaled.Luma(), hint);
                if (found is null)
                {
                    continue;
                }

                hint = found.Quality;
                if (found.Bytes.Length <= ImageLimits.MaxStoredBytes)
                {
                    return found.Bytes;
                }
            }
            else
            {
                var encoded = scaled.Encode(new PictureEncoding(format, 100, Lossless: true));
                if (encoded.Length <= ImageLimits.MaxStoredBytes)
                {
                    return encoded;
                }
            }
        }

        return null;
    }
}
