using Mustask.Application.Abstractions.WorkItems;

namespace Mustask.Application.Features.WorkItems.CreateWorkItem;

/// <param name="Item">The item, as stored.</param>
/// <param name="Created"><c>false</c> for a replay of a submission that already created it (BR-M3-26): nothing new was created.</param>
public sealed record CreateWorkItemResult(WorkItemView Item, bool Created);
