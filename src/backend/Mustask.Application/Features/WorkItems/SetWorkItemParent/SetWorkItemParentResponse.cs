using Mustask.Application.Abstractions.WorkItems;

namespace Mustask.Application.Features.WorkItems.SetWorkItemParent;

/// <summary>The body of api-contract § 3.2: the item's parent as stored after the write, or <c>null</c> (BR-H-3).</summary>
public sealed record SetWorkItemParentResponse(WorkItemRefView? Parent);
