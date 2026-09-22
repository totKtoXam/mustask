using Mustask.Application.Abstractions;

namespace Mustask.Application.Tests.TestDoubles;

public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
}
