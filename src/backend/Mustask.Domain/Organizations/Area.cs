using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Organizations;

/// <summary>
/// The type of a node of the organization's tree (mod-018 BR-AW-0, ADR-0031). The organization itself is the tree's top and no
/// stored node (OQ-4 A).
/// </summary>
public enum AreaType
{
    /// <summary>«Департамент»: stands at the top level and does not nest (OQ-3 A); holds no item; teams link to it (OQ-8 A).</summary>
    Department = 1,

    /// <summary>«Проект»: a project's one node, under a department or at the top; created, renamed and deleted only with its project.</summary>
    Project = 2,

    /// <summary>«Область»: a sub-area, only below a project's node or another sub-area of the same project (OQ-7 A).</summary>
    Area = 3,
}

/// <summary>
/// A typed node of the organization's one area tree (mod-018, ADR-0031): each node has an optional parent of the same organization, a
/// name unique among its siblings (the top level included, departments and project nodes in one name space: OQ-12 A, BR-AW-4), an
/// optional description and the stored «Наследовать доступ» switch (BR-AW-5, OQ-2 B). A project's node and its sub-areas carry the
/// project's id (<see cref="ProjectId"/>): a work item's «Область» is one of them (BR-AW-11). A node <b>owns nothing</b> (BR-AW-2):
/// teams are linked to a department (<see cref="AreaTeam"/>), never stored in it; a project is placed under a department, not owned
/// by it. A node has no status: a leaf is deleted outright and a project's node only with its project.
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
        AreaType type,
        Guid? projectId,
        EntityName name,
        EntityDescription? description,
        string createdBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        OrganizationId = organizationId;
        ParentId = parentId;
        Type = type;
        ProjectId = projectId;
        Name = name.Value;
        NormalizedName = name.Normalized;
        Description = description?.Value;
        InheritsAccess = true;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    /// <summary><c>null</c> for a top-level node.</summary>
    public Guid? ParentId { get; private set; }

    public AreaType Type { get; private set; }

    /// <summary><c>null</c> for a department; the project's id for its node and every sub-area below it (never changes).</summary>
    public Guid? ProjectId { get; private set; }

    public string Name { get; private set; } = null!;

    public string NormalizedName { get; private set; } = null!;

    /// <summary>The description as typed, or <c>null</c> when there is none (BR-AW-4).</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// «Наследовать доступ» (BR-AW-5): stored on for every node, the input of ADR-0029 § 6. No request changes it and no response
    /// shows it before <c>mod-019-scoped-access</c>.
    /// </summary>
    public bool InheritsAccess { get; private set; }

    /// <summary>The creator's Keycloak subject (<c>sub</c>).</summary>
    public string CreatedBy { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsTopLevel => ParentId is null;

    /// <summary>A new department (BR-AW-3): at the top level, no parent. The caller judged the name among the top-level siblings.</summary>
    public static Area CreateDepartment(
        Guid organizationId,
        EntityName name,
        EntityDescription? description,
        string createdBy,
        DateTimeOffset createdAt)
    {
        EnsureWellFormed(organizationId, createdBy);

        return new Area(Guid.CreateVersion7(), organizationId, null, AreaType.Department, null, name, description, createdBy, createdAt);
    }

    /// <summary>
    /// The node of a new project (OQ-5 A): under <paramref name="parentId"/> (a department the caller locked, or <c>null</c>: the
    /// top level), named after the project.
    /// </summary>
    public static Area CreateProjectNode(
        Guid organizationId,
        Guid? parentId,
        Guid projectId,
        EntityName name,
        string createdBy,
        DateTimeOffset createdAt)
    {
        EnsureWellFormed(organizationId, createdBy);

        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project's node names its project.", nameof(projectId));
        }

        return new Area(Guid.CreateVersion7(), organizationId, parentId, AreaType.Project, projectId, name, null, createdBy, createdAt);
    }

    /// <summary>
    /// A new sub-area under <paramref name="parent"/>, which must be a project's node or a sub-area (OQ-7 A); it takes the parent's
    /// project. A department as the parent is <see cref="OrganizationErrorCodes.AreaTypeNotAllowed"/>.
    /// </summary>
    public static Area CreateSubArea(
        Area parent,
        EntityName name,
        EntityDescription? description,
        string createdBy,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(parent);

        if (parent.Type == AreaType.Department || parent.ProjectId is not { } projectId)
        {
            throw new ConflictException(OrganizationErrorCodes.AreaTypeNotAllowed);
        }

        EnsureWellFormed(parent.OrganizationId, createdBy);

        return new Area(Guid.CreateVersion7(), parent.OrganizationId, parent.Id, AreaType.Area, projectId, name, description, createdBy, createdAt);
    }

    /// <summary>
    /// Renames the node on its own. A project's node is renamed only with its project (OQ-10 A):
    /// <see cref="OrganizationErrorCodes.AreaRootIsFixed"/>. The caller checked the siblings.
    /// </summary>
    public void Rename(EntityName name)
    {
        EnsureNotProjectNode();
        Name = name.Value;
        NormalizedName = name.Normalized;
    }

    /// <summary>The project's rename, which carries its node's name with it (<see cref="Project.Rename"/>).</summary>
    internal void RenameWithProject(EntityName name)
    {
        Name = name.Value;
        NormalizedName = name.Normalized;
    }

    /// <summary>Sets or clears the description (BR-AW-4); every type may be described.</summary>
    public void Describe(EntityDescription? description) => Description = description?.Value;

    /// <summary>
    /// Moves the node, with its whole subtree, under <paramref name="newParent"/> (<c>null</c>: the top level). The rules of BR-AW-3:
    /// a department stays at the top (nothing to move); a project's node goes to the top or under a department; a sub-area goes
    /// under a node of its own project (another project's: <see cref="OrganizationErrorCodes.AreaOutsideProject"/>, a department or
    /// the top: <see cref="OrganizationErrorCodes.AreaTypeNotAllowed"/>). A destination that is the node itself or one of its
    /// descendants is refused, and so is a tree that would pass <see cref="AreaTree.MaxDepth"/> levels. Returns <c>false</c> when the
    /// node already has that parent: nothing changes. The caller checks the destination's name among its children afterwards.
    /// </summary>
    public bool MoveUnder(Area? newParent, AreaTree tree)
    {
        switch (Type)
        {
            case AreaType.Department:
                if (newParent is not null)
                {
                    throw new ConflictException(OrganizationErrorCodes.AreaTypeNotAllowed);
                }

                break;
            case AreaType.Project:
                if (newParent is not null && newParent.Type != AreaType.Department)
                {
                    throw new ConflictException(OrganizationErrorCodes.AreaTypeNotAllowed);
                }

                break;
            default:
                if (newParent is null || newParent.Type == AreaType.Department)
                {
                    throw new ConflictException(OrganizationErrorCodes.AreaTypeNotAllowed);
                }

                if (newParent.ProjectId != ProjectId)
                {
                    throw new ConflictException(OrganizationErrorCodes.AreaOutsideProject);
                }

                break;
        }

        // The type rules above come first (US-2): a destination the type forbids is type_not_allowed even when it lies inside the subtree.
        if (newParent is not null && tree.IsSelfOrDescendant(Id, newParent.Id))
        {
            throw new ConflictException(OrganizationErrorCodes.AreaMoveIntoOwnSubtree);
        }

        if (ParentId == newParent?.Id)
        {
            return false;
        }

        tree.EnsureCanHold(newParent?.Id, tree.HeightOf(Id));

        ParentId = newParent?.Id;
        return true;
    }

    /// <summary>
    /// A node is deleted only as a leaf holding no work item (BR-AW-9); a project's node only with its project:
    /// <see cref="OrganizationErrorCodes.AreaRootIsFixed"/> first, then <see cref="OrganizationErrorCodes.AreaNotEmpty"/>.
    /// </summary>
    public void EnsureDeletable(bool hasChildren, bool holdsWorkItems)
    {
        EnsureNotProjectNode();

        if (hasChildren || holdsWorkItems)
        {
            throw new ConflictException(OrganizationErrorCodes.AreaNotEmpty);
        }
    }

    /// <summary>A project's node is renamed and deleted only through its project (OQ-5 A, OQ-10 A).</summary>
    public void EnsureNotProjectNode()
    {
        if (Type == AreaType.Project)
        {
            throw new ConflictException(OrganizationErrorCodes.AreaRootIsFixed);
        }
    }

    private static void EnsureWellFormed(Guid organizationId, string createdBy)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("An area belongs to an organization.", nameof(organizationId));
        }

        Organization.EnsureCreator(createdBy);
    }
}
