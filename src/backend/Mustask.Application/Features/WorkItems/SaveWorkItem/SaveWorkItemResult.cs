using Mustask.Application.Abstractions.WorkItems;

namespace Mustask.Application.Features.WorkItems.SaveWorkItem;

/// <param name="Item">The item as stored after the save.</param>
public sealed record SaveWorkItemResult(WorkItemView Item);
