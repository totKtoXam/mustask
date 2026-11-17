namespace Mustask.Infrastructure.Images;

/// <summary>
/// Section <c>Images</c> (plan-backend § 10): operational values only. The caps, the 2 h window and the hourly cadence are the
/// spec's and live in <c>ImageLimits</c>.
/// </summary>
public sealed class ImagesOptions
{
    public const string SectionName = "Images";

    public IntakeOptions Intake { get; set; } = new();

    public SweepOptions Sweep { get; set; } = new();

    public sealed class IntakeOptions
    {
        /// <summary>Decodes that run at once (ADR-0045 decision 5), tuned on the stand.</summary>
        public int MaxConcurrentDecodes { get; set; } = 2;

        /// <summary>Requests that wait for a permit.</summary>
        public int QueueLength { get; set; } = 4;

        /// <summary>How long a request waits for a permit before the busy answer.</summary>
        public int MaxWaitSeconds { get; set; } = 10;

        /// <summary>The threads one libvips operation uses.</summary>
        public int VipsThreads { get; set; } = 2;
    }

    public sealed class SweepOptions
    {
        /// <summary>The integration test hosts turn the timer off.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Rows of one batch of the sweep's delete.</summary>
        public int BatchSize { get; set; } = 100;
    }
}
