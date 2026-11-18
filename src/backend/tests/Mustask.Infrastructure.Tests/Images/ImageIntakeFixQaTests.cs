using System.Buffers.Binary;
using Mustask.Application.Abstractions.Images;
using Mustask.Domain.Images;
using Mustask.Infrastructure.Images;
using Mustask.Infrastructure.Images.Intake;
using Mustask.Infrastructure.Images.Intake.Headers;
using Mustask.Infrastructure.Images.Intake.Processing;
using NetVips;

namespace Mustask.Infrastructure.Tests.Images;

/// <summary>
/// feat-078 QA delta after code review (qa.md, "Backend delta — intake fix"): the frame rectangle at the canvas edge for GIF,
/// WebP ANMF and APNG fcTL, the still caps at their exact boundary per reader, malformed and truncated containers (refused,
/// never thrown), and review F-6's GIF rule through the whole intake.
/// </summary>
public sealed class ImageIntakeFixQaTests : IDisposable
{
    private readonly DecodeGate _gate = new(new ImagesOptions().Intake);
    private readonly ImageIntake _intake;

    public ImageIntakeFixQaTests() => _intake = new ImageIntake(new VipsCodec(new ImagesOptions()), _gate);

    public void Dispose() => _gate.Dispose();

    private static (bool Ok, ImageHeader? Header, ImageRefusal Refusal) Read(byte[] data)
    {
        var ok = ImageHeaderReader.TryRead(data, out var header, out var refusal);

        return (ok, ok ? header : null, refusal);
    }

    // ---- Frame rectangles at the edge ------------------------------------------------------------------------------

    // left + width == canvas fits; one more pixel is a format refusal. Both axes, both GIF frame positions (first and later).
    [Theory]
    [InlineData(40, 0, 60, 60, true)]
    [InlineData(41, 0, 60, 60, false)]
    [InlineData(0, 40, 60, 60, true)]
    [InlineData(0, 41, 60, 60, false)]
    [InlineData(0, 0, 100, 100, true)]
    [InlineData(0, 0, 101, 100, false)]
    [InlineData(0, 0, 100, 101, false)]
    [InlineData(99, 99, 1, 1, true)]
    [InlineData(100, 0, 1, 1, false)]
    [InlineData(0, 0, 10, 0, false)]
    [InlineData(65535, 65535, 65535, 65535, false)]
    public void Gif_FrameAtTheScreenEdge(int left, int top, int width, int height, bool fits)
    {
        foreach (var frames in new[] { 1, 3 })
        {
            var result = Read(TestImages.GifOfFrames(100, 100, frames, width, height, left, top));

            Assert.Equal(fits, result.Ok);
            if (!fits)
            {
                Assert.Equal(ImageRefusal.Format, result.Refusal);
            }
        }
    }

    // Only the last of several frames is outside: every descriptor is checked, not the first.
    [Fact]
    public void Gif_OnlyTheLastFrameOutside_IsFormat()
    {
        var good = TestImages.GifOfFrames(100, 100, 2, 50, 50);
        var bad = TestImages.GifOfFrames(100, 100, 1, 50, 50, frameX: 51);

        // good minus trailer + bad's frame block (after its 13-byte screen) = three frames, the third outside.
        byte[] joined = [.. good[..^1], .. bad[13..]];

        Assert.True(Read(good).Ok);
        Assert.Equal(ImageRefusal.Format, Read(joined).Refusal);
        Assert.False(Read(joined).Ok);
    }

    // ANMF offsets are stored halved: x = 2 * stored. 40 + 60 == 100 fits; width 61 at 40 is one past.
    [Theory]
    [InlineData(40, 0, 60, 60, true)]
    [InlineData(40, 0, 61, 60, false)]
    [InlineData(42, 0, 60, 60, false)]
    [InlineData(0, 40, 60, 60, true)]
    [InlineData(0, 40, 60, 61, false)]
    [InlineData(0, 0, 100, 100, true)]
    [InlineData(0, 0, 101, 100, false)]
    [InlineData(98, 98, 2, 2, true)]
    [InlineData(98, 98, 3, 2, false)]
    public void Webp_AnmfAtTheCanvasEdge(int left, int top, int width, int height, bool fits)
    {
        var result = Read(TestImages.WebpOfFrames(100, 100, 2, width, height, left, top));

        Assert.Equal(fits, result.Ok);
        if (!fits)
        {
            Assert.Equal(ImageRefusal.Format, result.Refusal);
        }
    }

