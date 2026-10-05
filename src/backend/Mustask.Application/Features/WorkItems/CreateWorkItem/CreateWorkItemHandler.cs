using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.WorkItems.CreateWorkItem;

/// <summary>
/// The create dialog's submission (US-1): a new item of the chosen type with
/// only a title, in its type's initial state, unassigned, with an empty
/// description, in the project's root area (BR-M3-20), numbered from the
/// project's one sequence (BR-M3-5).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.4): the address from the
/// outside in (the organization-context filter has answered for the
/// organization; the project must be the organization's and reachable); the
/// values, with every field reason at once; the type; then the creation.
/// </para>
/// <para>
/// <b>The number and the replay</b> (BR-M3-5, BR-M3-26). The project's counter
/// row is locked <c>FOR UPDATE</c> first, so the creations of a project run
/// one after another from there to their commit, and each takes one more than
/// the last number issued. The replay check comes after that lock, as a new
/// statement under READ COMMITTED: a submission that arrives twice at the same
/// moment waits for the first to commit, then finds its item and answers it,
/// with no number consumed. The unique index on the submission key is the
/// backstop. The root area needs no lock: it is never deleted (BR-M1-11).
/// </para>
/// </remarks>
public sealed class CreateWorkItemHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    IWorkItemRepository workItems,
    IWorkItemReadModel readModel,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<CreateWorkItemResult> Handle(CreateWorkItemCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                if (!AddressedId.TryParse(command.ProjectId, out var projectId)
                    || await structure.FindProjectAsync(projectId, ct) is not { IsReachable: true } project
                    || project.OrganizationId != organizationId)
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                // 2. The values, every field reason at once.
                var errors = new Dictionary<string, IReadOnlyList<FieldError>>(StringComparer.Ordinal);

                if (!AddressedId.TryParse(command.RequestId, out var requestId))
                {
                    errors["requestId"] = [new FieldError(WorkItemErrorCodes.RequestIdInvalid)];
                }

                if (WorkItemText.ValidateTitle(command.Title, out var title) is { } titleReason)
                {
                    errors["title"] = [new FieldError(titleReason)];
                }

                if (errors.Count > 0)
                {
                    throw new ValidationException(WorkItemErrorCodes.ValidationFailed, errors);
                }

                // 3. The type: one of the organization's process (BR-M3-1).
                var type = await FindTypeAsync(organizationId, command.TypeId, ct)
                    ?? throw new NotFoundException(WorkItemErrorCodes.TypeNotFound);

                // 4. The number, and the replay after its lock.
                await workItems.LockNumberSequenceAsync(projectId, ct);

                if (await workItems.FindCreatedByRequestAsync(projectId, requestId, ct) is { } existingId)
                {
                    return new CreateWorkItemResult(await readModel.GetAsync(existingId, ct), Created: false);
                }

                var number = await workItems.TakeNextNumberAsync(projectId, ct);

                var item = WorkItem.Create(projectId, number, type, project.RootAreaId, title, requestId, clock.UtcNow, currentUser.SubjectId);
                workItems.Add(item);
                await workItems.SaveAsync(ct);

                return new CreateWorkItemResult(await readModel.GetAsync(item.Id, ct), Created: true);
            },
            cancellationToken);

    /// <summary>The type of the organization's process with that id, or <c>null</c> for a malformed, unknown or foreign one.</summary>
    private async Task<WorkItemTypeStates?> FindTypeAsync(Guid organizationId, string? rawTypeId, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(rawTypeId, out var typeId))
        {
            return null;
        }

        var process = await processes.FindByOrganizationAsync(organizationId, cancellationToken);
        var type = process?.WorkItemTypes.SingleOrDefault(candidate => candidate.Id == typeId);

        return type is null
            ? null
            : new WorkItemTypeStates(
                type.Id,
                type.Key,
                type.States.Select(state => new WorkItemTypeState(state.Id, state.Position, state.Category)).ToList());
    }
}
