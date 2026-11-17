using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Imports;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.Templates;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Imports.GetTemplateColumns;

public sealed record GetTemplateColumnsQuery(string OrganizationId, string TemplateId);

/// <summary>
/// A template's name and its export header texts, for every member (api-contract § 2.4; BR-IE-19): the column-sourced mappings in mapping
/// order, one header once.
/// </summary>
public sealed class GetTemplateColumnsHandler(IImportTemplateRepository templates, IOrganizationAccess access)
{
    public async Task<TemplateColumnsView> Handle(GetTemplateColumnsQuery query, CancellationToken cancellationToken)
    {
        var organizationId = ImportAddress.Organization(query.OrganizationId);
        var templateId = ImportAddress.Template(query.TemplateId);

        if (!access.Holds(Domain.Identity.PermissionCatalog.ImportsTemplateRead))
        {
            throw new ForbiddenException(ImportErrorCodes.AccessForbidden);
        }

        var template = await templates.FindAsync(organizationId, templateId, cancellationToken)
            ?? throw new NotFoundException(ImportErrorCodes.TemplateNotFound);

        var columns = template.Settings.Mappings
            .Where(m => m.Source.Kind == TemplateTokens.SourceColumn && !string.IsNullOrWhiteSpace(m.Source.Column))
            .Select(m => m.Source.Column!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new TemplateColumnsView(template.Id, template.Name, columns);
    }
}
