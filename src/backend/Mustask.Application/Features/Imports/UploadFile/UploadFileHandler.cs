using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Imports;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.Source;

namespace Mustask.Application.Features.Imports.UploadFile;

public sealed record UploadFileCommand(string OrganizationId, string FileName, byte[] Content, FileSourceBody Options);

/// <summary>
/// `POST /imports/files` (api-contract § 3.1). Reads the file against every limit and safety rule first; a refused file is not stored.
/// The 24 h lifetime starts here; expired files and dry runs are cleaned on each upload.
/// </summary>
public sealed class UploadFileHandler(
    IImportFileRepository files,
    ISourceFileReader reader,
    IOrganizationAccess access,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<FileUploadedView> Handle(UploadFileCommand command, CancellationToken cancellationToken)
    {
        var organizationId = ImportAddress.Organization(command.OrganizationId);
        var rights = await ImportRights.LoadAsync(access, currentUser.SubjectId, cancellationToken);
        rights.EnsureWriteOrRun();

        if (command.Content.LongLength > ImportLimits.MaxFileBytes)
        {
            throw SourceRefusal.Because(ImportErrorCodes.FileTooLarge);
        }

        var table = reader.Read(command.Content, command.FileName, command.Options.ToOptions(ImportLimits.SampleRows));

        var now = clock.UtcNow;
        var file = ImportFile.Store(Guid.CreateVersion7(), organizationId, rights.UserId, command.FileName, command.Content, now);

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await files.DeleteExpiredAsync(now, ct);
            files.Add(file);
            await unitOfWork.SaveChangesAsync(ct);

            return true;
        }, cancellationToken);

        return new FileUploadedView(file.Id, file.FileName, file.Size, file.ExpiresAt, FileReadView.Of(table));
    }
}
