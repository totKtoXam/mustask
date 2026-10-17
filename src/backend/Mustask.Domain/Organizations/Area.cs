using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Organizations;

/// <summary>
/// A node of a project's area tree (US-5, US-6): a classification path of a work item's «Область» until
/// <c>mod-018-area-workspace</c> (mod-017 BR-OT-10). The root comes with the project, is named after it and is never moved or
/// deleted (BR-M1-11). Every other node has a parent in the same project and a name unique among its siblings (BR-M1-12). An area
/// owns nothing and decides no team: it has no owner, a team has no default area, and no rule resolves a team through the tree.
/// Areas have no status: an empty leaf is removed outright.
/// </summary>
public sealed class Area
{
    private Area()
    {
        // EF materialization.
    }

    private Area(Guid id, Guid projectId, Guid? parentId, EntityName name)
    {
        Id = id;
        ProjectId = projectId;
        ParentId = parentId;
        Name = name.Value;
        NormalizedName = name.Normalized;
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    /// <summary><c>null</c> for the root only.</summary>
    public Guid? ParentId { get; private set; }

    public string Name { get; private set; } = null!;

    public string NormalizedName { get; private set; } = null!;

    public bool IsRoot => ParentId is null;

    /// <summary>
    /// A new child of <paramref name="parent"/> (US-5). The caller has checked the name is free among the parent's children
    /// (BR-M1-12).
    /// </summary>
    public static Area CreateChild(Area parent, EntityName name) =>
        new(Guid.CreateVersion7(), parent.ProjectId, parent.Id, name);

    internal static Area CreateRoot(Guid projectId, EntityName name) =>
        new(Guid.CreateVersion7(), projectId, parentId: null, name);

    /// <summary>
    /// The rule about the addressed node alone (api-contract § 0.4 step 2): the
    /// root is renamed only with its project and is never moved or
    /// deleted (BR-M1-11).
    /// </summary>
    public void EnsureNotRoot()
    {
        if (IsRoot)
        {
            throw new ConflictException(OrganizationErrorCodes.AreaRootIsFixed);
        }
    }

    /// <summary>Renames a child node. Its children do not change (US-5), and no team does.</summary>
    public void Rename(EntityName name)
    {
        EnsureNotRoot();
        Name = name.Value;
        NormalizedName = name.Normalized;
    }

    /// <summary>
    /// Moves the node, with its whole subtree, under <paramref name="newParent"/>
    /// (US-5). Refuses the root, and a destination that is the node itself or one
    /// of its descendants (BR-M1-12). Returns <c>false</c> when
    /// <paramref name="newParent"/> is already the parent: nothing changes. No item changes its team (BR-OT-10).
    /// </summary>
    public bool MoveUnder(Area newParent, AreaTree tree)
    {
        EnsureNotRoot();

        if (newParent.ProjectId != ProjectId)
        {
            throw new ArgumentException("A node never leaves its project (BR-M1-22).", nameof(newParent));
        }

        if (tree.IsSelfOrDescendant(Id, newParent.Id))
        {
            throw new ConflictException(OrganizationErrorCodes.AreaMoveIntoOwnSubtree);
        }

        if (ParentId == newParent.Id)
        {
            return false;
        }

        ParentId = newParent.Id;
        return true;
    }

    /// <summary>
    /// Only an empty leaf is deleted (US-5, US-6, BR-M1-13). The root is refused first, then a node with children or one that
    /// holds work items (mod-003 BR-M3-24, whatever their state). A team's default area is no reason any more: a team has none
    /// (mod-017 BR-OT-10).
    /// </summary>
    public void EnsureDeletable(bool hasChildren, bool holdsWorkItems)
    {
        EnsureNotRoot();

        if (hasChildren || holdsWorkItems)
        {
            throw new ConflictException(OrganizationErrorCodes.AreaNotEmpty);
        }
    }

    internal void RenameAsRoot(EntityName name)
    {
        Name = name.Value;
        NormalizedName = name.Normalized;
    }
}
