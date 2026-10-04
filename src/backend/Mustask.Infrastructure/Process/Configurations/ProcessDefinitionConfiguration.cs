using Mustask.Domain.Process;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Process.Configurations;

/// <summary>
/// <c>processes</c>. One row per organization: <c>organization_id</c> is the
/// opaque seed key (doc-007 R-5) and its unique index is the target of the
/// seed's <c>ON CONFLICT</c>, the storage guard of BR-M2-9. Foreign keys are
/// <c>Restrict</c> throughout: nothing of a process is ever deleted.
/// </summary>
internal sealed class ProcessDefinitionConfiguration : IEntityTypeConfiguration<ProcessDefinition>
{
    public void Configure(EntityTypeBuilder<ProcessDefinition> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.OrganizationId).IsRequired();
        builder.HasIndex(p => p.OrganizationId).IsUnique().HasDatabaseName("ux_processes_organization_id");

        builder.HasMany(p => p.BacklogLevels)
            .WithOne()
            .HasForeignKey(level => level.ProcessId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(p => p.BacklogLevels).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.WorkItemTypes)
            .WithOne()
            .HasForeignKey(type => type.ProcessId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(p => p.WorkItemTypes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
