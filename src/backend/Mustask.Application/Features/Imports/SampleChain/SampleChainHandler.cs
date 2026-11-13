using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Imports;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.DryRun;
using Mustask.Domain.Imports.Templates;

namespace Mustask.Application.Features.Imports.SampleChain;

public sealed record SampleChainQuery(string OrganizationId, string FileId, MappingProbeBody? Body);

/// <summary>`POST /imports/files/{id}/chain-sample` (api-contract § 3.4): the first 5 data rows through each step of a chain.</summary>
public sealed class SampleChainHandler(
    IImportFileRepository files, ISourceFileReader reader, IOrganizationAccess access, ICurrentUser currentUser, IClock clock)
{
    public async Task<ChainSampleView> Handle(SampleChainQuery query, CancellationToken cancellationToken)
    {
        var organizationId = ImportAddress.Organization(query.OrganizationId);
        var rights = await ImportRights.LoadAsync(access, currentUser.SubjectId, cancellationToken);
        rights.EnsureWriteOrRun();
        var fileId = ImportAddress.File(query.FileId);

        var mapping = ImportFileInput.RequireMapping(query.Body);
        var file = await ImportFileInput.LoadAsync(files, organizationId, rights.UserId, fileId, clock.UtcNow, cancellationToken);
        var source = query.Body!.Source ?? new FileSourceBody(null, null, null, null, null);
        var table = reader.Read(file.Content, file.FileName, source.ToOptions(ImportLimits.SampleRows));
        var headers = DryRunEngine.HeaderIndex(table.Headers);
        var converters = mapping.Chain.Select(s => s.Converter).ToList();

        var rows = new List<ChainRowSampleView>();

        foreach (var row in table.Rows.Take(ImportLimits.SampleRows))
        {
            var evaluation = DryRunEngine.Evaluate(mapping, row, headers, table.DateSystem);
            var done = evaluation.Steps ?? [];
            ChainErrorView? rowError = null;

            if (evaluation.Failure is not null)
            {
                var parameters = evaluation.Failure.Timeout
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>
                    {
                        ["column"] = evaluation.Column ?? string.Empty,
                        ["cell"] = evaluation.Cell ?? string.Empty,
                        ["value"] = evaluation.Original,
                        ["converter"] = evaluation.Failure.Converter,
                    };
                rowError = new ChainErrorView(evaluation.Failure.Timeout ? ImportErrorCodes.RowRegexTimeout : ImportErrorCodes.RowConvertFailed, parameters);
            }

            var steps = new List<ChainStepSampleView>();

            for (var i = 0; i < converters.Count; i++)
            {
                if (i < done.Count)
                {
                    steps.Add(new ChainStepSampleView(converters[i], done[i].Display(), null));
                }
                else
                {
                    steps.Add(new ChainStepSampleView(converters[i], null, i == done.Count ? rowError : null));
                }
            }

            rows.Add(new ChainRowSampleView(row.Number, evaluation.Original, steps, rowError is null ? evaluation.Value.Display() : null, rowError));
        }

        return new ChainSampleView(rows);
    }
}
