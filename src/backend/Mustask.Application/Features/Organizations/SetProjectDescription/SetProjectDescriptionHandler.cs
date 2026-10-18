using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.SetProjectDescription;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/description</c> (feat-028 api-contract § 2.4; US-2 "edit a
/// project's description", BR-OL-3): a project's description is set, kept exactly as sent, or cleared. It survives a deactivation and a
/// restore; a rename changes neither the description nor the links.
/// </summary>
/// <remarks>
/// <b>The order of answers</b> (api-contract § 0.5): the address (404 for a deactivated project too, ADR-0026 rule 5); the value (400
/// <c>description</c>); the save. <b>Locks</b> (plan-backend § 7.1 "after"): the project row <c>FOR NO KEY UPDATE</c> and its status
/// read again under it — the lock a rename and a deactivation take — so a description and a deactivation serialize and the second
/// answers by the project's status. No team or link row is taken.
/// </remarks>
public sealed class SetProjectDescriptionHandler(IProjectRepository projects, IUnitOfWork unitOfWork)
{
    public async Task<SetProjectDescriptionResult> Handle(SetProjectDescriptionCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                var project = AddressedId.TryParse(command.ProjectId, out var projectId)
                    ? await projects.LockAsync(organizationId, projectId, ct)
                    : null;

                if (project is not { IsActive: true } || !await projects.IsReachableAsync(organizationId, project.Id, ct))
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                // 2. The value in the body.
                if (!EntityDescription.TryCreate(command.Description, out var description))
                {
                    throw new ValidationException(
                        OrganizationErrorCodes.ValidationFailed,
                        new Dictionary<string, IReadOnlyList<FieldError>> { ["description"] = [EntityDescription.LengthReason] });
                }

                project.Describe(description);
                await projects.SaveAsync(ct);

                return new SetProjectDescriptionResult(
                    new ProjectResponse(project.Id, project.OrganizationId, project.Name, project.Key, project.Description));
            },
            cancellationToken);
}
