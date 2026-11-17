using Mustask.Domain.Imports;
using Mustask.Domain.Imports.Templates;

namespace Mustask.Application.Abstractions.Imports;

/// <summary>The templates of organizations (<c>import_templates</c>). Writes go through the domain; the caller saves through the unit of work.</summary>
public interface IImportTemplateRepository
{
    /// <summary>The template by id inside the organization, tracked; <c>null</c> when there is none.</summary>
    Task<ImportTemplate?> FindAsync(Guid organizationId, Guid templateId, CancellationToken cancellationToken);

    /// <summary>As <see cref="FindAsync"/>, taking the row lock <c>FOR UPDATE</c> first (last save wins, BR-IE-18, ADR-0011).</summary>
    Task<ImportTemplate?> FindForUpdateAsync(Guid organizationId, Guid templateId, CancellationToken cancellationToken);

    /// <summary>All templates of the organization, not tracked (at most <see cref="ImportLimits.MaxTemplates"/>).</summary>
    Task<IReadOnlyList<ImportTemplate>> ListAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <summary>The organization's templates in a lock: serializes creates so the limit and the name are checked race-free.</summary>
    Task LockOrganizationTemplatesAsync(Guid organizationId, CancellationToken cancellationToken);

    void Add(ImportTemplate template);

    void Remove(ImportTemplate template);
}

/// <summary>The uploaded files (<c>import_files</c>).</summary>
public interface IImportFileRepository
{
    /// <summary>The caller's own unexpired file; <c>null</c> for an unknown, another user's or expired one.</summary>
    Task<ImportFile?> FindAsync(Guid organizationId, string userId, Guid fileId, DateTimeOffset now, CancellationToken cancellationToken);

    void Add(ImportFile file);

    /// <summary>Deletes files and dry runs past their expiry; run on every upload (the plan's clean-up).</summary>
    Task DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>Reads what a template's targets and a file's identifiers are checked against (BR-IE-7, BR-IE-12): reads only.</summary>
public interface IImportReferenceReader
{
    /// <summary>
    /// The organization's projects, the process's types and states, the members, the teams and their links, and the items named by
    /// <paramref name="identifiers"/> (in any state; a recycle-bin item is marked deleted).
    /// </summary>
    Task<Mustask.Domain.Imports.DryRun.ImportReferences> LoadAsync(
        Guid organizationId, IReadOnlyCollection<string> identifiers, CancellationToken cancellationToken);
}

/// <summary>Writes the full dry-run report as a .xlsx file (BR-IE-13, BR-IE-22).</summary>
public interface IDryRunReportWriter
{
    byte[] Write(IReadOnlyList<DryRunReportLine> rows);
}

/// <summary>One line of the report: row number, result (Russian), title, identifier, project and the rendered reasons.</summary>
public sealed record DryRunReportLine(int Row, string Result, string? Title, string? Identifier, string? Project, IReadOnlyList<string> Reasons);

/// <summary>The stored dry runs (<c>import_dry_runs</c>).</summary>
public interface IImportDryRunRepository
{
    /// <summary>The caller's own unexpired dry run; <c>null</c> otherwise.</summary>
    Task<ImportDryRun?> FindAsync(Guid organizationId, string userId, Guid dryRunId, DateTimeOffset now, CancellationToken cancellationToken);

    void Add(ImportDryRun dryRun);
}
