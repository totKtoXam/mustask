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
        // feat-028 (ADR-0028 rule 4): the target of the link's key onto the project, so the database refuses a link whose
        // organization is not the project's.
        builder.HasAlternateKey(project => new { project.OrganizationId, project.Id });

        builder.Property(project => project.OrganizationId).IsRequired();
        builder.Property(project => project.Description).HasColumnType("text");
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

        // mod-017 (BR-OT-4), feat-028 (BR-OL-7): the default-team setting references one of the project's own links: the key is
        // composite, (id, default_team_id) onto the link's primary key (project_id, team_id), so the database refuses a team
        // that is not linked to the project. A null setting leaves it unchecked (MATCH SIMPLE). Restrict: no link row is removed
        // under a setting; the unlink and the deletion clear the setting that names the team in their own operation first.
        builder.HasOne<ProjectTeam>()
            .WithMany()
            .HasForeignKey(project => new { project.Id, project.DefaultTeamId })
            .HasPrincipalKey(link => new { link.ProjectId, link.TeamId })
            .HasConstraintName(OrganizationsConstraints.ProjectDefaultTeamKey)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(project => new { project.OrganizationId, project.NormalizedName }, OrganizationsConstraints.ProjectName).IsUnique();
        builder.HasIndex(project => new { project.OrganizationId, project.Key }, OrganizationsConstraints.ProjectKey).IsUnique();

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_projects_status", "status IN ('Active', 'Deactivated')");
            table.HasCheckConstraint("ck_projects_name_length", OrganizationsConstraints.NameLengthCheck);
            table.HasCheckConstraint("ck_projects_key_format", "key ~ '^[A-Z][A-Z0-9]{1,9}$'");
            table.HasCheckConstraint("ck_projects_description_length", OrganizationsConstraints.DescriptionLengthCheck);
        });
    }
}
