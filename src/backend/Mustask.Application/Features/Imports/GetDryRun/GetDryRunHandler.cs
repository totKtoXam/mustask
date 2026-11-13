using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Imports;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.DryRun;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Imports.GetDryRun;

public sealed record GetDryRunQuery(string OrganizationId, string DryRunId, string? Result);

/// <summary>`GET /imports/dry-runs/{id}` (api-contract § 4.2): stale when the template was saved after the run, or deleted.</summary>
public sealed class GetDryRunHandler(
    IImportDryRunRepository dryRuns,
    IImportTemplateRepository templates,
    IReasonText texts,
    IOrganizationAccess access,
    ICurrentUser currentUser,
    IClock clock)
{
    public async Task<DryRunView> Handle(GetDryRunQuery query, CancellationToken cancellationToken)
    {
        var organizationId = ImportAddress.Organization(query.OrganizationId);
        var rights = await ImportRights.LoadAsync(access, currentUser.SubjectId, cancellationToken);
        rights.EnsureRun();
        var runId = ImportAddress.DryRun(query.DryRunId);

        var result = string.IsNullOrEmpty(query.Result) ? RowResults.All : query.Result;

        if (!RowResults.Filters.Contains(result))
        {
            throw new ValidationException(
                ImportErrorCodes.ValidationFailed,
                new Dictionary<string, IReadOnlyList<FieldError>> { ["result"] = [new FieldError(ImportErrorCodes.ValidationFailed)] });
        }

        var run = await dryRuns.FindAsync(organizationId, rights.UserId, runId, clock.UtcNow, cancellationToken)
            ?? throw new NotFoundException(ImportErrorCodes.DryRunNotFound);
        var template = await templates.FindAsync(organizationId, run.TemplateId, cancellationToken);
        var stale = template is null || template.Version != run.TemplateVersion;

        return DryRunView.Of(run, result, stale, texts);
    }
}
