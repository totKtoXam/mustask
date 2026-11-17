using Microsoft.EntityFrameworkCore;
using Mustask.Application.Abstractions.Imports;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.Templates;

namespace Mustask.Infrastructure.Imports;

/// <summary><see cref="IImportTemplateRepository"/> over <c>import_templates</c>.</summary>
public sealed class ImportTemplateRepository(MustaskDbContext context) : IImportTemplateRepository
{
    public Task<ImportTemplate?> FindAsync(Guid organizationId, Guid templateId, CancellationToken cancellationToken) =>
        context.ImportTemplates.SingleOrDefaultAsync(t => t.OrganizationId == organizationId && t.Id == templateId, cancellationToken);

    public async Task<ImportTemplate?> FindForUpdateAsync(Guid organizationId, Guid templateId, CancellationToken cancellationToken)
    {
        // Lock by id with raw SQL, then load with the regular tracking query (ADR-0010 "Consequences").
        var locked = await context.Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM import_templates WHERE id = {templateId} AND organization_id = {organizationId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return locked.Count == 0 ? null : await FindAsync(organizationId, templateId, cancellationToken);
    }

    public async Task<IReadOnlyList<ImportTemplate>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await context.ImportTemplates.AsNoTracking().Where(t => t.OrganizationId == organizationId).ToListAsync(cancellationToken);

    public async Task LockOrganizationTemplatesAsync(Guid organizationId, CancellationToken cancellationToken) =>
        _ = await context.Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM organizations WHERE id = {organizationId} FOR NO KEY UPDATE")
            .ToListAsync(cancellationToken);

    public void Add(ImportTemplate template) => context.ImportTemplates.Add(template);

    public void Remove(ImportTemplate template) => context.ImportTemplates.Remove(template);
}

/// <summary><see cref="IImportFileRepository"/> over <c>import_files</c>.</summary>
public sealed class ImportFileRepository(MustaskDbContext context) : IImportFileRepository
{
    public Task<ImportFile?> FindAsync(Guid organizationId, string userId, Guid fileId, DateTimeOffset now, CancellationToken cancellationToken) =>
        context.ImportFiles.AsNoTracking().SingleOrDefaultAsync(
            f => f.Id == fileId && f.OrganizationId == organizationId && f.UserId == userId && f.ExpiresAt > now, cancellationToken);

    public void Add(ImportFile file) => context.ImportFiles.Add(file);

    public async Task DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await context.ImportDryRuns.Where(r => r.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
        await context.ImportFiles.Where(f => f.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
    }
}

/// <summary><see cref="IImportDryRunRepository"/> over <c>import_dry_runs</c>.</summary>
public sealed class ImportDryRunRepository(MustaskDbContext context) : IImportDryRunRepository
{
    public Task<ImportDryRun?> FindAsync(Guid organizationId, string userId, Guid dryRunId, DateTimeOffset now, CancellationToken cancellationToken) =>
        context.ImportDryRuns.AsNoTracking().SingleOrDefaultAsync(
            r => r.Id == dryRunId && r.OrganizationId == organizationId && r.UserId == userId && r.ExpiresAt > now, cancellationToken);

    public void Add(ImportDryRun dryRun) => context.ImportDryRuns.Add(dryRun);
}
