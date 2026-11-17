using Mustask.Domain.Images;

namespace Mustask.Application.Abstractions.Images;

/// <summary>An image's metadata as the read decides on it: no bytes.</summary>
public sealed record ImageRecord(Guid Id, ImageFormat Format, int ByteLength, string UploadedBy, DateTimeOffset? BoundAt, Guid? WorkItemId);

/// <summary>The store of the <c>images</c> schema (ADR-0045): the row and its bytes are written together, the bytes are read apart.</summary>
public interface IImageStore
{
    /// <summary>Adds the row and its content; <c>IUnitOfWork.SaveChangesAsync</c> flushes both in the caller's transaction.</summary>
    void Add(Image image, byte[] content);

    /// <summary>The metadata of the image, or <c>null</c>; no content is read.</summary>
    Task<ImageRecord?> FindAsync(Guid imageId, CancellationToken cancellationToken);

    /// <summary>The bytes of the image, or <c>null</c> when it is gone.</summary>
    Task<byte[]?> ReadContentAsync(Guid imageId, CancellationToken cancellationToken);
}
