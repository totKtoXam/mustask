using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Organizations;

/// <summary>
/// A team of a project (US-4, US-7). Its name is unique among the project's
/// active teams and freed when it is deleted (BR-M1-1, ADR-0026 rule 6). No team
/// is the first and a team has no area (mod-017 BR-OT-10, BR-OT-11): it is a name,
/// a status and the people in it, and what it owns is the items whose owning team
/// it is. Any team may be renamed or deleted, except a project's last.
/// </summary>
public sealed class Team
{
    private Team()
    {
        // EF materialization.
    }

    private Team(Guid id, Guid projectId, EntityName name)
    {
        Id = id;
        ProjectId = projectId;
        Name = name.Value;
        NormalizedName = name.Normalized;
        Status = TeamStatus.Active;
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Name { get; private set; } = null!;

    public string NormalizedName { get; private set; } = null!;

    public TeamStatus Status { get; private set; }

    public bool IsActive => Status == TeamStatus.Active;

    /// <summary>
    /// mod-017 (BR-OT-11): an ordinary team, with no area and no special role — neither first nor owner of anything. The team a
    /// project is created with, and every team added later, are made so.
    /// </summary>
    public static Team Create(Guid projectId, EntityName name) => new(Guid.CreateVersion7(), projectId, name);

    /// <summary>Renames the team. What it owns and its people do not change (US-4).</summary>
    public void Rename(EntityName name)
    {
        Name = name.Value;
        NormalizedName = name.Normalized;
    }

    /// <summary>
    /// Deletes the team for good (mod-017 BR-OT-11, BR-OT-12): the status is terminal and the name is freed. The caller deletes
    /// in one operation over what the team owns — the items it owns go to «Без команды», its places and a default setting that
    /// named it go, its memberships end — and has locked all the project's active teams, so <paramref name="activeTeamsOfProject"/>
    /// is the count at the moment of the deletion. A project's last active team is refused (the interim guard, lifted by
    /// feat-028), because the project's views are reached through a team. No team is the first (BR-OT-11): any other may go.
    /// </summary>
    public void Delete(int activeTeamsOfProject)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("A deleted team answers not found before it reaches the domain.");
        }

        if (activeTeamsOfProject <= 1)
        {
            throw new ConflictException(OrganizationErrorCodes.TeamLastTeamCannotBeDeleted);
        }

        Status = TeamStatus.Deleted;
    }
}
