using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Images;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Images;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Images.GetImage;

/// <summary>The read's request: the id as it came in the address, and the entity tag the client holds, if any.</summary>
public sealed record GetImageQuery(string ImageId, string? IfNoneMatch);

/// <summary>The read's answer: the entity tag, and the bytes unless the client's copy is current (304).</summary>
public sealed record GetImageResult(Guid ImageId, ImageFormat Format, string ETag, byte[]? Content)
{
    public bool NotModified => Content is null;
}

/// <summary>
/// <c>GET /api/images/{imageId}</c> (US-4, BR-IM-10, -11): the address carries no organization, so the access is decided per image —
/// an unbound upload is its uploader's alone, a bound one is the item's readers' (<see cref="IWorkItemReadAccess"/>, live). Every
/// refusal is the one 404, so nothing tells a swept, a foreign and a made-up image apart. A matching <c>If-None-Match</c> is 304,
/// after the access check.
/// </summary>
public sealed class GetImageHandler(IImageStore store, IWorkItemReadAccess readAccess, ICurrentUser currentUser)
{
    public async Task<GetImageResult> Handle(GetImageQuery query, CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(query.ImageId, "D", out var imageId))
        {
            throw NotFound();
        }

        var image = await store.FindAsync(imageId, cancellationToken) ?? throw NotFound();

        var allowed = image.BoundAt is null
            ? string.Equals(image.UploadedBy, currentUser.SubjectId, StringComparison.Ordinal)
            : image.WorkItemId is { } workItemId && await readAccess.MayReadAsync(workItemId, currentUser.SubjectId, cancellationToken);

        if (!allowed)
        {
            throw NotFound();
        }

        var etag = $"\"{imageId:D}\"";

        if (query.IfNoneMatch is { } sent && sent.Split(',').Select(value => value.Trim()).Contains(etag, StringComparer.Ordinal))
        {
            return new GetImageResult(imageId, image.Format, etag, null);
        }

        var content = await store.ReadContentAsync(imageId, cancellationToken) ?? throw NotFound();

        return new GetImageResult(imageId, image.Format, etag, content);
    }

    private static NotFoundException NotFound() => new(ImageErrorCodes.ImageNotFound);
}
