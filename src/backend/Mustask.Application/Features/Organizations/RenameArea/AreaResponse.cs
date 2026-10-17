namespace Mustask.Application.Features.Organizations.RenameArea;

/// <summary>
/// <c>Area</c> of mod-017 api-contract § 1.6 (replacing mod-002 § 4.2), the shape every area write returns. The panel refetches the
/// tree after a write, because a move changes the paths of a whole subtree. The slice's own copy of the shape. An area has no
/// owner any more (BR-OT-10).
/// </summary>
public sealed record AreaResponse(Guid Id, Guid ProjectId, Guid ParentId, string Name);
