using System.Globalization;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Boards;
using Mustask.Domain.Process;

namespace Mustask.Application.Features.Boards.GetViewGroups;

/// <summary>
/// The headers of an aggregate view (feat-048 BR-AV-4, BR-AV-5, BR-AV-9): the groups of the chosen grouping with the count after
/// the selectors and filters and the total without them, and for a board the count of every column. Two statements for the
/// counts (narrowed, total) and one for the names, folded here: nothing grows with the number of projects or groups.
/// </summary>
public sealed class GetViewGroupsHandler(
    IAggregateViewReadModel views,
    IProcessDefinitionReadModel processes,
    IOrganizationAccess access,
    ICurrentUser currentUser)
{
    public async Task<ViewGroupsResponse> Handle(GetViewGroupsQuery query, CancellationToken cancellationToken)
    {
        var organizationId = AggregateSupport.OrganizationOf(query.Request.OrganizationId);
        var parsed = AggregateParameters.Parse(query.Request, query.View, groupRead: false);

        var level = query.View == AggregateView.WorkItems
            ? null
            : AggregateSupport.LevelOf(await processes.FindByOrganizationAsync(organizationId, cancellationToken), query.Request.Level);

        var caller = new CallerAccess(organizationId, currentUser.SubjectId, access.Role);
        var scope = AggregateSupport.ScopeOf(query.View, level, parsed);

        var narrowed = await views.ReadCountsAsync(caller, scope, narrowed: true, cancellationToken);
        var total = await views.ReadCountsAsync(caller, scope, narrowed: false, cancellationToken);
        var directory = await views.ReadDirectoryAsync(caller, cancellationToken);

        var projectsById = directory.Projects.ToDictionary(project => project.Id);
        var teamsById = directory.Teams.ToDictionary(team => team.Id);
        var departmentsById = directory.Departments.ToDictionary(department => department.Id);

        // The group an item is in; a project's department is its node's parent, none at the top level.
        Guid? KeyOf(ViewCountRow row) => parsed.Grouping switch
        {
            AggregateGrouping.Project => row.ProjectId,
            AggregateGrouping.Team => row.TeamId,
            AggregateGrouping.Department => projectsById.TryGetValue(row.ProjectId, out var project) ? project.DepartmentId : null,
            _ => null,
        };

        var totals = total.GroupBy(KeyOf).ToDictionary(group => group.Key ?? Guid.Empty, group => group.Sum(row => row.Count));
        var kind = parsed.Grouping.ToWireName();

        var headers = new List<GroupHeaderView>();

        foreach (var group in narrowed.GroupBy(KeyOf))
        {
            var count = group.Sum(row => row.Count);

            if (count == 0)
            {
                continue;
            }

            IReadOnlyList<ColumnCountView>? columns = null;

            if (query.View == AggregateView.Boards && level is not null)
            {
                columns = level.Layout.Columns
                    .Select((column, index) => new ColumnCountView(
                        column.Key,
                        column.Header,
                        new WorkItemStateCategoryView(column.Category.ToString(), StateCategoryLabels.Of(column.Category)),
                        column.Targets.Select(target => new BoardColumnTargetView(target.TypeId, target.StateId)).ToList(),
                        group.Where(row => level.Layout.ColumnOf(row.TypeId, row.StateId) == index).Sum(row => row.Count)))
                    .ToList();
            }

            var totalCount = totals.GetValueOrDefault(group.Key ?? Guid.Empty);

            headers.Add(parsed.Grouping switch
            {
                AggregateGrouping.Project when group.Key is { } id && projectsById.TryGetValue(id, out var project) =>
                    new GroupHeaderView(kind, id, project.Key, project.Name, count, totalCount, columns),
                AggregateGrouping.Team when group.Key is { } id && teamsById.TryGetValue(id, out var team) =>
                    new GroupHeaderView(kind, id, null, team.Name, count, totalCount, columns),
                AggregateGrouping.Department when group.Key is { } id && departmentsById.TryGetValue(id, out var department) =>
                    new GroupHeaderView(kind, id, null, department.Name, count, totalCount, columns),
                _ => new GroupHeaderView(kind, null, null, null, count, totalCount, columns),
            });
        }

        // BR-AV-4: no department / no team first, then by name, ties by id (a project: name, then key, then id).
        var ordered = headers
            .OrderBy(header => header.Id is null ? 0 : 1)
            .ThenBy(header => header.Name, StringComparer.Create(CultureInfo.InvariantCulture, ignoreCase: true))
            .ThenBy(header => header.Key, StringComparer.Ordinal)
            .ThenBy(header => header.Id)
            .ToList();

        return new ViewGroupsResponse(kind, ordered);
    }
}
