using Mustask.Domain.Imports.DryRun;

namespace Mustask.Domain.Imports;

/// <summary>An uploaded source file, kept in the database for 24 hours (BR-IE-14), owned by its uploader.</summary>
public sealed class ImportFile
{
    private ImportFile()
    {
        // EF materialization.
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string UserId { get; private set; } = null!;

    public string FileName { get; private set; } = null!;

    public byte[] Content { get; private set; } = null!;

    public long Size { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>The name cut to the stored width, keeping its extension.</summary>
    public static string FitName(string fileName)
    {
        if (fileName.Length <= ImportLimits.MaxFileNameLength)
        {
            return fileName;
        }

        var extension = Path.GetExtension(fileName);
        var keep = extension.Length is > 0 and < 20 ? extension : string.Empty;

        return fileName[..(ImportLimits.MaxFileNameLength - keep.Length)] + keep;
    }

    public static ImportFile Store(Guid id, Guid organizationId, string userId, string fileName, byte[] content, DateTimeOffset now) => new()
    {
        Id = id,
        OrganizationId = organizationId,
        UserId = userId,
        FileName = FitName(fileName),
        Content = content,
        Size = content.LongLength,
        CreatedAt = now,
        ExpiresAt = now + ImportLimits.FileLifetime,
    };
}

/// <summary>
/// A stored dry run (BR-IE-13, BR-IE-14): the report as a record of what was checked — not a batch, not a work item. It expires with the
/// file and names the template version it ran against, so a later save of the template makes it stale.
/// </summary>
public sealed class ImportDryRun
{
    private ImportDryRun()
    {
        // EF materialization.
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid TemplateId { get; private set; }

    public int TemplateVersion { get; private set; }

    public Guid FileId { get; private set; }

    public string UserId { get; private set; } = null!;

    public string FileName { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DryRunTotals Totals { get; private set; } = null!;

    public List<string> Notes { get; private set; } = [];

    public List<DryRunRowResult> Rows { get; private set; } = [];

    public static ImportDryRun Record(
        Guid id,
        Guid organizationId,
        Guid templateId,
        int templateVersion,
        ImportFile file,
        DryRunTotals totals,
        IEnumerable<string> notes,
        IEnumerable<DryRunRowResult> rows,
        DateTimeOffset now) => new()
    {
        Id = id,
        OrganizationId = organizationId,
        TemplateId = templateId,
        TemplateVersion = templateVersion,
        FileId = file.Id,
        UserId = file.UserId,
        FileName = file.FileName,
        CreatedAt = now,
        ExpiresAt = file.ExpiresAt,
        Totals = totals,
        Notes = notes.ToList(),
        Rows = rows.ToList(),
    };
}
