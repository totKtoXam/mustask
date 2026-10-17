namespace Mustask.Application.Features.Organizations.GetAreaTree;

/// <summary>
/// <c>AreaNode</c> of mod-017 api-contract § 1.6 (replacing mod-002 § 4.1): one node of the tree, in pre-order. An area owns
/// nothing now — it has no owner, no resolved owner and no team whose default it is (BR-OT-10).
/// </summary>
/// <param name="ParentId"><c>null</c> for the root only.</param>
/// <param name="Path">DC-9: the names below the root joined by « / »; the root's own name for the root.</param>
/// <param name="Depth">0 for the root.</param>
public sealed record AreaNodeResponse(
    Guid Id,
    Guid? ParentId,
    string Name,
    string Path,
    int Depth);
