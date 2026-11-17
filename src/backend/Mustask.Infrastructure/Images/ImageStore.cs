using Microsoft.EntityFrameworkCore;
using Mustask.Application.Abstractions.Images;
using Mustask.Domain.Images;

namespace Mustask.Infrastructure.Images;

/// <summary><see cref="IImageStore"/> over the <c>images</c> schema (ADR-0045): metadata and bytes are separate rows.</summary>
internal sealed class ImageStore(MustaskDbContext context) : IImageStore
{
    public void Add(Image image, byte[] content)
    {
        context.Images.Add(image);
        context.ImageContents.Add(new ImageContent(image.Id, content));
    }

    public Task<ImageRecord?> FindAsync(Guid imageId, CancellationToken cancellationToken) =>
        context.Images
            .AsNoTracking()
            .Where(image => image.Id == imageId)
            .Select(image => new ImageRecord(image.Id, image.Format, image.ByteLength, image.UploadedBy, image.BoundAt, image.WorkItemId))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<byte[]?> ReadContentAsync(Guid imageId, CancellationToken cancellationToken) =>
        context.ImageContents
            .AsNoTracking()
            .Where(content => content.ImageId == imageId)
            .Select(content => (byte[]?)content.Content)
            .SingleOrDefaultAsync(cancellationToken);
}
