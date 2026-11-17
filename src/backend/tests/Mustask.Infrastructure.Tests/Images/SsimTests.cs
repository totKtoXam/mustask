using Mustask.Infrastructure.Images.Intake.Processing;

namespace Mustask.Infrastructure.Tests.Images;

public sealed class SsimTests
{
    private static byte[] Flat(byte value, int size) => Enumerable.Repeat(value, size * size).ToArray();

    // US-2 · "Quality search keeps similarity at 0.98": the measure is the textbook one (hand-computed values).
    [Fact]
    public void Mean_IdenticalPlanes_IsOne() =>
        Assert.Equal(1.0, Ssim.Mean(Flat(100, 16), Flat(100, 16), 16, 16), 9);

    [Fact]
    public void Mean_FlatPlanesOfDifferentLevel_MatchesTheFormula()
    {
        // (2*100*110 + C1) / (100^2 + 110^2 + C1), C1 = (0.01*255)^2.
        var c1 = Math.Pow(0.01 * 255, 2);
        var expected = ((2 * 100 * 110) + c1) / ((100 * 100) + (110 * 110) + c1);

        Assert.Equal(expected, Ssim.Mean(Flat(100, 16), Flat(110, 16), 16, 16), 7);
    }

    [Fact]
    public void Mean_BlackAgainstWhite_IsNearZero() =>
        Assert.InRange(Ssim.Mean(Flat(0, 16), Flat(255, 16), 16, 16), 0, 0.001);

    [Fact]
    public void Mean_CheckerboardAgainstItsInverse_IsNegative()
    {
        var a = new byte[16 * 16];
        var b = new byte[16 * 16];
        for (var i = 0; i < a.Length; i++)
        {
            a[i] = (byte)(((i / 16) + (i % 16)) % 2 == 0 ? 255 : 0);
            b[i] = (byte)(255 - a[i]);
        }

        Assert.True(Ssim.Mean(a, b, 16, 16) < -0.9);
    }
}
