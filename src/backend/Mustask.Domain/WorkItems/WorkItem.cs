using Mustask.Shared.Exceptions;

namespace Mustask.Domain.WorkItems;

/// <summary>
/// A work item of one project (spec § Data model): one of the five types of its
/// organization's process, a number from the project's one sequence, a title, a
/// plain-text description, a state of its type, an optional assignee from the
/// user directory and an area of its project. Its type, its project and its
/// number never change (BR-M3-1, BR-M3-2, BR-M3-5), so neither does its
/// identifier. It records who created it and when, and the time of its latest
/// save that changed a value (BR-M3-28).
/// </summary>
public sealed class WorkItem
{
    private WorkItem()
    {
        // EF materialization.
    }

    private WorkItem(
        Guid id,
        Guid projectId,
        int number,
        Guid typeId,
        Guid stateId,
        Guid areaId,
        string title,
        Guid creationRequestId,
        DateTimeOffset createdAt,
        string createdBy)
    {
        Id = id;
        ProjectId = projectId;
        Number = number;
        TypeId = typeId;
        StateId = stateId;
        AreaId = areaId;
        Title = title;
        Description = string.Empty;
        CreationRequestId = creationRequestId;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        ChangedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    /// <summary>The number in the project's sequence, 1 or more (BR-M3-5).</summary>
    public int Number { get; private set; }

    public Guid TypeId { get; private set; }

    public Guid StateId { get; private set; }

    /// <summary>The assignee's Keycloak user id, or <c>null</c> for «Не назначен» (BR-M3-22).</summary>
    public string? AssigneeId { get; private set; }

    public Guid AreaId { get; private set; }

    public string Title { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    /// <summary>The client's key of the submission that created the item (BR-M3-26): a replay of it creates nothing.</summary>
    public Guid CreationRequestId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The creator's Keycloak subject (<c>sub</c>).</summary>
    public string CreatedBy { get; private set; } = null!;

    /// <summary>The time of the latest save that changed a value; the creation time until then (BR-M3-28).</summary>
    public DateTimeOffset ChangedAt { get; private set; }

    /// <summary>
    /// The item's one parent, an item of the same project, or <c>null</c> (feat-001 BR-H-1). Written only by
    /// <see cref="WorkItemHierarchy.Write"/>, which holds the rules; a parent's deletion leaves it as it is (BR-H-8).
    /// </summary>
    public Guid? ParentId { get; private set; }

    /// <summary>
    /// A new item (US-1, BR-M3-20): its type's initial state, no assignee, an
    /// empty description and the project's root area. The caller has validated
    /// the title (<see cref="WorkItemText.ValidateTitle"/>), resolved the type in
    /// the organization's process and taken <paramref name="number"/> from the
    /// project's sequence.
    /// </summary>
    public static WorkItem Create(
        Guid projectId,
        int number,
        WorkItemTypeStates type,
        Guid rootAreaId,
        string title,
        Guid creationRequestId,
        DateTimeOffset createdAt,
        string createdBy)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        if (projectId == Guid.Empty || rootAreaId == Guid.Empty || creationRequestId == Guid.Empty)
        {
            throw new ArgumentException("A work item has a project, an area and the key of its creation.");
        }

        if (WorkItemText.ValidateTitle(title, out var trimmed) is not null || trimmed != title)
        {
            throw new ArgumentException("The title is validated and trimmed before the item is built.", nameof(title));
        }

        return new WorkItem(
            Guid.CreateVersion7(),
            projectId,
            number,
            type.TypeId,
            type.InitialStateId,
            rootAreaId,
            title,
            creationRequestId,
            createdAt,
            createdBy);
    }

    /// <summary>
    /// One save of the form (US-3, US-4, US-5): exactly the fields of
    /// <paramref name="edit"/> that are set, all of them or none (BR-M3-25). The
    /// state is checked before the assignee, and a refusal throws before anything
    /// changes. A value equal to the stored one changes nothing; when nothing
    /// changes, <see cref="ChangedAt"/> stays. Returns whether anything changed.
    /// </summary>
    /// <param name="type">The item's own type, as the organization's process holds it.</param>
    public bool Save(WorkItemEdit edit, WorkItemTypeStates type, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(edit);
        ArgumentNullException.ThrowIfNull(type);

        if (type.TypeId != TypeId)
        {
            throw new ArgumentException("The states are checked against the item's own type.", nameof(type));
        }

        // The checks first, so a refusal leaves the item as it was.
        if (edit.StateId is { } stateId && stateId != StateId && !type.Allows(stateId))
        {
            throw new ConflictException(WorkItemErrorCodes.StateNotAllowed);
        }

        if (edit.Assignee is { UserId: { } userId, IsAvailable: false } && userId != AssigneeId)
        {
            throw new ConflictException(WorkItemErrorCodes.AssigneeUnavailable);
        }

        var changed = false;

        if (edit.Title is { } title && title != Title)
        {
            Title = title;
            changed = true;
        }

        if (edit.Description is { } description && description != Description)
        {
            Description = description;
            changed = true;
        }

        if (edit.StateId is { } newStateId && newStateId != StateId)
        {
            StateId = newStateId;
            changed = true;
        }

        if (edit.Assignee is { } assignee && assignee.UserId != AssigneeId)
        {
            AssigneeId = assignee.UserId;
            changed = true;
        }

        if (edit.AreaId is { } areaId && areaId != AreaId)
        {
            AreaId = areaId;
            changed = true;
        }

        if (changed)
        {
            ChangedAt = now;
        }

        return changed;
    }

    /// <summary>The link's half on the child (BR-H-3): the new parent, or none, and the write's time.</summary>
    internal void LinkTo(Guid? parentId, DateTimeOffset now)
    {
        ParentId = parentId;
        ChangedAt = now;
    }

    /// <summary>The link's half on a live parent it adds or removes (BR-H-5): only «Изменён» moves.</summary>
    internal void MarkLinkChanged(DateTimeOffset now) => ChangedAt = now;
}

/// <summary>
/// The fields one save sends (BR-M3-25); <c>null</c> means "not sent, keep the
/// stored value". The caller has validated the texts, and has resolved the area
/// as an area of the item's project (BR-M3-23).
/// </summary>
public sealed record WorkItemEdit(
    string? Title = null,
    string? Description = null,
    Guid? StateId = null,
    AssigneeEdit? Assignee = null,
    Guid? AreaId = null);

/// <summary>
/// The sent assignee: a user id, or <c>null</c> for «Не назначен» (BR-M3-22).
/// <paramref name="IsAvailable"/> says whether the directory knows the user as
/// active; it matters only when the user is newly set, so an assignee who has
/// become inactive since stays on every save.
/// </summary>
public sealed record AssigneeEdit(string? UserId, bool IsAvailable);
