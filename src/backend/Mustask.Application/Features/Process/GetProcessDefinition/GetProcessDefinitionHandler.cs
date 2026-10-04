using Mustask.Application.Abstractions.Process;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Process.GetProcessDefinition;

/// <summary>
/// US-2: reads an organization's process definition. A malformed identifier and
/// an unknown one get the same answer, <see cref="OrganizationErrorCodes.OrganizationNotFound"/>
/// (BR-M2-11), and nothing tells the two apart. The handler catches nothing, so a
/// failed read stays a 500 and never becomes a 404.
/// </summary>
public sealed class GetProcessDefinitionHandler(IProcessDefinitionReadModel readModel)
{
    public async Task<ProcessDefinitionResponse> Handle(GetProcessDefinitionQuery query, CancellationToken cancellationToken)
    {
        // 1. A value that is not a well-formed identifier never reaches the storage.
        if (!OrganizationKey.TryParse(query.OrganizationId, out var organizationId))
        {
            throw NotFound();
        }

        // 2. Unknown identifier: the same answer.
        var row = await readModel.FindByOrganizationAsync(organizationId, cancellationToken) ?? throw NotFound();

        // 3. Otherwise the rows map to the response; the words come from the domain.
        return Map(row);
    }

    private static NotFoundException NotFound() => new(OrganizationErrorCodes.OrganizationNotFound);

    private static ProcessDefinitionResponse Map(ProcessDefinitionRow row) => new(
        row.Id.Value,
        row.BacklogLevels
            .Select(level => new BacklogLevelResponse(level.Id, level.Name, level.Rank, level.Kind.ToWireName()))
            .ToList(),
        row.WorkItemTypes
            .Select(type => new WorkItemTypeResponse(
                type.Id,
                type.Key,
                type.Name,
                type.Appearance.ToWireName(),
                new BacklogLevelReferenceResponse(type.BacklogLevelId, type.BacklogLevelName),
                type.States
                    .Select(state => new StateResponse(
                        state.Id,
                        state.Name,
                        state.Position,
                        state.Position == StateDefinition.InitialPosition,
                        new StateCategoryResponse(state.Category.ToString(), StateCategoryLabels.Of(state.Category))))
                    .ToList()))
            .ToList());
}
