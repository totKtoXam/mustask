using Mustask.Application.Abstractions.WorkItems;

namespace Mustask.Application.Features.WorkItems.DeleteWorkItem;

/// <param name="Item">The item as stored after the deletion: its state «Удалена», its <c>deletion</c> set.</param>
public sealed record DeleteWorkItemResult(WorkItemView Item);
