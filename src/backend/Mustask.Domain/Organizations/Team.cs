using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Organizations;

/// <summary>
/// A team of a project (US-4, US-7). Its name is unique among the project's
/// active teams and freed when it is deleted (BR-M1-1, ADR-0026 rule 6). The
/// project's first team owns the root, cannot be deleted, and keeps the root as
/// its default area for good (BR-M1-9, BR-M1-18).
/// </summary>
public sealed class Team
{
    private Team()
    {
        // EF materialization.
    }

    private Team(Guid id, Guid projectId, EntityName name, bool isFirstTeam)
    {
        Id = id;
        ProjectId = projectId;
        Name = name.Value;
        NormalizedName = name.Normalized;
        Status = TeamStatus.Active;
        IsFirstTeam = isFirstTeam;
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Name { get; private set; } = null!;

    public string NormalizedName { get; private set; } = null!;

    public TeamStatus Status { get; private set; }

    /// <summary>The team that owns the project's root (BR-M1-9). Exactly one per project.</summary>
    public bool IsFirstTeam { get; private set; }

    public bool IsActive => Status == TeamStatus.Active;

    /// <summary>
    /// A second or later team, with its own area: a child of the root named after
    /// the team, owned by it explicitly and set as its default (US-4, BR-M1-9).
    /// </summary>
    public static NewTeam CreateWithArea(Guid projectId, EntityName name, Area root)
    {
        if (!root.IsRoot || root.ProjectId != projectId)
        {
            throw new ArgumentException("A new team's area is a child of its own project's root.", nameof(root));
        }

        var team = new Team(Guid.CreateVersion7(), projectId, name, isFirstTeam: false);
        var area = Area.CreateDefaultChild(root, name, team.Id);

        return new NewTeam(team, area);
    }

    /// <summary>
    /// mod-017 (BR-OT-11): an ordinary team, with no area and no special role — neither first nor owner of anything. The
    /// team a project is created with, and every team added later, are made so.
    /// </summary>
    public static Team Create(Guid projectId, EntityName name) =>
        new(Guid.CreateVersion7(), projectId, name, isFirstTeam: false);

    internal static Team CreateFirst(Guid projectId, EntityName name) =>
        new(Guid.CreateVersion7(), projectId, name, isFirstTeam: true);

    /// <summary>Renames the team. Its areas keep their names (US-4).</summary>
    public void Rename(EntityName name)
    {
        Name = name.Value;
        NormalizedName = name.Normalized;
    }

    /// <summary>
    /// Deletes the team for good (US-7): the status is terminal and the name is
    /// freed. The caller re-owns the team's explicit areas to the first team in
    /// the same transaction (BR-M1-18). The first team is refused.
    /// </summary>
    public void Delete()
    {
        if (IsFirstTeam)
        {
            throw new ConflictException(OrganizationErrorCodes.TeamFirstTeamCannotBeDeleted);
        }

        if (!IsActive)
        {
            throw new InvalidOperationException("A deleted team answers not found before it reaches the domain.");
        }

        Status = TeamStatus.Deleted;
    }
}

/// <summary>What <see cref="Team.CreateWithArea"/> builds: the team and its default area, to be stored together.</summary>
public sealed record NewTeam(Team Team, Area Area);
