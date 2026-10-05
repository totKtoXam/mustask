namespace Mustask.Application.Features.Organizations.GetAreaTree;

/// <summary><c>AreaNode</c> of api-contract § 4.1: one node of the tree, in pre-order.</summary>
/// <param name="ParentId"><c>null</c> for the root only.</param>
/// <param name="Path">DC-9, as in <c>Team.defaultArea.path</c>.</param>
/// <param name="Depth">0 for the root.</param>
/// <param name="OwnerTeamId">The explicit owner; <c>null</c> means inherited. Never <c>null</c> on the root.</param>
/// <param name="ResolvedOwner">The nearest explicit owner up the tree, at the latest the root's (the first team).</param>
/// <param name="OwnerInherited"><c>true</c> when <paramref name="OwnerTeamId"/> is <c>null</c>.</param>
/// <param name="DefaultOfTeam">The team whose default area this is, or <c>null</c>.</param>
public sealed record AreaNodeResponse(
    Guid Id,
    Guid? ParentId,
    string Name,
    string Path,
    int Depth,
    Guid? OwnerTeamId,
    AreaTeamResponse ResolvedOwner,
    bool OwnerInherited,
    AreaTeamResponse? DefaultOfTeam);

/// <summary>A team as a node of the tree names it.</summary>
public sealed record AreaTeamResponse(Guid Id, string Name);
