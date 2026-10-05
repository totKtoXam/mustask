using Gridify;
using Mustask.Application.Abstractions.WorkItems;

namespace Mustask.Application.Features.WorkItems.GetWorkItems;

/// <summary>The page in ADR-0015's shape, <c>{ count, data }</c>: <c>count</c> is the project's item count.</summary>
public sealed record GetWorkItemsResult(Paging<WorkItemListItemView> Page);
