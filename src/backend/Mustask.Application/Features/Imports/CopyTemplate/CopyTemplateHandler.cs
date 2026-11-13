using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Imports;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.Templates;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Imports.CopyTemplate;

public sealed record CopyTemplateCommand(string OrganizationId, string TemplateId);

/// <summary>`POST /imports/templates/{id}/copy` (api-contract § 2.7): «Копия X», the caller is the author, the limit applies.</summary>
public sealed class CopyTemplateHandler(
    IImportTemplateRepository templates,
    IUserDirectory directory,
    IOrganizationAccess access,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<TemplateView> Handle(CopyTemplateCommand command, CancellationToken cancellationToken)
    {
        var organizationId = ImportAddress.Organization(command.OrganizationId);
        var rights = await ImportRights.LoadAsync(access, currentUser.SubjectId, cancellationToken);

        if (!rights.CanCopy)
        {
            throw new ForbiddenException(ImportErrorCodes.AccessForbidden);
        }

        var templateId = ImportAddress.Template(command.TemplateId);

        var copy = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await templates.LockOrganizationTemplatesAsync(organizationId, ct);
            var existing = await templates.ListAsync(organizationId, ct);
            var source = existing.FirstOrDefault(t => t.Id == templateId)
                ?? throw new NotFoundException(ImportErrorCodes.TemplateNotFound);
            ImportTemplateRules.EnsureRoom(existing);

            var keys = existing.Select(t => t.NameKey).ToHashSet(StringComparer.Ordinal);
            var name = ImportTemplate.CopyName(source.Name, candidate => keys.Contains(ImportTemplate.KeyOf(candidate)));
            var created = ImportTemplate.Create(Guid.CreateVersion7(), organizationId, name, rights.UserId, source.Settings, clock.UtcNow);
            templates.Add(created);
            await unitOfWork.SaveChangesAsync(ct);

            return created;
        }, cancellationToken);

        var names = await ImportAddress.AuthorNamesAsync(directory, [copy.AuthorId], cancellationToken);

        return TemplateView.Of(copy, rights, names[copy.AuthorId]);
    }
}
