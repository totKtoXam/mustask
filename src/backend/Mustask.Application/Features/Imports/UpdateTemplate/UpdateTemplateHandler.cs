using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Imports;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Imports;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Imports.UpdateTemplate;

public sealed record UpdateTemplateCommand(string OrganizationId, string TemplateId, TemplateBody Body);

/// <summary>`PUT /imports/templates/{id}` (api-contract § 2.6): last save wins, the version goes up; who may change it is `canEdit`.</summary>
public sealed class UpdateTemplateHandler(
    IImportTemplateRepository templates,
    IImportReferenceReader references,
    IUserDirectory directory,
    IOrganizationAccess access,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<TemplateView> Handle(UpdateTemplateCommand command, CancellationToken cancellationToken)
    {
        var organizationId = ImportAddress.Organization(command.OrganizationId);
        var rights = await ImportRights.LoadAsync(access, currentUser.SubjectId, cancellationToken);
        rights.EnsureCreate();
        var templateId = ImportAddress.Template(command.TemplateId);

        var template = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await templates.LockOrganizationTemplatesAsync(organizationId, ct);
            var found = await templates.FindForUpdateAsync(organizationId, templateId, ct)
                ?? throw new NotFoundException(ImportErrorCodes.TemplateNotFound);

            if (!rights.CanChange(found))
            {
                throw new ForbiddenException(ImportErrorCodes.AccessForbidden);
            }

            var existing = await templates.ListAsync(organizationId, ct);
            await ImportTemplateRules.EnsureSavableAsync(command.Body, existing, found.Id, organizationId, references, ct);

            found.Update(command.Body.Name!, command.Body.Settings!, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(ct);

            return found;
        }, cancellationToken);

        var names = await ImportAddress.AuthorNamesAsync(directory, [template.AuthorId], cancellationToken);

        return TemplateView.Of(template, rights, names[template.AuthorId]);
    }
}
