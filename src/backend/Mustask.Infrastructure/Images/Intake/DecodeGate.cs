namespace Mustask.Infrastructure.Images.Intake;

/// <summary>
/// The process-wide limit on decodes (ADR-0045 decision 5): a fixed number run at once, a short queue waits, the rest are told
/// to come back. Bodies of animated and strip-only uploads never enter it.
/// </summary>
internal sealed class DecodeGate : IDisposable
{
    private readonly SemaphoreSlim _permits;
    private readonly int _queueLimit;
    private readonly TimeSpan _maxWait;
    private int _waiting;

    public DecodeGate(ImagesOptions.IntakeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _permits = new SemaphoreSlim(Math.Max(1, options.MaxConcurrentDecodes));
        _queueLimit = Math.Max(0, options.QueueLength);
        _maxWait = TimeSpan.FromSeconds(Math.Max(0, options.MaxWaitSeconds));
    }

    /// <summary>A permit to decode, or <c>null</c> when the queue is full or the wait ran out.</summary>
    public async Task<IDisposable?> EnterAsync(CancellationToken cancellationToken)
    {
        if (_permits.Wait(0, CancellationToken.None))
        {
            return new Lease(_permits);
        }

        if (Interlocked.Increment(ref _waiting) > _queueLimit)
        {
            Interlocked.Decrement(ref _waiting);

            return null;
        }

        try
        {
            return await _permits.WaitAsync(_maxWait, cancellationToken) ? new Lease(_permits) : null;
        }
        finally
        {
            Interlocked.Decrement(ref _waiting);
        }
    }

    public void Dispose() => _permits.Dispose();

    private sealed class Lease(SemaphoreSlim permits) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                permits.Release();
            }
        }
    }
}
