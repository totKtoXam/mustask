namespace Mustask.Domain.Process;

/// <summary>
/// Identity of an organization's process. It is the only typed id of the
/// module, because it is the only one that crosses a module seam: the
/// organization-creation flow records it (mod-001 api-contract § 2).
/// </summary>
public readonly record struct ProcessId(Guid Value)
{
    /// <summary>A fresh time-ordered (UUID v7) identity.</summary>
    public static ProcessId New() => new(Guid.CreateVersion7());
}
