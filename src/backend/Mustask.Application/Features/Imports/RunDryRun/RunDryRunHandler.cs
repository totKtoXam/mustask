using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Imports;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.DryRun;
using Mustask.Domain.Imports.Source;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Imports.RunDryRun;

public sealed record RunDryRunCommand(string OrganizationId, string TemplateId, Guid? FileId);

/// <summary>
/// `POST /imports/templates/{id}/dry-runs` (api-contract § 4.1). Reads the stored file by the template's own source settings, refuses the
/// whole file before any row when a mapped column is missing, runs every row against what the caller may import into, and stores the report.
/// Nothing in the work items changes.
/// </summary>
public sealed class RunDryRunHandler(
    IImportTemplateRepository templates,
    IImportFileRepository files,
    IImportDryRunRepository dryRuns,
    ISourceFileReader reader,
    IImportReferenceReader references,
    IReasonText texts,
    IOrganizationAccess access,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<DryRunView> Handle(RunDryRunCommand command, CancellationToken cancellationToken)
    {
        var organizationId = ImportAddress.Organization(command.OrganizationId);
        var rights = await ImportRights.LoadAsync(access, currentUser.SubjectId, cancellationToken);
        rights.EnsureRun();
        var templateId = ImportAddress.Template(command.TemplateId);

        var template = await templates.FindAsync(organizationId, templateId, cancellationToken)
            ?? throw new NotFoundException(ImportErrorCodes.TemplateNotFound);

        if (command.FileId is null)
        {
            throw new ValidationException(
                ImportErrorCodes.ValidationFailed,
                new Dictionary<string, IReadOnlyList<FieldError>> { ["fileId"] = [new FieldError(ImportErrorCodes.ValidationFailed)] });
        }

        var now = clock.UtcNow;
        var file = await ImportFileInput.LoadAsync(files, organizationId, rights.UserId, command.FileId.Value, now, cancellationToken);
        var settings = template.Settings;
        var table = reader.Read(file.Content, file.FileName, ImportAddress.OptionsOf(settings.Source));
        var headers = DryRunEngine.HeaderIndex(table.Headers);

        var missing = DryRunEngine.MissingColumns(settings, headers);

        if (missing.Count > 0)
        {
            throw SourceRefusal.Because(
                ImportErrorCodes.FileColumnsMissing,
                new ErrorParams { ["columns"] = ErrorParam.Text(string.Join(", ", missing)) });
        }

        var identifiers = new HashSet<string>(StringComparer.Ordinal);

        if (DryRunEngine.IdentifierColumn(settings, headers) is { } column)
        {
            foreach (var row in table.Rows)
            {
                var text = column < row.Cells.Count ? row.Cells[column].Text : string.Empty;

                if (!string.IsNullOrWhiteSpace(text))
                {
                    identifiers.Add(DryRunEngine.NormalizeIdentifier(text));
                }
            }
        }

        var refs = await references.LoadAsync(organizationId, identifiers, cancellationToken);
        var set = rights.Set;
        var context = refs.ToContext(
            projectId => set.Holds(PermissionCatalog.ImportsImportRun, projectId) && set.Holds(PermissionCatalog.WorkItemsWorkItemWrite, projectId),
            projectId => set.Holds(PermissionCatalog.WorkItemsWorkItemRead, projectId));

        var outcome = DryRunEngine.Run(settings, table, context);
        var run = ImportDryRun.Record(
            Guid.CreateVersion7(), organizationId, template.Id, template.Version, file, DryRunTotals.Of(outcome.Rows), outcome.Notes, outcome.Rows, now);

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            dryRuns.Add(run);
            await unitOfWork.SaveChangesAsync(ct);

            return true;
        }, cancellationToken);

        return DryRunView.Of(run, RowResults.All, stale: false, texts);
    }
}
