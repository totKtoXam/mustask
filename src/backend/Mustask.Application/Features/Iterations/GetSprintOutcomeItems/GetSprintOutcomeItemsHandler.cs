using System.Globalization;
using Mustask.Application.Abstractions.Iterations;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Iterations;

namespace Mustask.Application.Features.Iterations.GetSprintOutcomeItems;

/// <summary>
/// The done items of a Closed sprint, 200 a page, by project key then number (US-5, BR-6, BR-14). Read from the close snapshot: an
/// item purged since still has its line, by the identifier the snapshot built, with no title.
/// </summary>
public sealed class GetSprintOutcomeItemsHandler(IOrganizationStructure structure, ISprintReadModel sprints)
{
    public async Task<GetSprintOutcomeItemsResponse> Handle(GetSprintOutcomeItemsQuery query, CancellationToken cancellationToken)
    {
        var organizationId = SprintAddress.Organization(query.OrganizationId);
        var teamId = await SprintAddress.TeamAsync(structure, organizationId, query.TeamId, cancellationToken);
        var sprintId = SprintAddress.Sprint(query.SprintId);

        if (await sprints.FindAsync(teamId, sprintId, cancellationToken) is not { State: nameof(SprintState.Closed) })
        {
            throw SprintAddress.SprintNotFound();
        }

        var offset = int.TryParse(query.Offset, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

        var page = await sprints.OutcomeItemsAsync(sprintId, offset, cancellationToken);

        return new GetSprintOutcomeItemsResponse(
            page.Items
                .Select(item => new OutcomeItemResponse(
                    item.Identifier, item.Title, new OutcomeProjectResponse(item.ProjectId, item.ProjectName), item.StoryPoints, item.Purged))
                .ToList(),
            page.TotalItems,
            page.TotalStoryPoints);
    }
}