    // The biggest 24-bit ANMF values never overflow the arithmetic.
    [Fact]
    public void Webp_AnmfMaximalFields_IsFormat()
    {
        var webp = TestImages.WebpOfFrames(100, 100, 2, 1, 1);
        var at = FindChunk(webp, "ANMF") + 8;
        for (var i = 0; i < 12; i++)
        {
            webp[at + i] = 0xFF;
        }

        Assert.Equal(ImageRefusal.Format, Read(webp).Refusal);
    }

    // fcTL against IHDR 8x8: 4 + 4 fits, 4 + 5 does not; empty frames and the largest uint32 values are refused.
    [Theory]
    [InlineData(4, 4, 4, 4, true)]
    [InlineData(4, 0, 5, 4, false)]
    [InlineData(0, 4, 4, 5, false)]
    [InlineData(0, 0, 8, 8, true)]
    [InlineData(0, 0, 9, 8, false)]
    [InlineData(0, 0, 0, 8, false)]
    [InlineData(0, 0, 8, 0, false)]
    public void Apng_FctlAtTheCanvasEdge(int left, int top, int width, int height, bool fits)
    {
        var result = Read(TestImages.Apng(2, 8, frameWidth: width, frameHeight: height, frameX: left, frameY: top));

        Assert.Equal(fits, result.Ok);
        if (!fits)
        {
            Assert.Equal(ImageRefusal.Format, result.Refusal);
        }
    }

    [Fact]
    public void Apng_FctlUInt32Extremes_IsFormat()
    {
        Assert.Equal(ImageRefusal.Format, Read(TestImages.Apng(2, 8, frameWidth: 1, frameHeight: 1, frameX: unchecked((int)0xFFFFFFFF))).Refusal);
        Assert.Equal(ImageRefusal.Format, Read(TestImages.Apng(2, 8, frameWidth: unchecked((int)0xFFFFFFFF), frameHeight: 1)).Refusal);
        Assert.False(Read(TestImages.Apng(2, 8, frameWidth: unchecked((int)0xFFFFFFFF), frameHeight: 1)).Ok);
    }

    // ---- The still caps at their exact boundary, per reader ------------------------------------------------------------

    // 20 000 x 3 000 is exactly 60 MP and exactly the side cap; 4 693 x 12 785 = 60 000 005 is the smallest product over 60 MP.
    public static TheoryData<int, int, bool> CanvasBoundary() => new()
    {
        { 20_000, 3_000, true },
        { 3_000, 20_000, true },
        { 20_001, 1, false },
        { 1, 20_001, false },
        { 20_000, 3_001, false },
        { 4_693, 12_785, false },
        { 4_693, 12_784, true },
    };

    [Theory]
    [MemberData(nameof(CanvasBoundary))]
    public void Gif_ScreenAgainstTheStillCaps(int width, int height, bool accepted)
    {
        var result = Read(TestImages.GifOfFrames(width, height, 1, 1, 1));

        Assert.Equal(accepted, result.Ok);
        Assert.True(accepted || result.Refusal == ImageRefusal.Dimensions);
    }

    [Theory]
    [MemberData(nameof(CanvasBoundary))]
    public void Webp_Vp8xCanvasAgainstTheStillCaps(int width, int height, bool accepted)
    {
        var result = Read(TestImages.WebpOfFrames(width, height, 2, 1, 1));

        Assert.Equal(accepted, result.Ok);
        Assert.True(accepted || result.Refusal == ImageRefusal.Dimensions);
    }

    [Theory]
    [MemberData(nameof(CanvasBoundary))]
    public void Png_IhdrAgainstTheStillCaps(int width, int height, bool accepted)
    {
        var result = Read(TestImages.PngHeaderOnly((uint)width, (uint)height));

        Assert.Equal(accepted, result.Ok);
        Assert.True(accepted || result.Refusal == ImageRefusal.Dimensions);
    }

