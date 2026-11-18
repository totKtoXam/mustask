using Mustask.Domain.Images;

namespace Mustask.Infrastructure.Images.Intake.Processing;

/// <summary>The lowest quality that kept the similarity, its encoding and the similarity measured.</summary>
internal sealed record SearchResult(byte[] Bytes, int Quality, double Similarity);

/// <summary>
/// BR-IM-4 (4): a search over the quality of a lossy encoder for the lowest <c>Q</c> whose decoded result is similar enough to
/// the reference (plan-backend § 5.2). Tuned for the 5 s / 15 s targets: each round tries four qualities at once (encode,
/// decode and compare run in parallel), so the 61 values narrow to one in two rounds instead of six sequential encodes (a few hundred MB more per permit while a round runs). A
/// hint (the previous step's quality) centres the first round.
/// </summary>
internal static class QualitySearch
{
    public const int MinQuality = 40;
    public const int MaxQuality = 100;

    /// <summary>The floor of the upload's 0.98 search: photographs that need less are rare, and a narrower range is one round fewer (the 5 s target).</summary>
    public const int UploadFloorQuality = 60;

    private const int Probes = 4;
    private const int HintSpread = 4;

    /// <summary>The smallest passing encoding, or <c>null</c> when even the highest quality is not similar enough.</summary>
    public static SearchResult? Find(
        IImageCodec codec, IPicture picture, ImageFormat format, double threshold, byte[] referenceLuma, int? hint = null, int minQuality = MinQuality)
    {
        var low = Math.Max(MinQuality, minQuality);
        var high = MaxQuality;
        SearchResult? best = null;
        var useHint = hint is { } h && h >= low && h <= MaxQuality ? hint : null;

        while (low <= high)
        {
            var points = useHint is { } centre
                ? new[] { centre - (2 * HintSpread), centre - HintSpread, centre, centre + HintSpread, centre + (2 * HintSpread) }.Select(point => Math.Clamp(point, low, high)).Distinct().Order().ToArray()
                : Spread(low, high);
            useHint = null;

            var results = new SearchResult?[points.Length];

            Parallel.For(0, points.Length, index =>
            {
                var encoded = picture.Encode(new PictureEncoding(format, points[index], Lossless: false));
                var luma = codec.DecodeLuma(encoded, format, out var width, out var height);

                if (width != picture.Width || height != picture.Height)
                {
                    throw new InvalidOperationException("The candidate changed size.");
                }

                var similarity = Ssim.Mean(referenceLuma, luma, width, height);
                results[index] = similarity >= threshold ? new SearchResult(encoded, points[index], similarity) : null;
            });

            var passing = Array.FindIndex(results, result => result is not null);

            if (passing < 0)
            {
                low = points[^1] + 1;
                continue;
            }

            best = results[passing];
            high = points[passing] - 1;
            low = passing > 0 ? points[passing - 1] + 1 : low;
        }

        return best;
    }

    /// <summary>Up to <see cref="Probes"/> distinct qualities splitting [low, high] into equal parts.</summary>
    private static int[] Spread(int low, int high)
    {
        var count = high - low + 1;

        return Enumerable.Range(1, Probes)
            .Select(i => low + (int)((long)count * i / (Probes + 1)))
            .Select(point => Math.Min(point, high))
            .Distinct()
            .ToArray();
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
    private const double SkipAbove = 1.8;

    private static readonly double[] Steps =[0.85, 0.72, 0.61, 0.52, ImageLimits.ReductionSideFloor];

    public static byte[]? Run(
        IImageCodec codec, IPicture picture, ImageFormat format, bool lossy, int originalWidth, int originalHeight, int? qualityHint,
        SearchResult? initial = null)
    {
        var hint = qualityHint;
        long? fullSizeBytes = null;

        if (lossy)
        {
            // The caller may have run this very search already (the reduction's first probe): it is not repeated.
            var found = initial ?? QualitySearch.Find(codec, picture, format, ImageLimits.MinReductionSimilarity, picture.Luma(), hint);
            if (found is { Bytes.Length: <= ImageLimits.MaxStoredBytes })
            {
                return found.Bytes;
            }

            hint = found?.Quality ?? hint;
            fullSizeBytes = found?.Bytes.Length;
        }

        foreach (var step in Steps)
        {
            // Tuning of the 15 s target: the size of a lossy encode goes with the pixels, so a step whose estimate is far above
            // the cap is not tried (the exact half is always tried).
            if (fullSizeBytes is { } full && step != ImageLimits.ReductionSideFloor && full * step * step > SkipAbove * ImageLimits.MaxStoredBytes)
            {
                continue;
            }

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
