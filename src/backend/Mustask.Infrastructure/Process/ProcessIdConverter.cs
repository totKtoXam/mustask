using Mustask.Domain.Process;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mustask.Infrastructure.Process;

/// <summary>
/// <see cref="ProcessId"/> ↔ <c>uuid</c>. It is registered model-wide in
/// <see cref="MustaskDbContext"/> (<c>ConfigureConventions</c>), so every
/// property of that type, in this module and in the one that records the id
/// (<c>mod-002</c>'s <c>Organization.ProcessId</c>), is mapped with no
/// configuration of its own.
/// </summary>
public sealed class ProcessIdConverter : ValueConverter<ProcessId, Guid>
{
    public ProcessIdConverter()
        : base(id => id.Value, value => new ProcessId(value))
    {
    }
}
