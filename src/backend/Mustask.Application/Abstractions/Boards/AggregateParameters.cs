using System.Globalization;
using Mustask.Domain.Boards;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Abstractions.Boards;

/// <summary>
/// The query string of an aggregate read as it arrived (feat-048 api-contract § 1): every value the untrusted string of the
/// address. The selectors are the repeated <c>departmentId</c>, <c>projectId</c> and <c>teamId</c> (an id, or <c>none</c> for the
/// team), the filters feat-003's repeated <c>state</c> and <c>assigneeId</c> and its <c>search</c>.
/// </summary>
public sealed record AggregateQuery(
    string? OrganizationId,
    string? Level,
    string? Grouping = null,
    IReadOnlyList<string?>? DepartmentIds = null,
    IReadOnlyList<string?>? ProjectIds = null,
    IReadOnlyList<string?>? TeamIds = null,
    IReadOnlyList<string?>? States = null,
    IReadOnlyList<string?>? AssigneeIds = null,
    IReadOnlyList<string?>? Search = null,
    string? Group = null,
    string? Column = null,
    string? Limit = null,
    string? Cursor = null);

/// <summary>The validated parts of an <see cref="AggregateQuery"/>.</summary>
public sealed record ParsedAggregate(
    AggregateGrouping Grouping,
    WorkItemFilter Filter,
    bool DepartmentsSet,
    IReadOnlyCollection<Guid> DepartmentIds,
    GroupSelector Group,
    int Limit,
    CursorPosition? After,
    string Fingerprint);

/// <summary>
/// Validation of the parameters the five aggregate reads share, in one place (the slices do not reference each other). A bad value
/// is the existing validation 400 (<see cref="BoardErrorCodes.ValidationFailed"/>) naming the field and a reason; a selector or
/// filter value that names nothing selects nothing and is never an error, as on the project's list.
/// </summary>
public static class AggregateParameters
{
    public const int MaxLimit = 50;

    public const int DefaultLimit = 50;

    /// <param name="view">The view read; the backlog's and board's by-project group pages by rank, everything else by change time.</param>
    /// <param name="groupRead">Whether this read is of one group: it validates <c>group</c>, <c>limit</c> and <c>cursor</c>.</param>
    public static ParsedAggregate Parse(AggregateQuery query, AggregateView view, bool groupRead)
    {
        ArgumentNullException.ThrowIfNull(query);

        var grouping = AggregateGrouping.None;

        if (!string.IsNullOrEmpty(query.Grouping) && !AggregateGroupings.TryParse(query.Grouping, out grouping))
        {
            throw Invalid("grouping", BoardErrorCodes.InvalidGrouping);
        }

        var departments = (query.DepartmentIds ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        var teams = (query.TeamIds ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        var projects = (query.ProjectIds ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).ToList();

        if (!AggregateGroupings.Offers(projects.Count > 0, teams.Count > 0, departments.Count > 0, grouping))
        {
            throw Invalid("grouping", BoardErrorCodes.InvalidGrouping);
        }

        var departmentIds = departments
            .Select(value => AddressedId.TryParse(value, out var id) ? id : (Guid?)null)
            .OfType<Guid>()
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        var filter = WorkItemFilter.OfOrganization(projects, teams, query.States, query.AssigneeIds, query.Search);

        var group = new GroupSelector(grouping, null);
        var limit = DefaultLimit;
        CursorPosition? after = null;

        var fingerprint = Fingerprint(query, view, grouping, departmentIds, filter);

        if (groupRead)
        {
            if (grouping != AggregateGrouping.None)
            {
                group = new GroupSelector(grouping, GroupId(query.Group));
            }

            if (query.Limit is { Length: > 0 } text
                && (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit is < 1 or > MaxLimit))
            {
                throw Invalid("limit", BoardErrorCodes.InvalidParameter);
            }

            if (query.Limit is not { Length: > 0 })
            {
                limit = DefaultLimit;
            }

            fingerprint += "|g=" + (grouping == AggregateGrouping.None ? string.Empty : query.Group?.Trim().ToLowerInvariant());

            if (!string.IsNullOrEmpty(query.Cursor))
            {
                var byRank = view != AggregateView.WorkItems && grouping == AggregateGrouping.Project;

                if (!GroupCursor.TryDecode(query.Cursor, fingerprint, byRank, out var position))
                {
                    throw Invalid("cursor", BoardErrorCodes.InvalidCursor);
                }

                after = position;
            }
        }

        return new ParsedAggregate(grouping, filter, departments.Count > 0, departmentIds, group, limit, after, fingerprint);
    }

    /// <summary>The fingerprint of a request's cursor: everything that decides which rows a group holds, apart from the page.</summary>
    private static string Fingerprint(AggregateQuery query, AggregateView view, AggregateGrouping grouping, IReadOnlyCollection<Guid> departments, WorkItemFilter filter)
    {
        static string Sorted(IEnumerable<string?>? values) =>
            string.Join(',', (values ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim().ToLowerInvariant()).Distinct().Order(StringComparer.Ordinal));

        return string.Join(
            '|',
            view,
            query.OrganizationId?.Trim().ToLowerInvariant(),
            query.Level,
            grouping,
            "d=" + string.Join(',', departments),
            "p=" + Sorted(query.ProjectIds),
            "t=" + Sorted(query.TeamIds),
            "s=" + string.Join(',', filter.StateNames.Order(StringComparer.Ordinal)),
            "a=" + string.Join(',', filter.AssigneeIds.Order(StringComparer.Ordinal)) + (filter.NoAssignee ? ",none" : string.Empty),
            "q=" + filter.Keyword?.Text,
            "c=" + query.Column);
    }

    /// <summary>The group's id, or <c>null</c> for «Без департамента» / «Без команды» (<c>none</c>); anything else is a 400.</summary>
    private static Guid? GroupId(string? value)
    {
        if (string.Equals(value, "none", StringComparison.Ordinal))
        {
            return null;
        }

        return AddressedId.TryParse(value, out var id) ? id : throw Invalid("group", BoardErrorCodes.InvalidParameter);
    }

    private static ValidationException Invalid(string field, string reason) =>
        new(BoardErrorCodes.ValidationFailed, new Dictionary<string, IReadOnlyList<FieldError>> { [field] = [new FieldError(reason)] });
}
