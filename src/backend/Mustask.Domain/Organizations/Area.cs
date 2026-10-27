using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Organizations;

/// <summary>
/// An area of the organization (mod-018, ADR-0028 rules 1, 4, 5): one tree per organization, each area with an optional parent of
/// the same organization, a name unique among its siblings (the top level included, BR-AW-4), an optional description and the stored
/// «Наследовать доступ» switch (BR-AW-5, OQ-2 B: on at creation, shown and changed by no one until <c>mod-019-scoped-access</c>).
/// An area <b>owns nothing</b> (BR-AW-2): projects and teams are linked to it (<see cref="AreaProject"/>, <see cref="AreaTeam"/>),
/// never stored in it. Areas have no status: an area with no child is deleted outright, and its links end with it.
/// </summary>
public sealed class Area
{
    private Area()
    {
        // EF materialization.
    }

    private Area(
        Guid id,
        Guid organizationId,
        Guid? parentId,
        EntityName name,
        EntityDescription? description,
        string createdBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        OrganizationId = organizationId;
        ParentId = parentId;
        Name = name.Value;
        NormalizedName = name.Normalized;
        Description = description?.Value;
        InheritsAccess = true;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    /// <summary><c>null</c> for a top-level area.</summary>
    public Guid? ParentId { get; private set; }

    public string Name { get; private set; } = null!;

    public string NormalizedName { get; private set; } = null!;

    /// <summary>The description as typed, or <c>null</c> when there is none (BR-AW-4).</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// «Наследовать доступ» (BR-AW-5): stored on for every area, the input of ADR-0029 § 6. No request changes it and no response
    /// shows it before <c>mod-019-scoped-access</c>.
    /// </summary>
    public bool InheritsAccess { get; private set; }

    /// <summary>The creator's Keycloak subject (<c>sub</c>).</summary>
    public string CreatedBy { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsTopLevel => ParentId is null;

    /// <summary>
    /// A new area under <paramref name="parentId"/> (<c>null</c>: the top level). The caller has judged, under the tree lock, that the
    /// parent is an area of the organization, that the name is free among the siblings and that the depth holds
    /// (<see cref="AreaTree.EnsureCanHold"/>).
    /// </summary>
    public static Area Create(
        Guid organizationId,
        Guid? parentId,
        EntityName name,
        EntityDescription? description,
        string createdBy,
        DateTimeOffset createdAt)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("An area belongs to an organization.", nameof(organizationId));
        }

        Organization.EnsureCreator(createdBy);

        return new Area(Guid.CreateVersion7(), organizationId, parentId, name, description, createdBy, createdAt);
    }

    /// <summary>Renames the area. Its children, links and parent do not change (BR-AW-4); the caller checked the siblings.</summary>
    public void Rename(EntityName name)
    {
        Name = name.Value;
        NormalizedName = name.Normalized;
    }

    /// <summary>Sets or clears the description (BR-AW-4).</summary>
    public void Describe(EntityDescription? description) => Description = description?.Value;

    /// <summary>
    /// Moves the area, with its whole subtree, under <paramref name="newParentId"/> (<c>null</c>: the top level). Refuses a
    /// destination that is the area itself or one of its descendants (BR-AW-3) and a tree that would pass
    /// <see cref="AreaTree.MaxDepth"/> levels. Returns <c>false</c> when the area already has that parent: nothing changes. The
    /// caller checks the destination's name among its children afterwards.
    /// </summary>
    public bool MoveUnder(Guid? newParentId, AreaTree tree)
    {
        if (newParentId is { } destination && tree.IsSelfOrDescendant(Id, destination))
        {
            throw new ConflictException(OrganizationErrorCodes.AreaMoveIntoOwnSubtree);
        }

        if (ParentId == newParentId)
        {
            return false;
        }

        tree.EnsureCanHold(newParentId, tree.HeightOf(Id));

        ParentId = newParentId;
        return true;
    }
}