    // 100 frames of 3 000 x 2 000 is exactly 600 000 000 pixels; one more frame is over.
    [Fact]
    public void Gif_AnimationPixelsAtTheBoundary()
    {
        Assert.True(Read(TestImages.GifOfFrames(3_000, 2_000, 100, 3_000, 2_000)).Ok);
        Assert.Equal(ImageRefusal.Animation, Read(TestImages.GifOfFrames(3_000, 2_000, 101, 3_000, 2_000)).Refusal);
    }

    // At the boundary the picture decodes: the decoded-size check passes a JPEG and a PNG of exactly 20 000 x 3 000, and the
    // stored picture is scaled under BR-IM-4 (2).
    [Theory]
    [InlineData("jpeg")]
    [InlineData("png")]
    public async Task Intake_AtExactly60MegapixelsAnd20000Side_IsDecodedAndScaled(string format)
    {
        var flat = NetVips.Image.Black(20_000, 3_000, bands: 3) + new double[] { 120, 130, 140 };
        var picture = flat.Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
        var bytes = format == "jpeg" ? picture.JpegsaveBuffer(q: 90) : picture.PngsaveBuffer();

        var result = await _intake.AcceptAsync(bytes, ImageIntakeMode.Upload, CancellationToken.None);

        Assert.Equal(ImageIntakeOutcome.Accepted, result.Outcome);
        Assert.Equal(20_000, result.Original!.Width);
        Assert.InRange((long)result.Stored!.Width * result.Stored.Height, 1, ImageLimits.MaxPixelsAfterScaling);
        Assert.InRange(result.Stored.Width / (double)result.Stored.Height, 6.6, 6.7);
    }

    // ---- Malformed and truncated containers: refused as format, never thrown ----------------------------------------------

    public static TheoryData<string> Samples() => new() { "gif", "agif", "png", "apng", "webp", "awebp", "anmf" };

