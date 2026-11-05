using Mustask.Application.Abstractions.WorkItems;

namespace Mustask.Application.Features.WorkItems.GetAssigneeOptions;

/// <summary>The options in the order the contract fixes (known users by display name, then the unknown by id); the wire is the array alone.</summary>
public sealed record GetAssigneeOptionsResult(IReadOnlyList<AssigneeFilterOptionView> Options);
