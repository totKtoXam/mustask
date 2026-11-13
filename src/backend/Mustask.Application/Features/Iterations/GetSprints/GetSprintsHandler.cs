using System.Globalization;
using Mustask.Application.Abstractions.Iterations;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Boards;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Iterations.GetSprints;

/// <summary>
/// The team's sprints, highest number first, 200 a page (US-3, BR-11). Planned and Active sprints show what they hold now at every
/// level; Closed ones show the close snapshot at the level «Истории» (D-8). Three statements and the process read, whatever the
/// number of sprints.
/// </summary>
public sealed class GetSprintsHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    ISprintReadModel sprints)
{
    public async Task<SprintListPage> Handle(GetSprintsQuery query, CancellationToken cancellationToken)
    {
        var organizationId = SprintAddress.Organization(query.OrganizationId);
        var teamId = await SprintAddress.TeamAsync(structure, organizationId, query.TeamId, cancellationToken);

        int? before = int.TryParse(query.Before, NumberStyles.None, CultureInfo.InvariantCulture, out var cursor) ? cursor : null;

        // A process with no such level counts nothing for a Closed sprint rather than failing the list.
        Guid storiesLevelId;

        try
        {
            storiesLevelId = (await OutcomeScope.LevelAsync(processes, organizationId, BoardLevel.Stories.ToWireName(), cancellationToken)).Id;
        }
        catch (NotFoundException)
        {
            storiesLevelId = Guid.Empty;
        }

        return await sprints.ListAsync(teamId, before, query.Open == true, storiesLevelId, cancellationToken);
    }
}
