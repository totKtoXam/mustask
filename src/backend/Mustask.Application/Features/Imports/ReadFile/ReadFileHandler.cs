using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Imports;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Imports;

namespace Mustask.Application.Features.Imports.ReadFile;

public sealed record ReadFileQuery(string OrganizationId, string FileId, FileSourceBody Options);

/// <summary>`GET /imports/files/{id}/columns` (api-contract § 3.2): the stored file read again with other source options.</summary>
public sealed class ReadFileHandler(
    IImportFileRepository files, ISourceFileReader reader, IOrganizationAccess access, ICurrentUser currentUser, IClock clock)
{
    public async Task<FileReadView> Handle(ReadFileQuery query, CancellationToken cancellationToken)
    {
        var organizationId = ImportAddress.Organization(query.OrganizationId);
        var fileId = ImportAddress.File(query.FileId);
        var rights = await ImportRights.LoadAsync(access, currentUser.SubjectId, cancellationToken);
        rights.EnsureWriteOrRun();

        var file = await ImportFileInput.LoadAsync(files, organizationId, rights.UserId, fileId, clock.UtcNow, cancellationToken);
        var table = reader.Read(file.Content, file.FileName, query.Options.ToOptions(ImportLimits.SampleRows));

        return FileReadView.Of(table);
    }
}
