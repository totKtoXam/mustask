using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Imports;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.DryRun;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Imports.DownloadDryRunReport;

public sealed record DownloadDryRunReportQuery(string OrganizationId, string DryRunId);

/// <summary>`GET /imports/dry-runs/{id}/report` (api-contract § 4.3): every row of the file with its result and every reason, as .xlsx.</summary>
public sealed class DownloadDryRunReportHandler(
    IImportDryRunRepository dryRuns, IDryRunReportWriter writer, IReasonText texts, IOrganizationAccess access, ICurrentUser currentUser, IClock clock)
{
    public async Task<ReportFile> Handle(DownloadDryRunReportQuery query, CancellationToken cancellationToken)
    {
        var organizationId = ImportAddress.Organization(query.OrganizationId);
        var runId = ImportAddress.DryRun(query.DryRunId);
        var rights = await ImportRights.LoadAsync(access, currentUser.SubjectId, cancellationToken);
        rights.EnsureRun();

        var run = await dryRuns.FindAsync(organizationId, rights.UserId, runId, clock.UtcNow, cancellationToken)
            ?? throw new NotFoundException(ImportErrorCodes.DryRunNotFound);

        var lines = run.Rows.Select(r => new DryRunReportLine(
            r.Row,
            RowResults.LabelOf(r.Result),
            r.Title,
            r.Identifier,
            r.Project?.Name,
            r.Reasons.Select(x => texts.Render(x.Code, x.Params)).ToList())).ToList();

        var baseName = Path.GetFileNameWithoutExtension(run.FileName);

        return new ReportFile(writer.Write(lines), $"{(string.IsNullOrWhiteSpace(baseName) ? "import" : baseName)} — отчёт.xlsx");
    }
}
