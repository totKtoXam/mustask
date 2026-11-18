namespace Mustask.Api.Features.Images;

/// <summary>
/// How many request bodies above the 5 MB upload cap one host holds at once (feat-078 review F-2, F-7). One per host, so a
/// test host with its own instance cannot starve another; a request that finds no free slot waits <see cref="Wait"/>, then gets 503.
/// </summary>
public sealed class UploadBodySlots(int count = UploadBodySlots.DefaultCount, TimeSpan? wait = null)
{
    public const int DefaultCount = 4;

    public int Count { get; } = count;

    public TimeSpan Wait { get; } = wait ?? TimeSpan.FromSeconds(5);

    internal SemaphoreSlim Semaphore { get; } = new(count, count);
}