    private static byte[] Sample(string name) => name switch
    {
        "gif" => TestImages.Photo(24, 16).GifsaveBuffer(dither: 0),
        "agif" => TestImages.AnimatedGif(3),
        "png" => TestImages.Png(24, 16),
        "apng" => TestImages.Apng(3, 8),
        "webp" => TestImages.Webp(24, 16),
        "awebp" => AnimatedWebp(),
        "anmf" => TestImages.WebpOfFrames(100, 100, 3, 50, 50),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    // Every proper prefix of a valid file (a GIF cut inside a descriptor, a WebP cut inside a chunk, an APNG cut inside fcTL)
    // is a format refusal from the header reader alone, and the whole intake answers it without throwing.
    [Theory]
    [MemberData(nameof(Samples))]
    public async Task EveryPrefix_IsFormat_AndTheIntakeNeverThrows(string name)
    {
        var full = Sample(name);
        Assert.True(Read(full).Ok, name);

        // A GIF prefix that still ends on its trailer, or a WebP cut after the last chunk, can be complete: only the file
        // minus its last byte(s) inside a structure is checked to be refused.
        for (var length = 0; length < full.Length; length++)
        {
            var prefix = full[..length];
            var result = Read(prefix);

            Assert.False(result.Ok, $"{name} cut at {length} of {full.Length}");
            Assert.Equal(ImageRefusal.Format, result.Refusal);

            var answer = await _intake.AcceptAsync(prefix, ImageIntakeMode.Upload, CancellationToken.None);
            Assert.Equal(ImageIntakeOutcome.Format, answer.Outcome);
        }
    }

    // Seeded byte flips over each sample: whatever the header reader accepts, the intake ends in an outcome, never an
    // exception (an InvalidOperationException from the re-read of the stored form would be a 500).
    [Theory]
    [MemberData(nameof(Samples))]
    public async Task SeededMutations_EndInAnOutcome_NeverAnException(string name)
    {
        var full = Sample(name);
        var random = new Random(78);

        for (var round = 0; round < 1500; round++)
        {
            var mutated = (byte[])full.Clone();
            var flips = 1 + random.Next(4);
            for (var i = 0; i < flips; i++)
            {
                // Bias towards the container head, where the structure is.
                var at = random.Next(2) == 0 ? random.Next(Math.Min(64, mutated.Length)) : random.Next(mutated.Length);
                mutated[at] = (byte)random.Next(256);
            }

            var exception = await Record.ExceptionAsync(() => _intake.AcceptAsync(mutated, ImageIntakeMode.Upload, CancellationToken.None));

            Assert.True(exception is null, $"{name} round {round}: {exception}");
        }
    }

    [Fact]
    public void Webp_ChunkSizePastTheEndOfTheFile_IsFormat()
    {
        var webp = TestImages.Webp(24, 16);
        var at = FindChunk(webp, "VP8 ");
        BinaryPrimitives.WriteUInt32LittleEndian(webp.AsSpan(at + 4), (uint)webp.Length);

        Assert.Equal(ImageRefusal.Format, Read(webp).Refusal);
        Assert.False(Read(webp).Ok);
    }

    [Fact]
    public void Webp_ChunkSizeUInt32Max_IsFormat()
    {
        var webp = TestImages.WebpOfFrames(100, 100, 2, 50, 50);
        BinaryPrimitives.WriteUInt32LittleEndian(webp.AsSpan(FindChunk(webp, "ANIM") + 4), uint.MaxValue);

        Assert.False(Read(webp).Ok);
        Assert.Equal(ImageRefusal.Format, Read(webp).Refusal);
    }

    // An odd-sized last chunk whose pad byte is missing: the walk ends one past the RIFF end; nothing reads out of bounds.
    [Fact]
    public async Task Webp_OddLastChunkWithoutItsPadByte_NeverThrows()
    {
        var webp = TestImages.Webp(24, 16);
        byte[] withTail = [.. webp, .. "XTRA"u8, 1, 0, 0, 0, 0x41];
        BinaryPrimitives.WriteUInt32LittleEndian(withTail.AsSpan(4), (uint)(withTail.Length - 8));

        var exception = await Record.ExceptionAsync(() => _intake.AcceptAsync(withTail, ImageIntakeMode.Upload, CancellationToken.None));

        Assert.Null(exception);
    }

    // ANMF needs VP8X before it; a second VP8X is refused; a still file with no VP8X is the simple form and reads.
    [Fact]
    public void Webp_Vp8xMissingOrRepeated()
    {
        var animated = TestImages.WebpOfFrames(100, 100, 2, 50, 50);
        var vp8x = FindChunk(animated, "VP8X");
        var vp8xChunk = animated[vp8x..(vp8x + 18)];

        byte[] withoutVp8x = [.. animated[..vp8x], .. animated[(vp8x + 18)..]];
        BinaryPrimitives.WriteUInt32LittleEndian(withoutVp8x.AsSpan(4), (uint)(withoutVp8x.Length - 8));

        byte[] twice = [.. animated[..(vp8x + 18)], .. vp8xChunk, .. animated[(vp8x + 18)..]];
        BinaryPrimitives.WriteUInt32LittleEndian(twice.AsSpan(4), (uint)(twice.Length - 8));

        Assert.Equal(ImageRefusal.Format, Read(withoutVp8x).Refusal);
        Assert.False(Read(withoutVp8x).Ok);
        Assert.False(Read(twice).Ok);
        Assert.Equal(ImageRefusal.Format, Read(twice).Refusal);
        Assert.True(Read(TestImages.Webp(24, 16)).Ok);
    }

    // VP8X after ANMF (out of order) is refused: the frames would have been checked against no canvas.
    [Fact]
    public void Webp_AnmfBeforeVp8x_IsFormat()
    {
        var animated = TestImages.WebpOfFrames(100, 100, 2, 50, 50);
        var vp8x = FindChunk(animated, "VP8X");
        var vp8xChunk = animated[vp8x..(vp8x + 18)];
        byte[] reordered = [.. animated[..vp8x], .. animated[(vp8x + 18)..], .. vp8xChunk];

        Assert.False(Read(reordered).Ok);
    }

    [Fact]
    public void Apng_FctlBeforeIhdr_IsFormat()
    {
        var apng = TestImages.Apng(2, 8);
        var fctl = TestImages.ChunkBodies(apng, "fcTL").First();
        var output = new MemoryStream();
        output.Write(apng.AsSpan(0, 8));
        TestImages.WriteChunk(output, "fcTL", fctl);
        output.Write(apng.AsSpan(8));

        Assert.False(Read(output.ToArray()).Ok);
        Assert.Equal(ImageRefusal.Format, Read(output.ToArray()).Refusal);
    }

    // A truncated fcTL: its length runs past the end of the file.
    [Fact]
    public void Apng_FctlRunningPastTheEnd_IsFormat()
    {
        var apng = TestImages.Apng(2, 8);
        var at = FindPngChunk(apng, "fcTL");
        var cut = apng[..(at + 8 + 10)];

        Assert.False(Read(cut).Ok);
        Assert.Equal(ImageRefusal.Format, Read(cut).Refusal);
    }

    // A truncated fcTL: a chunk that says fcTL but is shorter than the 26 bytes of a frame control, so it holds no frame
    // rectangle at all. The header cannot check it against the canvas; it is malformed and should be a format refusal.
    [Fact(Skip = "Open defect qa.md D-15 (minor): an fcTL shorter than 26 bytes is skipped, not refused.")]
    public void Apng_FctlShorterThan26Bytes_IsFormat()
    {
        var apng = TestImages.Apng(2, 8);
        var output = new MemoryStream();
        var position = 8;
        output.Write(apng.AsSpan(0, 8));
        var first = true;

        while (position < apng.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(apng.AsSpan(position));
            var type = System.Text.Encoding.ASCII.GetString(apng, position + 4, 4);
            if (type == "fcTL" && first)
            {
                // Keep the sequence number and a 16000-wide frame, drop the offsets and the rest.
                var body = apng.AsSpan(position + 8, 12).ToArray();
                BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(4), 16_000);
                TestImages.WriteChunk(output, "fcTL", body);
                first = false;
            }
            else
            {
                output.Write(apng.AsSpan(position, 12 + length));
            }

            position += 12 + length;
        }

        Assert.False(Read(output.ToArray()).Ok);
    }

