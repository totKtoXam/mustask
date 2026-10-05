using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Organizations;

/// <summary>
/// A node of a project's area tree (US-5, US-6). The root comes with the
/// project, is named after it, is owned by the first team for good and is
/// never moved, re-owned or deleted (BR-M1-11). Every other node has a parent
/// in the same project, a name unique among its siblings (BR-M1-12), and an
/// explicit owner or none, in which case it inherits from the nearest owned
/// ancestor (<see cref="AreaTree.ResolveOwner"/>). A team's default area is
/// marked on the area itself and is always owned explicitly by that team
/// (BR-M1-14). Areas have no status: an empty leaf is removed outright.
/// </summary>
public sealed class Area
{
    private Area()
    {
        // EF materialization.
    }

    private Area(Guid id, Guid projectId, Guid? parentId, EntityName name, Guid? ownerTeamId, Guid? defaultOfTeamId)
    {
        Id = id;
        ProjectId = projectId;
        ParentId = parentId;
        Name = name.Value;
        NormalizedName = name.Normalized;
        OwnerTeamId = ownerTeamId;
        DefaultOfTeamId = defaultOfTeamId;
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    /// <summary><c>null</c> for the root only.</summary>
    public Guid? ParentId { get; private set; }

    public string Name { get; private set; } = null!;

    public string NormalizedName { get; private set; } = null!;

    /// <summary>The explicit owner; <c>null</c> means the owner is inherited. Never <c>null</c> on the root.</summary>
    public Guid? OwnerTeamId { get; private set; }

    /// <summary>The team whose default area this is, or <c>null</c>. Equals <see cref="OwnerTeamId"/> when set (BR-M1-14).</summary>
    public Guid? DefaultOfTeamId { get; private set; }

    public bool IsRoot => ParentId is null;

    /// <summary>
    /// A new child of <paramref name="parent"/> (US-5), owned explicitly by
    /// <paramref name="ownerTeam"/> or by nobody, so that it inherits (US-6).
    /// The caller has resolved the owner as an active team of the parent's
    /// project (BR-M1-22) and checked the name is free among the parent's
    /// children (BR-M1-12).
    /// </summary>
    public static Area CreateChild(Area parent, EntityName name, Team? ownerTeam)
    {
        if (ownerTeam is not null && (ownerTeam.ProjectId != parent.ProjectId || !ownerTeam.IsActive))
        {
            throw new ArgumentException("An area's owner is an active team of the area's own project.", nameof(ownerTeam));
        }

        return new Area(Guid.CreateVersion7(), parent.ProjectId, parent.Id, name, ownerTeam?.Id, defaultOfTeamId: null);
    }

    internal static Area CreateRoot(Guid projectId, EntityName name, Guid firstTeamId) =>
        new(Guid.CreateVersion7(), projectId, parentId: null, name, firstTeamId, firstTeamId);

    internal static Area CreateDefaultChild(Area root, EntityName name, Guid teamId) =>
        new(Guid.CreateVersion7(), root.ProjectId, root.Id, name, teamId, teamId);

    /// <summary>
    /// The rule about the addressed node alone (api-contract § 0.4 step 2): the
    /// root is renamed only with its project and is never moved, re-owned or
    /// deleted (BR-M1-11).
    /// </summary>
    public void EnsureNotRoot()
    {
        if (IsRoot)
        {
            throw new ConflictException(OrganizationErrorCodes.AreaRootIsFixed);
        }
    }

    /// <summary>Renames a child node. Its children and its owner do not change (US-5).</summary>
    public void Rename(EntityName name)
    {
        EnsureNotRoot();
        Name = name.Value;
        NormalizedName = name.Normalized;
    }

    /// <summary>
    /// Moves the node, with its whole subtree, under <paramref name="newParent"/>
    /// (US-5). Explicit owners stay and inherited ones re-resolve by themselves
    /// (US-6). Refuses the root, and a destination that is the node itself or one
    /// of its descendants (BR-M1-12). Returns <c>false</c> when
    /// <paramref name="newParent"/> is already the parent: nothing changes.
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
    /// Sets the explicit owner, or clears it with <c>null</c> (US-6). Refuses the
    /// root, and a team's default area when the owner would change
    /// (<see cref="OrganizationErrorCodes.AreaIsTeamDefault"/>, naming
    /// <paramref name="defaultTeamName"/>). The caller has resolved the new owner
    /// as an active team of this project. Returns <c>false</c> when the owner is
    /// already <paramref name="ownerTeamId"/>: nothing changes.
    /// </summary>
    public bool SetOwner(Guid? ownerTeamId, string? defaultTeamName)
    {
        EnsureNotRoot();

        if (OwnerTeamId == ownerTeamId)
        {
            return false;
        }

        EnsureNotDefault(defaultTeamName);

        OwnerTeamId = ownerTeamId;
        return true;
    }

    /// <summary>
    /// Only an empty leaf that is no team's default is deleted (US-5, US-6,
    /// BR-M1-13, BR-M1-14). The root is refused first, then a node with
    /// children, then a default area.
    /// </summary>
    public void EnsureDeletable(bool hasChildren, string? defaultTeamName)
    {
        EnsureNotRoot();

        if (hasChildren)
        {
            throw new ConflictException(OrganizationErrorCodes.AreaNotEmpty);
        }

        EnsureNotDefault(defaultTeamName);
    }

    /// <summary>
    /// Makes this the default area of <paramref name="team"/> (US-4). Refuses an
    /// area the team does not own explicitly (BR-M1-14).
    /// </summary>
    public void MarkDefaultOf(Team team)
    {
        if (team.ProjectId != ProjectId)
        {
            throw new ArgumentException("A team's default area is an area of its own project.", nameof(team));
        }

        if (OwnerTeamId != team.Id)
        {
            throw new ConflictException(OrganizationErrorCodes.TeamDefaultAreaNotOwned);
        }

        DefaultOfTeamId = team.Id;
    }

    /// <summary>Removes the default mark. The owner stays.</summary>
    public void ClearDefault() => DefaultOfTeamId = null;

    internal void RenameAsRoot(EntityName name)
    {
        Name = name.Value;
        NormalizedName = name.Normalized;
    }

    private void EnsureNotDefault(string? defaultTeamName)
    {
        if (DefaultOfTeamId is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(defaultTeamName))
        {
            throw new ArgumentException("A default area's refusal names its team.", nameof(defaultTeamName));
        }

        throw new ConflictException(
            OrganizationErrorCodes.AreaIsTeamDefault,
            new ErrorParams { ["team"] = ErrorParam.Text(defaultTeamName) });
    }
}
