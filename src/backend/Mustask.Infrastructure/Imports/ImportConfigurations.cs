using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.DryRun;
using Mustask.Domain.Imports.Templates;
using Mustask.Domain.Organizations;

namespace Mustask.Infrastructure.Imports;

/// <summary><c>import_templates</c> (mod-024 plan § 5): the settings are one jsonb document; the name is unique per organization by its key.</summary>
internal sealed class ImportTemplateConfiguration : IEntityTypeConfiguration<ImportTemplate>
{
    public void Configure(EntityTypeBuilder<ImportTemplate> builder)
    {
        builder.HasKey(template => template.Id);
        builder.Property(template => template.Id).ValueGeneratedNever();
        builder.Property(template => template.Name).HasMaxLength(ImportLimits.MaxTemplateNameLength).IsRequired();
        builder.Property(template => template.NameKey).HasMaxLength(ImportLimits.MaxTemplateNameLength * 2).IsRequired();
        builder.Property(template => template.AuthorId).HasMaxLength(OrganizationLimits.UserIdMaxLength).IsRequired();
        builder.Property(template => template.Version).IsRequired();
        builder.Property(template => template.Settings)
            .HasColumnType("jsonb")
            .HasConversion(settings => TemplateJson.Serialize(settings), json => TemplateJson.Deserialize(json))
            .IsRequired();
        builder.Property(template => template.CreatedAt).IsRequired();
        builder.Property(template => template.UpdatedAt).IsRequired();

        builder.HasOne<Organization>().WithMany().HasForeignKey(template => template.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(template => new { template.OrganizationId, template.NameKey }).IsUnique().HasDatabaseName("ux_import_templates_organization_name_key");

        builder.ToTable("import_templates");
    }
}

/// <summary><c>import_files</c>: the bytes in the database, owner = uploader, expiring after 24 hours (BR-IE-14).</summary>
internal sealed class ImportFileConfiguration : IEntityTypeConfiguration<ImportFile>
{
    public void Configure(EntityTypeBuilder<ImportFile> builder)
    {
        builder.HasKey(file => file.Id);
        builder.Property(file => file.Id).ValueGeneratedNever();
        builder.Property(file => file.UserId).HasMaxLength(OrganizationLimits.UserIdMaxLength).IsRequired();
        builder.Property(file => file.FileName).HasMaxLength(260).IsRequired();
        builder.Property(file => file.Content).HasColumnType("bytea").IsRequired();
        builder.Property(file => file.Size).IsRequired();
        builder.Property(file => file.CreatedAt).IsRequired();
        builder.Property(file => file.ExpiresAt).IsRequired();

        builder.HasOne<Organization>().WithMany().HasForeignKey(file => file.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(file => file.ExpiresAt).HasDatabaseName("ix_import_files_expires_at");
        builder.HasIndex(file => new { file.OrganizationId, file.UserId }).HasDatabaseName("ix_import_files_organization_user");

        builder.ToTable("import_files");
    }
}

/// <summary><c>import_dry_runs</c>: the report as jsonb; no foreign key to a work item or to the template (a deleted template leaves its runs to expire).</summary>
internal sealed class ImportDryRunConfiguration : IEntityTypeConfiguration<ImportDryRun>
{
    public void Configure(EntityTypeBuilder<ImportDryRun> builder)
    {
        builder.HasKey(run => run.Id);
        builder.Property(run => run.Id).ValueGeneratedNever();
        builder.Property(run => run.TemplateId).IsRequired();
        builder.Property(run => run.TemplateVersion).IsRequired();
        builder.Property(run => run.FileId).IsRequired();
        builder.Property(run => run.UserId).HasMaxLength(OrganizationLimits.UserIdMaxLength).IsRequired();
        builder.Property(run => run.FileName).HasMaxLength(260).IsRequired();
        builder.Property(run => run.CreatedAt).IsRequired();
        builder.Property(run => run.ExpiresAt).IsRequired();
        builder.Property(run => run.Totals).HasColumnType("jsonb")
            .HasConversion(totals => DryRunJson.Serialize(totals), json => DryRunJson.Deserialize<DryRunTotals>(json)).IsRequired();
        builder.Property(run => run.Notes).HasColumnType("jsonb")
            .HasConversion(notes => DryRunJson.Serialize(notes), json => DryRunJson.Deserialize<List<string>>(json)).IsRequired();
        builder.Property(run => run.Rows).HasColumnType("jsonb")
            .HasConversion(rows => DryRunJson.Serialize(rows), json => DryRunJson.Deserialize<List<DryRunRowResult>>(json)).IsRequired();

        builder.HasOne<Organization>().WithMany().HasForeignKey(run => run.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(run => run.ExpiresAt).HasDatabaseName("ix_import_dry_runs_expires_at");

        builder.ToTable("import_dry_runs");
    }
}
