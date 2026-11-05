namespace Mustask.Application.Abstractions.WorkItems;

/// <summary>
/// How the data of a line to come leaves with a purge (mod-005, ADR-0030 rule 7 (3)). A row that cannot exist without the item
/// carries a foreign key to <c>work_items</c> with <c>ON DELETE CASCADE</c> and needs no step. A step is for what a cascade
/// cannot do: a row that must be written on survivors (history's "parent purged" note on each child) or an effect outside the
/// database (the files of an attachment, through an outbox intent, ADR-0013). It is implemented in Infrastructure by the line
/// that owns the data, registered <c>AddScoped&lt;IWorkItemPurgeStep, …&gt;</c>, collected as
/// <c>IEnumerable&lt;IWorkItemPurgeStep&gt;</c> in registration order, and marked <see cref="PurgeStepCoversAttribute"/> for every
/// entity it handles, which is what the reference check (<c>WorkItemReferenceInventoryTests</c>) reads without a container.
/// This line registers none; it tests the contract with a fake step.
/// </summary>
public interface IWorkItemPurgeStep
{
    /// <summary>
    /// Before the transaction. May refuse (a <c>ConflictException</c>) and may do I/O; sees no child ids. mod-004's refusal while
    /// the Drive is unreachable (doc-002 US-10) is one. It is safe because the item's data is frozen while it is deleted
    /// (ADR-0030 rule 3): what the pre-flight saw is what the step meets, and a restore in between is covered by the outbox.
    /// </summary>
    Task PreflightAsync(WorkItemPurgeRequest request, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Inside the transaction, after the item and its children are locked and before the item is removed. Touches only its own
    /// tables, locking in ADR-0028 rule 8's order; does no external I/O (an outbox intent instead, ADR-0013); receives the
    /// item's id and identifier, the children's ids, the actor and the time, and never content. It never writes the purged
    /// item's own row: the purge holds that row tracked, and its <c>DELETE</c> checks the row's version.
    /// </summary>
    Task PurgeAsync(WorkItemPurge purge, CancellationToken cancellationToken);
}

/// <summary>What a purge is about, before the transaction: the item's ids and identifier, who purges it and when. Never content.</summary>
public sealed record WorkItemPurgeRequest(
    Guid OrganizationId, Guid ProjectId, Guid WorkItemId, string Identifier, string ActorId, DateTimeOffset At);

/// <summary>What a step meets inside the transaction: the request and the ids of the item's direct children (deleted ones too).</summary>
public sealed record WorkItemPurge(WorkItemPurgeRequest Request, IReadOnlyList<Guid> ChildIds);

/// <summary>
/// Declares an entity (a type of the EF model) that a <see cref="IWorkItemPurgeStep"/> removes or rewrites on a purge, so that the
/// reference check counts a reference to a work item from that entity as covered (ADR-0030 rule 7).
/// </summary>
/// <param name="entity">The entity type the step handles.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class PurgeStepCoversAttribute(Type entity) : Attribute
{
    public Type Entity { get; } = entity;
}
