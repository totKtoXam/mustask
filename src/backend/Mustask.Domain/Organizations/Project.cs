namespace Mustask.Domain.Organizations;

/// <summary>
/// A project of an organization (US-3). Its name is unique in the organization
/// among projects of any status, and so is its key, which never changes
/// (BR-M1-1). It is created with <b>no team</b> (feat-028 BR-OL-2) and <b>no area</b> (mod-018 BR-AW-11): the teams that work in it
/// and the areas it belongs to are its links. It may carry a
/// description (BR-OL-3). Removed softly, like the organization (ADR-0026).
/// </summary>
public sealed class Project
{
    private Project()
    {
        // EF materialization.
    }

    private Project(
        Guid id,
        Guid organizationId,
        EntityName name,
        string key,
        EntityDescription? description,
        DateTimeOffset createdAt,
        string createdBy)
    {
        Id = id;
        OrganizationId = organizationId;
        Name = name.Value;
        NormalizedName = name.Normalized;
        Description = description?.Value;
        Key = key;
        Status = ProjectStatus.Active;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = null!;

    public string NormalizedName { get; private set; } = null!;

    /// <summary>The description as typed, or <c>null</c> when there is none (feat-028 BR-OL-3). A rename and a deactivation leave it.</summary>
    public string? Description { get; private set; }

    /// <summary>Set once at creation and never changed (BR-M1-1): there is no setter beyond EF's.</summary>
    public string Key { get; private set; } = null!;

    public ProjectStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The creator's Keycloak subject (<c>sub</c>, BR-M1-21).</summary>
    public string CreatedBy { get; private set; } = null!;

    /// <summary>
    /// mod-017 (BR-OT-4): the team the project's settings name as its default, a team <b>linked</b> to this project
    /// (feat-028 BR-OL-7), or <c>null</c> when none is named. The default team in force is <see cref="ProjectDefaultTeam.Resolve"/>:
    /// this, else the project's only linked active team, else none. It decides a creation's starting value and nothing else.
    /// </summary>
    public Guid? DefaultTeamId { get; private set; }

    public bool IsActive => Status == ProjectStatus.Active;

    /// <summary>
    /// Names the project's default team, or clears the setting with <c>null</c> (BR-OT-4). The caller has judged the team with
    /// <see cref="ProjectTeams.EnsureBindable"/> under the link's lock. Returns <c>false</c> when the setting already holds
    /// <paramref name="teamId"/>: nothing changes.
    /// </summary>
    public bool NameDefaultTeam(Guid? teamId)
    {
        if (DefaultTeamId == teamId)
        {
            return false;
        }

        DefaultTeamId = teamId;

        return true;
    }

    /// <summary>
    /// A new, active project (US-3). <b>No team and no area comes with it</b> and no one joins a team (feat-028 BR-OL-2, mod-018
    /// BR-AW-11). The caller has checked the key already (<see cref="ProjectKey"/>) and links the teams and areas the request names in
    /// the same operation.
    /// </summary>
    public static Project Create(
        Guid organizationId,
        EntityName name,
        string key,
        DateTimeOffset createdAt,
        string createdBy,
        EntityDescription? description = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("A project belongs to an organization.", nameof(organizationId));
        }

        if (!ProjectKey.IsWellFormed(key))
        {
            throw new ArgumentException("The key is checked before the project is built.", nameof(key));
        }

        Organization.EnsureCreator(createdBy);

        return new Project(Guid.CreateVersion7(), organizationId, name, key, description, createdAt, createdBy);
    }

    /// <summary>Sets or clears the description (BR-OL-3). The key, the name and the links do not change.</summary>
    public void Describe(EntityDescription? description) => Description = description?.Value;

    /// <summary>Renames the project (BR-AW-11: no area is renamed with it). The key and the links do not change.</summary>
    public void Rename(EntityName name)
    {
        Name = name.Value;
        NormalizedName = name.Normalized;
    }

    /// <summary>Writes this one row: nothing beneath changes status (ADR-0026 rule 2).</summary>
    public void Deactivate() => Status = ProjectStatus.Deactivated;

    /// <summary>Back to active, exactly as it was. Restoring an active project changes nothing.</summary>
    public void Restore() => Status = ProjectStatus.Active;
}
