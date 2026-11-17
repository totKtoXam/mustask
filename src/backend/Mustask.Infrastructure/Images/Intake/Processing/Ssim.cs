namespace Mustask.Infrastructure.Images.Intake.Processing;

/// <summary>
/// Structural similarity of two 8-bit luminance planes (plan-backend § 5.1; BR-IM-4 (4)): every 8 x 8 window at stride 1 with
/// uniform weights, <c>C1 = (0.01 * 255)^2</c>, <c>C2 = (0.03 * 255)^2</c>, the mean over all windows. Running column sums over a
/// window-high band keep memory at O(width) and time at O(pixels); stripes of window rows run in parallel. Our own, so that a
/// library change cannot move a threshold the spec names; pinned by unit tests against hand-computed values.
/// </summary>
internal static class Ssim
{
    private const int Window = 8;
    private const double C1 = (0.01 * 255) * (0.01 * 255);
    private const double C2 = (0.03 * 255) * (0.03 * 255);

    public static double Mean(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, int width, int height) =>
        Mean(a.ToArray(), b.ToArray(), width, height);

    public static double Mean(byte[] planeA, byte[] planeB, int width, int height)
    {
        var a = planeA.AsSpan();
        var b = planeB.AsSpan();

        if (a.Length != width * height || b.Length != width * height || width <= 0 || height <= 0)
        {
            throw new ArgumentException("Both planes must hold width x height samples.");
        }

        var window = Math.Min(Window, Math.Min(width, height));
        var windowRows = height - window + 1;
        var stripes = Math.Clamp(windowRows / 64, 1, Environment.ProcessorCount);
        var sums = new double[stripes];

        Parallel.For(0, stripes, stripe =>
        {
            var from = (int)((long)windowRows * stripe / stripes);
            var to = (int)((long)windowRows * (stripe + 1) / stripes);
            sums[stripe] = StripeSum(planeA, planeB, width, window, from, to);
        });

        return sums.Sum() / ((double)windowRows * (width - window + 1));
    }

    /// <summary>The sum of the SSIM of the windows whose top row is in [<paramref name="from"/>, <paramref name="to"/>).</summary>
    private static double StripeSum(byte[] a, byte[] b, int width, int window, int from, int to)
    {
        var columnA = new long[width];
        var columnB = new long[width];
        var columnAA = new long[width];
        var columnBB = new long[width];
        var columnAB = new long[width];

        for (var row = from; row < from + window; row++)
        {
            AddRow(a, b, width, row, +1, columnA, columnB, columnAA, columnBB, columnAB);
        }

        var count = (double)window * window;
        var total = 0.0;

        for (var y = from; y < to; y++)
        {
            long sumA = 0, sumB = 0, sumAA = 0, sumBB = 0, sumAB = 0;

            for (var x = 0; x < window; x++)
            {
                sumA += columnA[x];
                sumB += columnB[x];
                sumAA += columnAA[x];
                sumBB += columnBB[x];
                sumAB += columnAB[x];
            }

            for (var x = 0; ; x++)
            {
                var meanA = sumA / count;
                var meanB = sumB / count;
                var varianceA = (sumAA / count) - (meanA * meanA);
                var varianceB = (sumBB / count) - (meanB * meanB);
                var covariance = (sumAB / count) - (meanA * meanB);

                total += ((2 * meanA * meanB) + C1) * ((2 * covariance) + C2)
                    / (((meanA * meanA) + (meanB * meanB) + C1) * (varianceA + varianceB + C2));

                if (x + window >= width)
                {
                    break;
                }

                sumA += columnA[x + window] - columnA[x];
                sumB += columnB[x + window] - columnB[x];
                sumAA += columnAA[x + window] - columnAA[x];
                sumBB += columnBB[x + window] - columnBB[x];
                sumAB += columnAB[x + window] - columnAB[x];
            }

            if (y + 1 < to)
            {
                AddRow(a, b, width, y, -1, columnA, columnB, columnAA, columnBB, columnAB);
                AddRow(a, b, width, y + window, +1, columnA, columnB, columnAA, columnBB, columnAB);
            }
        }

        return total;
    }

    private static void AddRow(
        byte[] a, byte[] b, int width, int row, int sign, long[] columnA, long[] columnB, long[] columnAA, long[] columnBB, long[] columnAB)
    {
        var offset = row * width;

        for (var x = 0; x < width; x++)
        {
            long valueA = a[offset + x];
            long valueB = b[offset + x];

            columnA[x] += sign * valueA;
            columnB[x] += sign * valueB;
            columnAA[x] += sign * valueA * valueA;
            columnBB[x] += sign * valueB * valueB;
            columnAB[x] += sign * valueA * valueB;
        }
    }
}
