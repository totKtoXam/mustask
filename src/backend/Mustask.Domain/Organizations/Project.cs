namespace Mustask.Domain.Organizations;

/// <summary>
/// A project of an organization (US-3). Its name is unique in the organization
/// among projects of any status, and so is its key, which never changes
/// (BR-M1-1). It is created with its node in the organization's area tree (mod-018 BR-AW-0, OQ-5 A),
/// and <b>no team</b> (feat-028 BR-OL-2): the teams that work in it are its links. It may carry a
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
    /// A new, active project with its node (US-3). The node is named after the project, stands under a department or at the top, and owns nothing (mod-017
    /// BR-OT-10); <b>no team comes with the project</b> and no one joins one (feat-028 BR-OL-2). The caller has checked the key
    /// already (<see cref="ProjectKey"/>) and links the teams the request names in the same operation.
    /// </summary>
    public static NewProject Create(
        Guid organizationId,
        EntityName name,
        string key,
        DateTimeOffset createdAt,
        string createdBy,
        EntityDescription? description = null,
        Guid? parentAreaId = null)
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

        var project = new Project(Guid.CreateVersion7(), organizationId, name, key, description, createdAt, createdBy);
        var root = Area.CreateProjectNode(organizationId, parentAreaId, project.Id, name, createdBy, createdAt);

        return new NewProject(project, root);
    }

    /// <summary>Sets or clears the description (BR-OL-3). The key, the name and the links do not change.</summary>
    public void Describe(EntityDescription? description) => Description = description?.Value;

    /// <summary>Renames the project and its root with it (BR-M1-11). The key, the links and the rest of the tree do not change.</summary>
    public void Rename(EntityName name, Area root)
    {
        if (root.Type != AreaType.Project || root.ProjectId != Id)
        {
            throw new ArgumentException("The root renamed with a project is that project's own root.", nameof(root));
        }

        Name = name.Value;
        NormalizedName = name.Normalized;
        root.RenameWithProject(name);
    }

    /// <summary>Writes this one row: nothing beneath changes status (ADR-0026 rule 2).</summary>
    public void Deactivate() => Status = ProjectStatus.Deactivated;

    /// <summary>Back to active, exactly as it was. Restoring an active project changes nothing.</summary>
    public void Restore() => Status = ProjectStatus.Active;
}

/// <summary>What <see cref="Project.Create"/> builds: the project and its root, to be stored together. No team (feat-028 BR-OL-2).</summary>
public sealed record NewProject(Project Project, Area Root);
