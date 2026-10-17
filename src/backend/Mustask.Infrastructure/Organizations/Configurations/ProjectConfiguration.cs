using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Organizations.Configurations;

/// <summary>
/// <c>projects</c> (plan-backend § 3). The normalized name and the key are each
/// unique in the organization over all rows: a deactivated project keeps both
/// for good (BR-M1-1, ADR-0026 rule 6). The key's form is pinned by a check
/// as a storage backstop of <see cref="ProjectKey"/>.
/// </summary>
internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(project => project.Id);
        builder.Property(project => project.Id).ValueGeneratedNever();

        builder.Property(project => project.OrganizationId).IsRequired();
        builder.Property(project => project.Name).HasMaxLength(OrganizationLimits.NameMaxLength).IsRequired();
        builder.Property(project => project.NormalizedName).HasMaxLength(OrganizationLimits.NameMaxLength).IsRequired();
        builder.Property(project => project.Key).HasMaxLength(OrganizationLimits.KeyMaxLength).IsRequired();
        builder.Property(project => project.Status)
            .HasConversion<string>()
            .HasMaxLength(OrganizationLimits.StatusMaxLength)
            .IsRequired();
        builder.Property(project => project.CreatedAt).IsRequired();
        builder.Property(project => project.CreatedBy).HasMaxLength(OrganizationLimits.CreatedByMaxLength).IsRequired();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(project => project.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        // mod-017 (BR-OT-4): the default-team setting references one of the project's own teams: the key is composite,
        // (id, default_team_id) onto the alternate key (project_id, id) of teams, so the database refuses another project's
        // team. A null setting leaves it unchecked (MATCH SIMPLE). Restrict: no team row is removed, a team is deleted by
        // its status, and the deletion clears the setting that names it in its own operation (BR-OT-12).
        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(project => new { project.Id, project.DefaultTeamId })
            .HasPrincipalKey(team => new { team.ProjectId, team.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(project => new { project.OrganizationId, project.NormalizedName }, OrganizationsConstraints.ProjectName).IsUnique();
        builder.HasIndex(project => new { project.OrganizationId, project.Key }, OrganizationsConstraints.ProjectKey).IsUnique();

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_projects_status", "status IN ('Active', 'Deactivated')");
            table.HasCheckConstraint("ck_projects_name_length", OrganizationsConstraints.NameLengthCheck);
            table.HasCheckConstraint("ck_projects_key_format", "key ~ '^[A-Z][A-Z0-9]{1,9}$'");
        });
    }
}