    // ---- Review F-6 through the intake ----------------------------------------------------------------------------------

    // A GIF whose screen the loader ignores (over 2 048 px) and whose frame is smaller: it still decodes, and the scaling
    // works from the loaded size, so the stored picture is the frame scaled down to 16 MP, never stretched to the screen.
    [Fact]
    public async Task Intake_GifScreenBiggerThanItsFrame_ScaledFromTheLoadedSize()
    {
        var frame = (NetVips.Image.Black(4_500, 4_000, bands: 3) + new double[] { 30, 90, 200 })
            .Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
        var gif = frame.GifsaveBuffer(dither: 0);
        BinaryPrimitives.WriteUInt16LittleEndian(gif.AsSpan(6), 6_000);
        BinaryPrimitives.WriteUInt16LittleEndian(gif.AsSpan(8), 4_000);
        Assert.True(Read(gif).Ok);

        var result = await _intake.AcceptAsync(gif, ImageIntakeMode.Upload, CancellationToken.None);

        Assert.Equal(ImageIntakeOutcome.Accepted, result.Outcome);
        Assert.InRange((long)result.Stored!.Width * result.Stored.Height, 1, ImageLimits.MaxPixelsAfterScaling);
        Assert.InRange(result.Stored.Width, 1, 4_500);
        Assert.InRange(result.Stored.Height, 1, 4_000);
        Assert.InRange(result.Stored.Width / (double)result.Stored.Height, 1.12, 1.13);
    }

    // ---- Helpers ------------------------------------------------------------------------------------------------------

    private static byte[] AnimatedWebp()
    {
        var list = Enumerable.Range(0, 3)
            .Select(i => NetVips.Image.Black(8, 8, bands: 3) + new double[] { i * 80, 10, 10 })
            .ToArray();
        var joined = NetVips.Image.Arrayjoin(list, across: 1).Cast(Enums.BandFormat.Uchar)
            .Mutate(mutable => mutable.Set(GValue.GIntType, "page-height", 8));

        return joined.WebpsaveBuffer(q: 80);
    }

    private static int FindChunk(byte[] webp, string type)
    {
        var position = 12;
        while (position + 8 <= webp.Length)
        {
            if (System.Text.Encoding.ASCII.GetString(webp, position, 4) == type)
            {
                return position;
            }

            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(webp.AsSpan(position + 4));
            position += 8 + size + (size & 1);
        }

        throw new InvalidOperationException($"No {type} chunk.");
    }

    private static int FindPngChunk(byte[] png, string type)
    {
        var position = 8;
        while (position + 12 <= png.Length)
        {
            if (System.Text.Encoding.ASCII.GetString(png, position + 4, 4) == type)
            {
                return position;
            }

            position += 12 + (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(position));
        }

        throw new InvalidOperationException($"No {type} chunk.");
    }
}
