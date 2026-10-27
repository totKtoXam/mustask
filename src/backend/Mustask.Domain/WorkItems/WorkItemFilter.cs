using Mustask.Domain.Organizations;
using Mustask.Domain.Process;

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
/// <para>
/// feat-040 (BR-TS-8) adds the second dimension, the project: <see cref="ProjectsSet"/> and <see cref="ProjectIds"/>, set
/// by <see cref="OfProjects"/> from the repeated <c>projectId</c> values. An item matches when its project is any chosen
/// project; a filter that is set but names no project the team's items are in selects nothing, as the team dimension's
/// does. Only a team's backlog and board set it; the list and a project's views never do. The two dimensions are
/// <c>init</c> members, not constructor arguments, so a surface that sets only one reads as it did.
/// </para>
/// <para>
/// feat-003 (BR-WS-16) adds three dimensions: the state (<see cref="StatesSet"/>, <see cref="StateNames"/>), the assignee
/// (<see cref="AssigneesSet"/>, <see cref="AssigneeIds"/>, <see cref="NoAssignee"/>) and the keyword (<see cref="Keyword"/>),
/// built by <see cref="Of"/> from the request's repeated <c>state</c> and <c>assigneeId</c> and its <c>search</c>. They are
/// <c>init</c> members, not constructor arguments, so a surface that sets only the team reads as it did. A dimension whose values
/// are all blank is not set; one that is set but names nothing the item can have selects nothing, alike for every kind of value
/// and never an error (api-contract § 2.1). Only the project's list takes them in feat-003; it never sets the project dimension.
/// </para>
/// </remarks>
/// <param name="TeamsSet">Whether the request carried the filter at all; a set filter with no usable value selects nothing.</param>
/// <param name="TeamIds">The well-formed team ids among the values.</param>
/// <param name="NoTeam">Whether <c>none</c> was among the values.</param>
public sealed record WorkItemFilter(bool TeamsSet, IReadOnlyCollection<Guid> TeamIds, bool NoTeam)
{
    /// <summary>The literal that names «Без команды» in a filter.</summary>
    public const string NoTeamValue = "none";

    /// <summary>The literal that names «Не назначен» in a filter (feat-003 api-contract § 2.1).</summary>
    public const string NoAssigneeValue = "none";

    /// <summary>Whether the request carried a non-blank <c>state</c> value; a set filter with no usable name selects nothing.</summary>
    public bool StatesSet { get; init; }

    /// <summary>The state names among the values, as stored (ordinal, letter case counts), each once; a name no state can have is left out.</summary>
    public IReadOnlyCollection<string> StateNames { get; init; } = [];

    /// <summary>Whether the request carried a non-blank <c>assigneeId</c> value; a set filter with no usable value selects nothing.</summary>
    public bool AssigneesSet { get; init; }

    /// <summary>The user ids among the values, as stored, each once; a value no user id can be is left out.</summary>
    public IReadOnlyCollection<string> AssigneeIds { get; init; } = [];

    /// <summary>Whether <c>none</c> was among the assignee values.</summary>
    public bool NoAssignee { get; init; }

    /// <summary>The keyword, or <c>null</c> when the request carried none (a blank one sets no filter).</summary>
    public WorkItemKeyword? Keyword { get; init; }

    /// <summary>Whether the request carried the project filter at all; a set filter with no usable value selects nothing (feat-040 BR-TS-8).</summary>
    public bool ProjectsSet { get; init; }

    /// <summary>The well-formed project ids among the project values (feat-040 BR-TS-8).</summary>
    public IReadOnlyCollection<Guid> ProjectIds { get; init; } = [];

    /// <summary>No filter: the surface is as it was.</summary>
    public static WorkItemFilter None { get; } = new(false, [], false);

    /// <summary>
    /// The list's filter (feat-003): the team dimension as <see cref="OfTeams"/> reads it, and the state, the assignee and the
    /// keyword from the values as they arrived. <paramref name="projectKey"/> is the addressed project's, which decides whether
    /// a keyword is an identifier of its items.
    /// </summary>
    public static WorkItemFilter Of(
        string projectKey,
        IEnumerable<string?>? teamIds,
        IEnumerable<string?>? states,
        IEnumerable<string?>? assigneeIds,
        IEnumerable<string?>? search)
    {
        var keyword = WorkItemKeyword.Of(search, projectKey);
        var chosenStates = UsableValues(states, StateDefinition.NameMaxLength);
        var chosenAssignees = UsableValues(assigneeIds, WorkItemLimits.UserIdMaxLength);

        if (keyword is null && chosenStates is null && chosenAssignees is null)
        {
            return OfTeams(teamIds);
        }

        var noAssignee = chosenAssignees?.Contains(NoAssigneeValue, StringComparer.Ordinal) == true;

        return OfTeams(teamIds) with
        {
            StatesSet = chosenStates is not null,
            StateNames = chosenStates?.Distinct(StringComparer.Ordinal).ToList() ?? [],
            AssigneesSet = chosenAssignees is not null,
            AssigneeIds = chosenAssignees?.Where(id => !string.Equals(id, NoAssigneeValue, StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).ToList() ?? [],
            NoAssignee = noAssignee,
            Keyword = keyword,
        };
    }

    /// <summary>
    /// The state filter alone (the «Исполнитель» options take the chosen states): <see cref="StatesSet"/> and
    /// <see cref="StateNames"/> of the repeated <c>state</c> values as they arrived.
    /// </summary>
    public static WorkItemFilter OfStates(IEnumerable<string?>? raw)
    {
        var chosen = UsableValues(raw, StateDefinition.NameMaxLength);

        return chosen is null ? None : None with { StatesSet = true, StateNames = chosen.Distinct(StringComparer.Ordinal).ToList() };
    }

    /// <summary>
    /// The non-blank values, <c>null</c> when there is none (the dimension is not set). A set dimension keeps only the values
    /// that can name something: one holding U+0000 (PostgreSQL text cannot) or longer than the column it is compared with
    /// names nothing and is left out, so it is never an error.
    /// </summary>
    private static List<string>? UsableValues(IEnumerable<string?>? raw, int maxLength)
    {
        var values = raw?.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).ToList();

        if (values is null || values.Count == 0)
        {
            return null;
        }

        return values.Where(value => value.Length <= maxLength && !value.Contains('\0', StringComparison.Ordinal)).ToList();
    }

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
