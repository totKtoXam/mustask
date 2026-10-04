using Mustask.Domain.Process;

namespace Mustask.Application.Abstractions.Process;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/process</c> (slice
/// <c>GetProcessDefinition</c>): the read side, straight from the store into
/// rows, bypassing the domain model (docs/conventions/backend.md: reads may go to
/// the database directly).
/// </summary>
public interface IProcessDefinitionReadModel
{
    /// <summary>
    /// The organization's process with its levels, types and states in display
    /// order (design R-25: the server fixes the order and the client never sorts),
    /// or <c>null</c> when no process exists for the identifier. It does not catch
    /// a failed read: a failure must stay a failure and never become <c>null</c>
    /// (BR-M2-11).
    /// </summary>
    Task<ProcessDefinitionRow?> FindByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);
}

/// <summary>The process: its levels by rank descending, its types by level rank descending and then by position.</summary>
public sealed record ProcessDefinitionRow(
    ProcessId Id,
    IReadOnlyList<BacklogLevelRow> BacklogLevels,
    IReadOnlyList<WorkItemTypeRow> WorkItemTypes);

public sealed record BacklogLevelRow(Guid Id, string Name, int Rank, BacklogLevelKind Kind);

/// <summary>A type with the level that hosts it and its states by position ascending.</summary>
public sealed record WorkItemTypeRow(
    Guid Id,
    string Key,
    string Name,
    AppearanceToken Appearance,
    Guid BacklogLevelId,
    string BacklogLevelName,
    IReadOnlyList<StateRow> States);

public sealed record StateRow(Guid Id, string Name, int Position, StateCategory Category);
