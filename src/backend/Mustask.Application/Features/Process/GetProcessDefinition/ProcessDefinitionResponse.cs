namespace Mustask.Application.Features.Process.GetProcessDefinition;

/// <summary>
/// The body of <c>GET /api/organizations/{organizationId}/process</c>
/// (api-contract.md § 1.2). Camel-cased on the wire. Every value is always
/// present and nothing is <c>null</c>. The response never carries the
/// organization's name, an audit stamp, an icon or a colour.
/// </summary>
/// <param name="Id">The process's identity.</param>
/// <param name="BacklogLevels">The levels, ordered by rank descending.</param>
/// <param name="WorkItemTypes">The types in display order. The client renders them in the order received and does not sort.</param>
public sealed record ProcessDefinitionResponse(
    Guid Id,
    IReadOnlyList<BacklogLevelResponse> BacklogLevels,
    IReadOnlyList<WorkItemTypeResponse> WorkItemTypes);

/// <param name="Id">The level's stable identity.</param>
/// <param name="Name">The Russian name.</param>
/// <param name="Rank">Total order of the levels; a higher rank is a higher level.</param>
/// <param name="Kind"><c>portfolio</c>, <c>requirement</c> or <c>task</c>.</param>
public sealed record BacklogLevelResponse(Guid Id, string Name, int Rank, string Kind);

/// <param name="Id">The type's stable identity.</param>
/// <param name="Key"><c>EPIC</c>, <c>FEAT</c>, <c>US</c>, <c>BUG</c> or <c>TASK</c>.</param>
/// <param name="Name">The Russian name.</param>
/// <param name="Appearance">The appearance token: <c>epic</c>, <c>feature</c>, <c>story</c>, <c>task</c> or <c>bug</c>.</param>
/// <param name="BacklogLevel">The level hosting the type, equal to one entry of <see cref="ProcessDefinitionResponse.BacklogLevels"/>.</param>
/// <param name="States">The type's states, ordered by <see cref="StateResponse.Order"/> ascending.</param>
public sealed record WorkItemTypeResponse(
    Guid Id,
    string Key,
    string Name,
    string Appearance,
    BacklogLevelReferenceResponse BacklogLevel,
    IReadOnlyList<StateResponse> States);

/// <summary>The level a type is on: its identity and its name.</summary>
public sealed record BacklogLevelReferenceResponse(Guid Id, string Name);

/// <param name="Id">The state's own identity: two equal labels on two types are two states.</param>
/// <param name="Name">The Russian label.</param>
/// <param name="Order">1-based and dense; «Удалена» is always last.</param>
/// <param name="IsInitial"><c>true</c> for exactly one state, the first.</param>
/// <param name="Category">The state's category.</param>
public sealed record StateResponse(Guid Id, string Name, int Order, bool IsInitial, StateCategoryResponse Category);

/// <param name="Code"><c>Proposed</c>, <c>InProgress</c>, <c>Resolved</c>, <c>Completed</c> or <c>Removed</c>. Never shown to a user.</param>
/// <param name="Label">The Russian label: «Новые», «В работе», «Решённые», «Завершённые» or «Удалённые». The client shows this and holds no map of its own.</param>
public sealed record StateCategoryResponse(string Code, string Label);
