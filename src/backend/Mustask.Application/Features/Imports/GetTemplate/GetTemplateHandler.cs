using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Imports;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Shared.Exceptions;
using Mustask.Domain.Imports;

namespace Mustask.Application.Features.Imports.GetTemplate;

public sealed record GetTemplateQuery(string OrganizationId, string TemplateId);

/// <summary>One template with its settings (api-contract § 2.3): read by who may write or run (BR-IE-18, § Roles).</summary>
public sealed class GetTemplateHandler(IImportTemplateRepository templates, IUserDirectory directory, IOrganizationAccess access, ICurrentUser currentUser)
{
    public async Task<TemplateView> Handle(GetTemplateQuery query, CancellationToken cancellationToken)
    {
        var organizationId = ImportAddress.Organization(query.OrganizationId);
        var rights = await ImportRights.LoadAsync(access, currentUser.SubjectId, cancellationToken);
        rights.EnsureWriteOrRun();
        var templateId = ImportAddress.Template(query.TemplateId);

        var template = await templates.FindAsync(organizationId, templateId, cancellationToken)
            ?? throw new NotFoundException(ImportErrorCodes.TemplateNotFound);
        var names = await ImportAddress.AuthorNamesAsync(directory, [template.AuthorId], cancellationToken);

        return TemplateView.Of(template, rights, names[template.AuthorId]);
    }
}
