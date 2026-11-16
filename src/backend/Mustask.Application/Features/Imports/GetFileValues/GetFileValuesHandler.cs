using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Imports;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Imports.DryRun;

namespace Mustask.Application.Features.Imports.GetFileValues;

public sealed record GetFileValuesQuery(string OrganizationId, string FileId, MappingProbeBody? Body);

/// <summary>
/// `POST /imports/files/{id}/values` (api-contract § 3.3): the distinct results of a mapping over the whole file, before recoding — the
/// source of the recoding table's rows. Identity ignores case and edge spaces; a row whose chain fails is not counted.
/// </summary>
public sealed class GetFileValuesHandler(
    IImportFileRepository files, ISourceFileReader reader, IOrganizationAccess access, ICurrentUser currentUser, IClock clock)
{
    private const int MaxValues = 1_000;

    public async Task<FileValuesView> Handle(GetFileValuesQuery query, CancellationToken cancellationToken)
    {
        var organizationId = ImportAddress.Organization(query.OrganizationId);
        var rights = await ImportRights.LoadAsync(access, currentUser.SubjectId, cancellationToken);
        rights.EnsureWriteOrRun();
        var fileId = ImportAddress.File(query.FileId);

        var mapping = ImportFileInput.RequireMapping(query.Body);
        var file = await ImportFileInput.LoadAsync(files, organizationId, rights.UserId, fileId, clock.UtcNow, cancellationToken);
        var table = reader.Read(file.Content, file.FileName, (query.Body!.Source ?? new FileSourceBody(null, null, null, null, null)).ToOptions());
        var headers = DryRunEngine.HeaderIndex(table.Headers);

        var counts = new Dictionary<string, (string First, int Rows)>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in table.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var evaluation = DryRunEngine.Evaluate(mapping, row, headers, table.DateSystem);

            if (evaluation.Failure is not null || evaluation.Value.IsEmpty)
            {
                continue;
            }

            var text = evaluation.Value.Display().Trim();

            if (text.Length == 0)
            {
                continue;
            }

            counts[text] = counts.TryGetValue(text, out var seen) ? (seen.First, seen.Rows + 1) : (text, 1);
        }

        if (counts.Count > MaxValues)
        {
            return new FileValuesView([], counts.Count, true);
        }

        var values = counts.Values.OrderBy(v => v.First, StringComparer.CurrentCultureIgnoreCase)
            .Select(v => new FileValueView(v.First, v.Rows)).ToList();

        return new FileValuesView(values, values.Count, false);
    }
}
