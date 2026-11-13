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

/// <summary>The stored dry runs (<c>import_dry_runs</c>).</summary>
public interface IImportDryRunRepository
{
    /// <summary>The caller's own unexpired dry run; <c>null</c> otherwise.</summary>
    Task<ImportDryRun?> FindAsync(Guid organizationId, string userId, Guid dryRunId, DateTimeOffset now, CancellationToken cancellationToken);

    void Add(ImportDryRun dryRun);
}
