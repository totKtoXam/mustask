using Mustask.Domain.Images;
using Mustask.Infrastructure.Images.Intake.Headers;
using NetVips;

namespace Mustask.Infrastructure.Images.Intake.Processing;

/// <summary>A structurally valid file that the decoder still refuses (a corrupt body): the intake answers it as a format refusal.</summary>
internal sealed class ImageDecodeException(string message, Exception inner) : Exception(message, inner)
{
    public ImageDecodeException(string message)
        : this(message, null!)
    {
    }
}

/// <summary>
/// <see cref="IImageCodec"/> on libvips through NetVips (plan-backend § 5). Loaders are called by name, never by sniffing
/// (<c>NewFromBuffer</c>), with <c>fail-on: error</c> and sequential access; only the folder this class sits in names NetVips.
/// </summary>
internal sealed class VipsCodec : IImageCodec
{
    public VipsCodec(ImagesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        VipsSetup.EnsureInitialized(options.Intake.VipsThreads);
    }

    public IPicture Decode(byte[] stripped, ImageHeader header)
    {
        try
        {
            var pixels = (double)header.Width * header.Height;
            var target = (double)ImageLimits.MaxPixelsAfterScaling;
            var shrink = 1;

            if (header.Format == ImageFormat.Jpeg && pixels > target)
            {
                var ratio = Math.Sqrt(pixels / target);
                shrink = ratio >= 8 ? 8 : ratio >= 4 ? 4 : ratio >= 2 ? 2 : 1;
            }

            var loaded = Load(stripped, header.Format, shrink);
            var scaled = loaded;

            // Defence in depth (BR-IM-3 (c)): whatever the header said, a decoder that produced another size is not trusted.
            var expectedWidth = (header.Width + shrink - 1) / shrink;
            var expectedHeight = (header.Height + shrink - 1) / shrink;
            var slack = shrink > 1 ? shrink : 0;
            if (Math.Abs(loaded.Width - expectedWidth) > slack || Math.Abs(loaded.Height - expectedHeight) > slack)
            {
                loaded.Dispose();

                throw new ImageDecodeException("The decoded size differs from the header's.");
            }

            if (pixels > target)
            {
                var factor = Math.Sqrt(target / pixels);
                var width = Math.Max(1, (int)Math.Floor(header.Width * factor));
                var height = Math.Max(1, (int)Math.Floor(header.Height * factor));

                // A full size of 1 means nothing to scale; otherwise the integers above never exceed the cap.
                if (width != loaded.Width || height != loaded.Height)
                {
                    scaled = loaded.Resize(
                        (double)width / loaded.Width,
                        kernel: header.Format == ImageFormat.Gif ? Enums.Kernel.Nearest : Enums.Kernel.Lanczos3,
                        vscale: (double)height / loaded.Height);
                }
            }

            // The scaled picture is pulled into memory once, before any rotation, so a rotation never holds more than it.
            var picture = scaled.CopyMemory();

            if (picture.Interpretation == Enums.Interpretation.Cmyk)
            {
                picture = ToSrgb(picture);
            }

            if (header.Orientation > 1)
            {
                picture = picture.Mutate(mutable => mutable.Set(GValue.GIntType, "orientation", header.Orientation)).Autorot();
            }

            return new VipsPicture(picture);
        }
        catch (VipsException exception)
        {
            throw new ImageDecodeException("The decoder refused the image.", exception);
        }
    }

    public byte[] DecodeLuma(byte[] encoded, ImageFormat format, out int width, out int height)
    {
        try
        {
            using var image = Load(encoded, format, shrink: 1);
            width = image.Width;
            height = image.Height;

            return VipsPicture.LumaOf(image);
        }
        catch (VipsException exception)
        {
            throw new ImageDecodeException("The decoder refused the image.", exception);
        }
    }

    private static NetVips.Image Load(byte[] data, ImageFormat format, int shrink) => format switch
    {
        ImageFormat.Jpeg => NetVips.Image.JpegloadBuffer(data, shrink: shrink, access: Enums.Access.Sequential, failOn: Enums.FailOn.Error),
        ImageFormat.Png => NetVips.Image.PngloadBuffer(data, access: Enums.Access.Sequential, failOn: Enums.FailOn.Error),
        ImageFormat.Gif => NetVips.Image.GifloadBuffer(data, access: Enums.Access.Sequential, failOn: Enums.FailOn.Error),
        ImageFormat.Webp => NetVips.Image.WebploadBuffer(data, access: Enums.Access.Sequential, failOn: Enums.FailOn.Error),
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    private static NetVips.Image ToSrgb(NetVips.Image picture)
    {
        try
        {
            return picture.IccTransform("srgb", embedded: true);
        }
        catch (VipsException)
        {
            return picture.Colourspace(Enums.Interpretation.Srgb);
        }
    }
}

internal sealed class VipsPicture(NetVips.Image image) : IPicture
{
    public int Width => image.Width;

    public int Height => image.Height;

    public IPicture Resize(int width, int height, ImageFormat format) =>
        new VipsPicture(image.Resize(
            (double)width / image.Width,
            kernel: format == ImageFormat.Gif ? Enums.Kernel.Nearest : Enums.Kernel.Lanczos3,
            vscale: (double)height / image.Height));

    public byte[] Encode(PictureEncoding encoding)
    {
        try
        {
            return encoding.Format switch
            {
                ImageFormat.Jpeg => image.JpegsaveBuffer(
                    q: encoding.Quality,
                    optimizeCoding: true,
                    interlace: false,
                    subsampleMode: Enums.ForeignSubsample.Auto,
                    keep: Enums.ForeignKeep.Icc),
                ImageFormat.Webp => image.WebpsaveBuffer(q: encoding.Quality, lossless: encoding.Lossless, effort: 4, keep: Enums.ForeignKeep.Icc),
                ImageFormat.Png => image.PngsaveBuffer(compression: 9, interlace: false, palette: false, keep: Enums.ForeignKeep.Icc),
                ImageFormat.Gif => image.GifsaveBuffer(reuse: true, dither: 0, keep: Enums.ForeignKeep.None),
                _ => throw new ArgumentOutOfRangeException(nameof(encoding)),
            };
        }
        catch (VipsException exception)
        {
            throw new ImageDecodeException("The encoder refused the picture.", exception);
        }
    }

    public byte[] Luma() => LumaOf(image);

    public void Dispose() => image.Dispose();

    /// <summary>The luminance plane of an 8-bit picture; a grey picture is its own luminance.</summary>
    internal static byte[] LumaOf(NetVips.Image source)
    {
        var picture = source.Format == Enums.BandFormat.Uchar ? source : source.Cast(Enums.BandFormat.Uchar);
        NetVips.Image luma;

        if (picture.Bands >= 3)
        {
            luma = picture.ExtractBand(0, n: 3).Recomb(NetVips.Image.NewFromArray(new[,] { { 0.299, 0.587, 0.114 } }));
        }
        else
        {
            luma = picture.ExtractBand(0);
        }

        // Rounded, not truncated, so the same function on both sides of a comparison cancels itself.
        return (luma + 0.5).Cast(Enums.BandFormat.Uchar).WriteToMemory<byte>();
    }
}
