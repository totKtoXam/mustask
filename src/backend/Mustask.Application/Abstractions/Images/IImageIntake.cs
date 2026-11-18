using Mustask.Domain.Images;

namespace Mustask.Application.Abstractions.Images;

/// <summary>Which request the bytes came with: an ordinary upload (5 MB) or a reduction (20 MB), api-contract § 1-2.</summary>
public enum ImageIntakeMode
{
    Upload,
    Reduction,
}

/// <summary>What the intake decided (ADR-0045 decision 2). Every refusal stores nothing.</summary>
public enum ImageIntakeOutcome
{
    Accepted,
    Format,
    Dimensions,
    Animation,
    AnimatedReduction,
    CannotReduce,
    Busy,
}

/// <summary>
/// The result of the intake. <see cref="Content"/> and <see cref="Stored"/> are set when accepted; <see cref="Original"/> is the
/// file as sent (display size, the body's byte length), for the reduction answer's «Было».
/// </summary>
public sealed record ImageIntakeResult(ImageIntakeOutcome Outcome, byte[]? Content, StoredImageFacts? Stored, StoredImageFacts? Original)
{
    public static ImageIntakeResult Refused(ImageIntakeOutcome outcome) => new(outcome, null, null, null);
}

/// <summary>
/// The port of the image intake (ADR-0045 decision 2): header read, container strip, scaling, quality search, reduction. The
/// implementation lives in Infrastructure with the only reference to libvips; the byte cap is the body reader's, not the intake's.
/// </summary>
public interface IImageIntake
{
    Task<ImageIntakeResult> AcceptAsync(ReadOnlyMemory<byte> upload, ImageIntakeMode mode, CancellationToken cancellationToken);
}
