using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Organizations.Configurations;

/// <summary>
/// <c>organization_appointments</c> (feat-033 plan-backend § 4.1): the insert-only record of administrator appointments. The
/// appointee and the appointer are Keycloak subjects with no foreign key to the user projection (as memberships); the
/// organization's foreign key is <c>Restrict</c> (organizations are never purged). The reason is <c>varchar(500)</c> — PostgreSQL
/// counts characters, the unit of BR-RS-5 — and a check refuses a blank one.
/// </summary>
internal sealed class OrganizationAppointmentConfiguration : IEntityTypeConfiguration<OrganizationAppointment>
{
    public void Configure(EntityTypeBuilder<OrganizationAppointment> builder)
    {
        builder.HasKey(appointment => appointment.Id);
        builder.Property(appointment => appointment.Id).ValueGeneratedNever();

        builder.Property(appointment => appointment.AppointeeId).HasMaxLength(OrganizationLimits.UserIdMaxLength).IsRequired();
        builder.Property(appointment => appointment.AppointedBy).HasMaxLength(OrganizationLimits.UserIdMaxLength).IsRequired();
        builder.Property(appointment => appointment.AppointedAt).IsRequired();
        builder.Property(appointment => appointment.Reason).HasMaxLength(OrganizationLimits.AppointmentReasonMaxLength).IsRequired();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(appointment => appointment.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(
                appointment => new { appointment.OrganizationId, appointment.AppointedAt, appointment.Id },
                OrganizationsConstraints.AppointmentOrganizationAppointedAt)
            .IsDescending(false, true, true);

        builder.ToTable(table => table.HasCheckConstraint("ck_organization_appointments_reason_not_blank", "btrim(reason) <> ''"));
    }
}
