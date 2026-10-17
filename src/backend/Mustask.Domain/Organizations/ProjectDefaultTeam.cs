namespace Mustask.Domain.Organizations;

/// <summary>The mark a team carries in the project's team list (mod-017 BR-OT-4); at most one team of a project carries one.</summary>
public enum DefaultTeamMark
{
    /// <summary>Neither named nor the only team.</summary>
    None,

    /// <summary>The project's settings name this team: «По умолчанию».</summary>
    Named,

    /// <summary>No team is named and this is the project's only active team: «По умолчанию: единственная команда».</summary>
    OnlyTeam,
}

/// <summary>
/// The one definition of the project default team (mod-017 BR-OT-4): the team the project's settings name, else the
/// project's only active team, else none. It decides a creation's starting value and nothing else. The read models, the
/// structure contract and the binders all use it, so they cannot disagree.
/// </summary>
public static class ProjectDefaultTeam
{
    /// <summary>
    /// The default team of a project whose settings name <paramref name="named"/> (or none) and whose active teams are
    /// <paramref name="activeTeamIds"/>. A named team that is not active is no longer named (the deletion clears the setting
    /// in its own operation, so this only guards a stale read).
    /// </summary>
    public static Guid? Resolve(Guid? named, IReadOnlyCollection<Guid> activeTeamIds)
    {
        ArgumentNullException.ThrowIfNull(activeTeamIds);

        if (named is { } id && activeTeamIds.Contains(id))
        {
            return id;
        }

        return activeTeamIds.Count == 1 ? activeTeamIds.Single() : null;
    }

    /// <summary>
    /// The mark of one active team. <paramref name="isNamed"/> says the settings name this team; <paramref name="activeTeams"/>
    /// is the number of the project's active teams. A named team is always active, so when some other team is named the
    /// project has at least two, and a team that is the only active team can be named only by itself or by nobody.
    /// </summary>
    public static DefaultTeamMark MarkOf(bool isNamed, int activeTeams)
    {
        if (isNamed)
        {
            return DefaultTeamMark.Named;
        }

        return activeTeams == 1 ? DefaultTeamMark.OnlyTeam : DefaultTeamMark.None;
    }
}
