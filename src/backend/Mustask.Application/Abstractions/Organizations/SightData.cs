namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// What the sight rule of doc-016 BR-TD-12..-14 reads besides the caller's grants and the tree (feat-041): every project of the
/// organization with its «Видим всей организации», every team with its department (a deleted team has none), the project–team
/// links, and the teams the caller is a member of. Read once per request with the access set; nothing is cached beyond it.
/// </summary>
/// <param name="Projects">Project id to the flag «Видим всей организации», every status.</param>
/// <param name="Teams">Team id to its department, <c>null</c> for a deleted team, every status.</param>
/// <param name="TeamsOfProject">Project id to the ids of the teams linked to it.</param>
/// <param name="MemberOfTeams">The teams the caller is a member of.</param>
public sealed record SightData(
    IReadOnlyDictionary<Guid, bool> Projects,
    IReadOnlyDictionary<Guid, Guid?> Teams,
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> TeamsOfProject,
    IReadOnlySet<Guid> MemberOfTeams)
{
    /// <summary>Nothing read: the administrator's set, which sees everything and never asks.</summary>
    public static SightData None { get; } = new(
        new Dictionary<Guid, bool>(),
        new Dictionary<Guid, Guid?>(),
        new Dictionary<Guid, IReadOnlyList<Guid>>(),
        new HashSet<Guid>());
}
