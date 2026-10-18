using Mustask.Domain.Organizations;

namespace Mustask.Domain.WorkItems;

/// <summary>
/// The team dimension of the shared filter specification (mod-017 BR-OT-8, api-contract § 3): one meaning on the
/// project's list, backlog and board. A value is a team's id or the literal <c>none</c> («Без команды»); an item matches
/// when it is owned by any chosen team, or by none when <c>none</c> is chosen. A filter that is <b>set</b> but names no team
/// the item can be owned by selects nothing — a malformed value, an unknown id, a deleted team's, another project's and
/// another organization's all answer alike, and none is an error. Later dimensions (feat-003, feat-013) add members here
/// and leave this one's meaning.
/// </summary>
/// <remarks>
/// feat-040 (BR-TS-8) adds the second dimension, the project: <see cref="ProjectsSet"/> and <see cref="ProjectIds"/>, set
/// by <see cref="OfProjects"/> from the repeated <c>projectId</c> values. An item matches when its project is any chosen
/// project; a filter that is set but names no project the team's items are in selects nothing, as the team dimension's
/// does. Only a team's backlog and board set it; the list and a project's views never do. The two dimensions are
/// <c>init</c> members, not constructor arguments, so a surface that sets only one reads as it did.
/// </remarks>
/// <param name="TeamsSet">Whether the request carried the filter at all; a set filter with no usable value selects nothing.</param>
/// <param name="TeamIds">The well-formed team ids among the values.</param>
/// <param name="NoTeam">Whether <c>none</c> was among the values.</param>
public sealed record WorkItemFilter(bool TeamsSet, IReadOnlyCollection<Guid> TeamIds, bool NoTeam)
{
    /// <summary>The literal that names «Без команды» in a filter.</summary>
    public const string NoTeamValue = "none";

    /// <summary>Whether the request carried the project filter at all; a set filter with no usable value selects nothing (feat-040 BR-TS-8).</summary>
    public bool ProjectsSet { get; init; }

    /// <summary>The well-formed project ids among the project values (feat-040 BR-TS-8).</summary>
    public IReadOnlyCollection<Guid> ProjectIds { get; init; } = [];

    /// <summary>No filter: the surface is as it was.</summary>
    public static WorkItemFilter None { get; } = new(false, [], false);

    /// <summary>
    /// The project filter of the repeated <c>projectId</c> values as they arrived (feat-040 api-contract § 3.1). Absent or
    /// empty is no filter; otherwise it is set, and each value is a canonical project id (either letter case, not all
    /// zeros) or ignored, so a filter whose every value is ignored is set and selects nothing.
    /// </summary>
    public static WorkItemFilter OfProjects(IEnumerable<string?>? raw)
    {
        if (raw is null)
        {
            return None;
        }

        var values = raw.ToList();

        if (values.Count == 0)
        {
            return None;
        }

        var ids = new HashSet<Guid>();

        foreach (var value in values)
        {
            if (AddressedId.TryParse(value, out var id))
            {
                ids.Add(id);
            }
        }

        return None with { ProjectsSet = true, ProjectIds = ids.ToList() };
    }

    /// <summary>
    /// The filter of the repeated <c>teamId</c> values as they arrived. Absent or empty is no filter; otherwise it is set,
    /// and each value is a canonical team id, <c>none</c>, or ignored.
    /// </summary>
    public static WorkItemFilter OfTeams(IEnumerable<string?>? raw)
    {
        if (raw is null)
        {
            return None;
        }

        var values = raw.ToList();

        if (values.Count == 0)
        {
            return None;
        }

        var ids = new HashSet<Guid>();
        var noTeam = false;

        foreach (var value in values)
        {
            if (string.Equals(value, NoTeamValue, StringComparison.Ordinal))
            {
                noTeam = true;
            }
            else if (AddressedId.TryParse(value, out var id))
            {
                ids.Add(id);
            }
        }

        return new WorkItemFilter(true, ids.ToList(), noTeam);
    }
}
