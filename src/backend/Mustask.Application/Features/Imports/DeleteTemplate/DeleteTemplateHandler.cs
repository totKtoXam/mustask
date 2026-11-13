using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Imports;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Imports;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Imports.DeleteTemplate;

public sealed record DeleteTemplateCommand(string OrganizationId, string TemplateId);

/// <summary>`DELETE /imports/templates/{id}` (api-contract § 2.8): a dry run that names it turns stale.</summary>
public sealed class DeleteTemplateHandler(
    IImportTemplateRepository templates, IOrganizationAccess access, ICurrentUser currentUser, IUnitOfWork unitOfWork)
{
    public async Task Handle(DeleteTemplateCommand command, CancellationToken cancellationToken)
    {
        var organizationId = ImportAddress.Organization(command.OrganizationId);
        var templateId = ImportAddress.Template(command.TemplateId);
        var rights = await ImportRights.LoadAsync(access, currentUser.SubjectId, cancellationToken);

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var found = await templates.FindForUpdateAsync(organizationId, templateId, ct)
                ?? throw new NotFoundException(ImportErrorCodes.TemplateNotFound);

            if (!rights.CanChange(found))
            {
                throw new ForbiddenException(ImportErrorCodes.AccessForbidden);
            }

            templates.Remove(found);
            await unitOfWork.SaveChangesAsync(ct);

            return true;
        }, cancellationToken);
    }
}
