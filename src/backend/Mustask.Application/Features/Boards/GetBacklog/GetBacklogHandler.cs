using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Boards;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Boards.GetBacklog;

/// <summary>
/// A team's backlog of one level (US-1): the items § Model's conditions 1–4
/// select at this moment (BR-M4-5), placed ones by their places, then the
/// unplaced ones by number (BR-M4-24), numbered «Порядок» 1..n.
/// </summary>
/// <remarks>
/// Not found from the outside in (api-contract § 0.3): the organization (the
/// filter has answered), the project — malformed, unknown, of another
/// organization or not reachable — the team — malformed, unknown, of another
/// project or deleted (BR-M4-20) — then the level, by the organization's
/// process. Reads only, no transaction.
/// </remarks>
public sealed class GetBacklogHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    IBacklogReadModel backlogs)
{
    public async Task<BacklogResponse> Handle(GetBacklogQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.ProjectId, out var projectId)
            || await structure.FindProjectAsync(projectId, cancellationToken) is not { IsReachable: true } project
            || project.OrganizationId != organizationId)
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        if (!AddressedId.TryParse(query.TeamId, out var teamId)
            || await structure.FindTeamAsync(teamId, cancellationToken) is not { IsReachable: true } team
            || team.ProjectId != projectId)
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        if (!BoardLevelNames.TryParse(query.Level, out var level)
            || await processes.FindByOrganizationAsync(organizationId, cancellationToken) is not { } process
            || level.Resolve(process.BacklogLevels.Select(row => new ProcessLevel(row.Id, row.Rank, row.Kind))) is not { } resolved)
        {
            throw new NotFoundException(BoardErrorCodes.LevelNotFound);
        }

        var processLevel = process.BacklogLevels.Single(row => row.Id == resolved.Id);
        var types = process.WorkItemTypes.Where(type => type.BacklogLevelId == resolved.Id).ToList();

        var read = await backlogs.ReadAsync(BacklogScope.Of(projectId, teamId, resolved.Id, types), cancellationToken);

        return new BacklogResponse(
            new BoardTeamView(team.Id, team.Name, read.DefaultArea),
            BoardLevelView.Of(level, processLevel, types),
            BacklogItemView.InOrder(read.Items));
    }
}
