using Microsoft.Extensions.Logging;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.SetProjectVisibility;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/visibility</c> (feat-041, doc-016 BR-TD-12 (c)): turns the
/// project's «Видим всей организации» on or off under <c>organizations.visibility.write</c> on the project, which the route's scoped
/// check has judged (the sight gate before it: a project the caller does not see is «Проект не найден»). The write changes sight only;
/// it adds no code (BR-TD-16) and is seen on the next request of everyone (nothing is cached).
/// </summary>
/// <remarks>
/// <b>Order of answers:</b> the address (404 for a deactivated project too); the value (400); the save. <b>Lock:</b> the project row
/// <c>FOR NO KEY UPDATE</c>, the lock a rename and a deactivation take, so a flag write and a deactivation serialize. Setting the value
/// it already holds is a success that changes nothing.
/// </remarks>
public sealed class SetProjectVisibilityHandler(
    IProjectRepository projects, IUnitOfWork unitOfWork, ICurrentUser currentUser, ILogger<SetProjectVisibilityHandler> logger)
{
    public async Task<SetProjectVisibilityResponse> Handle(SetProjectVisibilityCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
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

                if (command.VisibleToOrganization is not { } visible)
                {
                    throw new ValidationException(
                        OrganizationErrorCodes.ValidationFailed,
                        new Dictionary<string, IReadOnlyList<FieldError>> { ["visibleToOrganization"] = [new FieldError(OrganizationErrorCodes.ValidationFailed)] });
                }

                if (project.SetVisibleToOrganization(visible))
                {
                    await projects.SaveAsync(ct);
                    logger.LogInformation(
                        "Project visibility set: project {ProjectId} in organization {OrganizationId} visible to the organization {Visible}, by {ActorId}.",
                        project.Id, organizationId, visible, currentUser.SubjectId);
                }

                return new SetProjectVisibilityResponse(project.Id, project.OrganizationId, project.VisibleToOrganization);
            },
            cancellationToken);
}
