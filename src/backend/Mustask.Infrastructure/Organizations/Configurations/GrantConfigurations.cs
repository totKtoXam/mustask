using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Organizations.Configurations;

/// <summary>
/// The three grant tables (mod-019 plan-backend P-2): <c>area_grants</c>, <c>project_grants</c>, <c>team_grants</c>. The primary key
/// <c>(object, user)</c> is the rule "one grant per member and object" (BR-SA-1). The composite key
/// <c>(organization_id, user_id)</c> onto the organization membership <b>cascades</b>: removing the membership ends every grant in
/// the same statement, and the insert's key share closes the give-versus-remove race (BR-SA-3). The key onto the object cascades
/// for departments (hard-deleted) and projects (never deleted); a team is soft-deleted, so its deletion handler ends its grants.
/// </summary>
internal static class GrantConfiguration
{
    public static void Configure<TGrant, TObject>(
        EntityTypeBuilder<TGrant> builder,
        string table,
        string objectColumn,
        string primaryKeyName,
        string membershipKeyName,
        System.Linq.Expressions.Expression<Func<TObject, object?>> objectAlternateKey)
        where TGrant : class, IGrant
        where TObject : class
    {
        builder.HasKey(nameof(IGrant.ObjectId), nameof(IGrant.UserId)).HasName(primaryKeyName);

        builder.Property(grant => grant.ObjectId).HasColumnName(objectColumn).IsRequired();
        builder.Property(grant => grant.OrganizationId).IsRequired();
        builder.Property(grant => grant.UserId).HasMaxLength(OrganizationLimits.UserIdMaxLength).IsRequired();
        builder.Property(grant => grant.Role)
            .HasConversion(role => GrantRoles.ToCode(role), code => Parse(code))
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(grant => grant.GrantedBy).HasMaxLength(OrganizationLimits.UserIdMaxLength).IsRequired();
        builder.Property(grant => grant.GrantedAt).IsRequired();
        builder.Ignore(grant => grant.Scope);

        builder.HasOne<OrganizationMembership>()
            .WithMany()
            .HasForeignKey(nameof(IGrant.OrganizationId), nameof(IGrant.UserId))
            .HasPrincipalKey(membership => new { membership.OrganizationId, membership.UserId })
            .HasConstraintName(membershipKeyName)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<TObject>()
            .WithMany()
            .HasForeignKey(nameof(IGrant.OrganizationId), nameof(IGrant.ObjectId))
            .HasPrincipalKey(objectAlternateKey)
            .OnDelete(DeleteBehavior.Cascade);

        // The caller's grants in one organization (the single form's read): tens of rows among thousands.
        builder.HasIndex(nameof(IGrant.OrganizationId), nameof(IGrant.UserId));

        builder.ToTable(table);
    }

    private static GrantRole Parse(string code) =>
        GrantRoles.TryParse(code, out var role)
            ? role
            : throw new InvalidOperationException($"'{code}' is not a grant role.");
}

internal sealed class AreaGrantConfiguration : IEntityTypeConfiguration<AreaGrant>
{
    public void Configure(EntityTypeBuilder<AreaGrant> builder) =>
        GrantConfiguration.Configure<AreaGrant, Area>(
            builder, "area_grants", "area_id", OrganizationsConstraints.AreaGrantPrimaryKey, OrganizationsConstraints.AreaGrantMembership,
            area => new { area.OrganizationId, area.Id });
}

internal sealed class ProjectGrantConfiguration : IEntityTypeConfiguration<ProjectGrant>
{
    public void Configure(EntityTypeBuilder<ProjectGrant> builder) =>
        GrantConfiguration.Configure<ProjectGrant, Project>(
            builder, "project_grants", "project_id", OrganizationsConstraints.ProjectGrantPrimaryKey, OrganizationsConstraints.ProjectGrantMembership,
            project => new { project.OrganizationId, project.Id });
}

internal sealed class TeamGrantConfiguration : IEntityTypeConfiguration<TeamGrant>
{
    public void Configure(EntityTypeBuilder<TeamGrant> builder) =>
        GrantConfiguration.Configure<TeamGrant, Team>(
            builder, "team_grants", "team_id", OrganizationsConstraints.TeamGrantPrimaryKey, OrganizationsConstraints.TeamGrantMembership,
            team => new { team.OrganizationId, team.Id });
}
