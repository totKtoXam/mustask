namespace Mustask.Domain.Organizations;

/// <summary>
/// A team of an organization (feat-028 BR-OL-1, ADR-0028 rule 4) standing in one department (doc-016 BR-TD-1). It belongs to no project: the
/// projects it works in are its <see cref="ProjectTeam"/> links. Its name is unique among the organization's active teams and
/// freed when it is deleted (BR-OL-3, ADR-0026 rule 6). It has a name, an optional description, a status and the people in
/// it; what it owns is the items whose owning team it is, and no team is the first or fixed (mod-017 BR-OT-11).
/// </summary>
public sealed class Team
{
    private Team()
    {
        // EF materialization.
    }

    private Team(Guid id, Guid organizationId, Guid departmentId, string color, EntityName name, EntityDescription? description)
    {
        Id = id;
        OrganizationId = organizationId;
        DepartmentId = departmentId;
        Color = color;
        Name = name.Value;
        NormalizedName = name.Normalized;
        Description = description?.Value;
        Status = TeamStatus.Active;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    /// <summary>
    /// The department the team stands in (doc-016 BR-TD-1): required for an active team, <c>null</c> for a deleted one (its deletion
    /// clears it, so it never blocks a department).
    /// </summary>
    public Guid? DepartmentId { get; private set; }

    /// <summary>The palette key chosen at creation (BR-TD-20); never edited, kept when the team moves.</summary>
    public string Color { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string NormalizedName { get; private set; } = null!;

    /// <summary>The description as typed, or <c>null</c> when there is none (BR-OL-3).</summary>
    public string? Description { get; private set; }

    public TeamStatus Status { get; private set; }

    public bool IsActive => Status == TeamStatus.Active;

    /// <summary>
    /// A new, active team of the organization: a name, an optional description, no project and no member
    /// (BR-OL-2). The caller links it and adds its people in the same operation when the request names them.
    /// </summary>
    public static Team Create(Guid organizationId, Guid departmentId, string color, EntityName name, EntityDescription? description = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("A team belongs to an organization.", nameof(organizationId));
        }

        if (departmentId == Guid.Empty)
        {
            throw new ArgumentException("A team stands in a department.", nameof(departmentId));
        }

        if (!ColorPalette.IsKey(color))
        {
            throw new ArgumentException("The colour is a key of the palette.", nameof(color));
        }

        return new Team(Guid.CreateVersion7(), organizationId, departmentId, color, name, description);
    }

    /// <summary>Renames the team. What it owns, its people and its links do not change (US-1).</summary>
    public void Rename(EntityName name)
    {
        Name = name.Value;
        NormalizedName = name.Normalized;
    }

    /// <summary>Sets or clears the description (BR-OL-3).</summary>
    public void Describe(EntityDescription? description) => Description = description?.Value;

    /// <summary>
    /// Moves the team to another department (BR-TD-4): only the place changes, never the colour. Returns <c>false</c> when it already
    /// stands there. The caller judged the destination and that no project is linked.
    /// </summary>
    public bool MoveTo(Guid departmentId)
    {
        if (!IsActive || departmentId == Guid.Empty)
        {
            throw new InvalidOperationException("Only an active team moves, to a department.");
        }

        if (DepartmentId == departmentId)
        {
            return false;
        }

        DepartmentId = departmentId;
        return true;
    }

    /// <summary>
    /// Deletes the team for good (BR-OL-9, BR-OL-10): the status is terminal and the name is freed. There is no guard: a
    /// project's last team may go (the last-team rule of mod-017 BR-OT-11 is retired). The caller deletes in one operation
    /// over what the team owns in every project it is linked to — the items go to «Без команды», its places, its links and
    /// the default settings that named it go, its memberships end.
    /// </summary>
    public void Delete()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("A deleted team answers not found before it reaches the domain.");
        }

        DepartmentId = null;
        Status = TeamStatus.Deleted;
    }
}
