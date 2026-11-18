using Mustask.Application.Abstractions.Images;
using Mustask.Domain.Images;
using Mustask.Infrastructure.Images.Intake.Headers;
using Mustask.Infrastructure.Images.Intake.Processing;
using Mustask.Infrastructure.Images.Intake.Stripping;

namespace Mustask.Infrastructure.Images.Intake;

/// <summary>
/// <see cref="IImageIntake"/> (ADR-0045 decision 2; plan-backend § 6.1): the header read (refusals), the strip, then the
/// decision. An animation is stored as stripped (an upload) or refused for a reduction; a still picture that needs nothing more
/// is stored as stripped; the rest enters the decode gate and is decoded, scaled, searched and, for a reduction, reduced.
/// </summary>
internal sealed class ImageIntake(IImageCodec codec, DecodeGate gate) : IImageIntake
{
    public async Task<ImageIntakeResult> AcceptAsync(ReadOnlyMemory<byte> upload, ImageIntakeMode mode, CancellationToken cancellationToken)
    {
        if (!ImageHeaderReader.TryRead(upload.Span, out var header, out var refusal))
        {
            return ImageIntakeResult.Refused(refusal switch
            {
                ImageRefusal.Dimensions => ImageIntakeOutcome.Dimensions,
                ImageRefusal.Animation => ImageIntakeOutcome.Animation,
                _ => ImageIntakeOutcome.Format,
            });
        }

        var original = new StoredImageFacts(header.Format, header.DisplayWidth, header.DisplayHeight, header.FrameCount, upload.Length);
        var stripped = ImageStripper.Strip(upload.Span, header);

        if (header.IsAnimated)
        {
            return mode == ImageIntakeMode.Reduction && upload.Length > ImageLimits.MaxStoredBytes
                ? ImageIntakeResult.Refused(ImageIntakeOutcome.AnimatedReduction)
                : Accepted(stripped, original);
        }

        var lossy = IsLossy(header);
        var needsWork = (long)header.Width * header.Height > ImageLimits.MaxPixelsAfterScaling
            || stripped.Length > ImageLimits.MaxStoredBytes
            || (lossy && stripped.Length > ImageLimits.ReencodeAboveBytes);

        if (!needsWork)
        {
            return Accepted(stripped, original);
        }

        using var permit = await gate.EnterAsync(cancellationToken);
        if (permit is null)
        {
            return ImageIntakeResult.Refused(ImageIntakeOutcome.Busy);
        }

        try
        {
            var content = await Task.Run(() => Process(stripped, header, lossy, mode), cancellationToken);

            return content is null ? ImageIntakeResult.Refused(ImageIntakeOutcome.CannotReduce) : Accepted(content, original);
        }
        catch (ImageDecodeException)
        {
            return ImageIntakeResult.Refused(ImageIntakeOutcome.Format);
        }
    }

    /// <summary>The bytes to store, or <c>null</c> when no step reaches the cap.</summary>
    private byte[]? Process(byte[] stripped, ImageHeader header, bool lossy, ImageIntakeMode mode)
    {
        using var picture = codec.Decode(stripped, header);
        var strippedFits = stripped.Length <= ImageLimits.MaxStoredBytes;
        byte[]? candidate = null;
        int? quality = null;

        if (lossy && mode == ImageIntakeMode.Reduction && !strippedFits)
        {
            // A reduction of a file over the cap: the lowest quality at 0.95 comes first. If even that does not fit, the 0.98
            // search could not either (a higher quality is larger), so it is not run, and the ladder continues from this result.
            var luma = picture.Luma();
            var rough = QualitySearch.Find(codec, picture, header.Format, ImageLimits.MinReductionSimilarity, luma);

            if (rough is not { Bytes.Length: <= ImageLimits.MaxStoredBytes })
            {
                return ReductionLadder.Run(codec, picture, header.Format, lossy, header.DisplayWidth, header.DisplayHeight, rough?.Quality, rough);
            }

            var fine = QualitySearch.Find(codec, picture, header.Format, ImageLimits.MinSimilarity, luma, minQuality: rough.Quality);

            return fine is { Bytes.Length: <= ImageLimits.MaxStoredBytes } ? fine.Bytes : rough.Bytes;
        }

        if (lossy)
        {
            var found = QualitySearch.Find(codec, picture, header.Format, ImageLimits.MinSimilarity, picture.Luma(), minQuality: QualitySearch.UploadFloorQuality);
            candidate = found?.Bytes;
            quality = found?.Quality;
        }
        else
        {
            candidate = picture.Encode(new PictureEncoding(header.Format, 100, Lossless: true));
        }

        // BR-IM-4 (5) wins: a result larger than what the stripped file already is, is never stored.
        if (strippedFits && (candidate is null || candidate.Length >= stripped.Length))
        {
            return stripped;
        }

        if (candidate is { Length: <= ImageLimits.MaxStoredBytes })
        {
            return candidate;
        }

        return mode == ImageIntakeMode.Reduction
            ? ReductionLadder.Run(codec, picture, header.Format, lossy, header.DisplayWidth, header.DisplayHeight, quality)
            : null;
    }

    private static bool IsLossy(ImageHeader header) =>
        header.Format == ImageFormat.Jpeg || (header.Format == ImageFormat.Webp && !header.IsLossless);

    /// <summary>The stored form is read again by the header reader: the caps hold on the output too (US-6 "passes every check").</summary>
    private static ImageIntakeResult Accepted(byte[] content, StoredImageFacts original)
    {
        if (!ImageHeaderReader.TryRead(content, out var stored, out var refusal))
        {
            throw new InvalidOperationException($"The processed image failed its own checks ({refusal}).");
        }

        if (content.Length is < 1 or > ImageLimits.MaxStoredBytes)
        {
            throw new InvalidOperationException("The processed image is outside the stored size.");
        }

        return new ImageIntakeResult(
            ImageIntakeOutcome.Accepted,
            content,
            new StoredImageFacts(stored.Format, stored.DisplayWidth, stored.DisplayHeight, stored.FrameCount, content.Length),
            original);
    }
}
