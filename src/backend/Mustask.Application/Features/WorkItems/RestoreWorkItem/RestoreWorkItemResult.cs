using Mustask.Application.Abstractions.WorkItems;

namespace Mustask.Application.Features.WorkItems.RestoreWorkItem;

/// <param name="Item">The item as stored after the restore: live, in the restored state, its <c>deletion</c> <c>null</c>.</param>
public sealed record RestoreWorkItemResult(WorkItemView Item);
