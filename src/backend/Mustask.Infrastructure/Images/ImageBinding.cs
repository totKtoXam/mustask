using Microsoft.EntityFrameworkCore;
using Mustask.Application.Abstractions.Images;
using Mustask.Domain.Images;

namespace Mustask.Infrastructure.Images;

/// <summary><see cref="IImageBinding"/> over the <c>images</c> schema: lock the rows in id order, decide by the domain, update by statement.</summary>
internal sealed class ImageBinding(MustaskDbContext context) : IImageBinding
{
    public async Task<bool> LockAndBindAsync(
        IReadOnlyCollection<Guid> imageIds, ImageBindingRule rule, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imageIds);
        ArgumentNullException.ThrowIfNull(rule);

        if (imageIds.Count == 0)
        {
            return true;
        }

        var wanted = imageIds.Distinct().Order().ToArray();

        // The lock names the rows and nothing else; the id order is the order every locker takes them in (no cycle between saves).
        var locked = await context.Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM images.images WHERE id = ANY({wanted}) ORDER BY id FOR UPDATE")
            .ToListAsync(cancellationToken);

        if (locked.Count != wanted.Length)
        {
            return false;
        }

        var images = await context.Images
            .AsNoTracking()
            .Where(image => wanted.Contains(image.Id))
            .ToListAsync(cancellationToken);

        var bindable = new List<Guid>();

        foreach (var image in images)
        {
            switch (image.CheckBinding(rule))
            {
                case ImageBindingVerdict.Unavailable:
                    return false;
                case ImageBindingVerdict.Bindable:
                    bindable.Add(image.Id);
                    break;
            }
        }

        if (bindable.Count > 0)
        {
            var ids = bindable.ToArray();
            await context.Images
                .Where(image => ids.Contains(image.Id))
                .ExecuteUpdateAsync(set => set.SetProperty(image => image.BoundAt, now), cancellationToken);
        }

        return true;
    }
}
