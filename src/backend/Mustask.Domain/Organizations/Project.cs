namespace Mustask.Domain.Organizations;

/// <summary>
/// A project of an organization (US-3). Its name is unique in the organization
/// among projects of any status, and so is its key, which never changes
/// (BR-M1-1). It is created ready to use: with one team named after it and its root
/// area (mod-017 BR-OT-11, until feat-028 and mod-018 remove them): the team an ordinary one, the
/// root owned by nobody. Removed softly, like the organization
/// (ADR-0026).
/// </summary>
public sealed class Project
{
    private Project()
    {
        // EF materialization.
    }

    private Project(Guid id, Guid organizationId, EntityName name, string key, DateTimeOffset createdAt, string createdBy)
    {
        Id = id;
        OrganizationId = organizationId;
        Name = name.Value;
        NormalizedName = name.Normalized;
        Key = key;
        Status = ProjectStatus.Active;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = null!;

    public string NormalizedName { get; private set; } = null!;

    /// <summary>Set once at creation and never changed (BR-M1-1): there is no setter beyond EF's.</summary>
    public string Key { get; private set; } = null!;

    public ProjectStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The creator's Keycloak subject (<c>sub</c>, BR-M1-21).</summary>
    public string CreatedBy { get; private set; } = null!;

    /// <summary>
    /// mod-017 (BR-OT-4): the team the project's settings name as its default, a team of this project, or <c>null</c> when
    /// none is named. The default team in force is <see cref="ProjectDefaultTeam.Resolve"/>: this, else the project's only
    /// active team, else none. It decides a creation's starting value and nothing else.
    /// </summary>
    public Guid? DefaultTeamId { get; private set; }

    public bool IsActive => Status == ProjectStatus.Active;

    /// <summary>
    /// Names the project's default team, or clears the setting with <c>null</c> (BR-OT-4). The caller has judged the team with
    /// <see cref="ProjectTeams.EnsureBindable"/> under its lock. Returns <c>false</c> when the setting already holds
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
    /// A new, active project with its team and its root (US-3 "created
    /// ready to use"). The team and the root are both named after the
    /// project; the team is an ordinary one and the root belongs to nobody
    /// (mod-017 BR-OT-10, BR-OT-11). The caller has checked the key already (<see cref="ProjectKey"/>).
    /// </summary>
    public static NewProject Create(Guid organizationId, EntityName name, string key, DateTimeOffset createdAt, string createdBy)
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

        var project = new Project(Guid.CreateVersion7(), organizationId, name, key, createdAt, createdBy);
        var team = Team.Create(project.Id, name);
        var root = Area.CreateRoot(project.Id, name);

        return new NewProject(project, team, root);
    }

    /// <summary>Renames the project and its root with it (BR-M1-11). The key, the teams and the rest of the tree do not change.</summary>
    public void Rename(EntityName name, Area root)
    {
        if (!root.IsRoot || root.ProjectId != Id)
        {
            throw new ArgumentException("The root renamed with a project is that project's own root.", nameof(root));
        }

        Name = name.Value;
        NormalizedName = name.Normalized;
        root.RenameAsRoot(name);
    }

    /// <summary>Writes this one row: nothing beneath changes status (ADR-0026 rule 2).</summary>
    public void Deactivate() => Status = ProjectStatus.Deactivated;

    /// <summary>Back to active, exactly as it was. Restoring an active project changes nothing.</summary>
    public void Restore() => Status = ProjectStatus.Active;
}

/// <summary>What <see cref="Project.Create"/> builds: the project, its one team and its root, to be stored together (plan-backend D-13).</summary>
public sealed record NewProject(Project Project, Team Team, Area Root);
