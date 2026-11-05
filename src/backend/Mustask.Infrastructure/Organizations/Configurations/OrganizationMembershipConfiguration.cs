using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Organizations.Configurations;

/// <summary>
/// <c>organization_memberships</c> (mod-016 plan-backend § 3.3). One membership
/// per user per organization (BR-M1-5): the primary key <c>(organization_id,
/// user_id)</c>. The user id is the Keycloak <c>sub</c> and has <b>no</b> foreign
/// key to the user projection: a membership outlives the account (BR-M1-17), and a
/// creator the projection does not know yet must be storable (BR-M1-25). The role
/// is stored as the API's spelling, <c>admin</c>, <c>member</c> or <c>viewer</c>,
/// and a check refuses anything else; the two «role changed» columns are both set
/// or both null. The foreign key to the organization is <c>Restrict</c>:
/// organizations are never purged.
/// </summary>
internal sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.HasKey(membership => new { membership.OrganizationId, membership.UserId });

        builder.Property(membership => membership.UserId).HasMaxLength(OrganizationLimits.UserIdMaxLength).IsRequired();
        builder.Property(membership => membership.Role)
            .HasConversion(role => OrganizationRoles.ToCode(role), code => ParseRole(code))
            .HasMaxLength(OrganizationLimits.RoleMaxLength)
            .IsRequired();
        builder.Property(membership => membership.AddedBy).HasMaxLength(OrganizationLimits.UserIdMaxLength).IsRequired();
        builder.Property(membership => membership.AddedAt).IsRequired();
        builder.Property(membership => membership.RoleChangedBy).HasMaxLength(OrganizationLimits.UserIdMaxLength);

        builder.Ignore(membership => membership.IsAdmin);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(membership => membership.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        // "My organizations" and the access read look memberships up by user.
        builder.HasIndex(membership => membership.UserId, OrganizationsConstraints.MembershipUser);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_organization_memberships_role",
                $"role IN ('{OrganizationRoles.AdminCode}', '{OrganizationRoles.MemberCode}', '{OrganizationRoles.ViewerCode}')");
            table.HasCheckConstraint(
                "ck_organization_memberships_role_change",
                "(role_changed_by IS NULL) = (role_changed_at IS NULL)");
        });
    }

    private static OrganizationRole ParseRole(string code) =>
        OrganizationRoles.TryParse(code, out var role)
            ? role
            : throw new InvalidOperationException($"'{code}' is not an organization role; the check constraint should have refused it.");
}
