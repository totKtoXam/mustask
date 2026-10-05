namespace Mustask.Application.Features.Organizations.SetAreaOwner;

/// <summary>
/// <c>Area</c> of api-contract § 4.1, the shape every area write returns. The
/// panel refetches the tree after a write, because the change can move the
/// resolved owners of a whole subtree. The slice's own copy of the shape.
/// </summary>
/// <param name="OwnerTeamId">The explicit owner; <c>null</c> means the owner is inherited.</param>
public sealed record AreaResponse(Guid Id, Guid ProjectId, Guid ParentId, string Name, Guid? OwnerTeamId);
