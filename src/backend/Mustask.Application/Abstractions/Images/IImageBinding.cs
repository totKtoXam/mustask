using Mustask.Domain.Images;

namespace Mustask.Application.Abstractions.Images;

/// <summary>
/// The bind of the images a description names, inside the caller's transaction (feat-078 BR-IM-6, ADR-0045 decision 4). It locks the
/// rows <c>FOR UPDATE</c> in id order — after the item's own lock, never before it — so a sweep that wants the same row and a
/// second save that names it are serialised on it, and decides every image by the domain's <see cref="Image.CheckBinding"/>.
/// </summary>
public interface IImageBinding
{
    /// <summary>
    /// <c>true</c> when every id is an image the rule may take (bound to the rule's item already, or unbound and the writer's own),
    /// and the unbound ones are bound at <paramref name="now"/>; <c>false</c> when any is missing or unavailable, with nothing
    /// updated — one answer for every kind of refusal, so it tells nothing about the image.
    /// </summary>
    Task<bool> LockAndBindAsync(IReadOnlyCollection<Guid> imageIds, ImageBindingRule rule, DateTimeOffset now, CancellationToken cancellationToken);
}
