using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Imports;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Imports.Templates;

namespace Mustask.Application.Features.Imports.CreateTemplate;

public sealed record CreateTemplateCommand(string OrganizationId, TemplateBody Body);

/// <summary>`POST /imports/templates` (api-contract § 2.5): the author is the caller; the lock makes the limit and the name race-free.</summary>
public sealed class CreateTemplateHandler(
    IImportTemplateRepository templates,
    IImportReferenceReader references,
    IUserDirectory directory,
    IOrganizationAccess access,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<TemplateView> Handle(CreateTemplateCommand command, CancellationToken cancellationToken)
    {
        var organizationId = ImportAddress.Organization(command.OrganizationId);
        var rights = await ImportRights.LoadAsync(access, currentUser.SubjectId, cancellationToken);
        rights.EnsureCreate();

        var template = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await templates.LockOrganizationTemplatesAsync(organizationId, ct);
            var existing = await templates.ListAsync(organizationId, ct);
            ImportTemplateRules.EnsureRoom(existing);
            await ImportTemplateRules.EnsureSavableAsync(command.Body, existing, null, organizationId, references, ct);

            var created = ImportTemplate.Create(
                Guid.CreateVersion7(), organizationId, command.Body.Name!, rights.UserId, command.Body.Settings!, clock.UtcNow);
            templates.Add(created);
            await unitOfWork.SaveChangesAsync(ct);

            return created;
        }, cancellationToken);

        var names = await ImportAddress.AuthorNamesAsync(directory, [template.AuthorId], cancellationToken);

        return TemplateView.Of(template, rights, names[template.AuthorId]);
    }
}
