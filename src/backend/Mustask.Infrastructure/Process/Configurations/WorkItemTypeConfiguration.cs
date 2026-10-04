using Mustask.Domain.Process;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Process.Configurations;

/// <summary>
/// <c>work_item_types</c>. The unique indexes are the storage guards behind "no
/// type, key or token repeats within a process" (plan-backend.md § 2). The
/// appearance index is deliberate for the MVP (status.md D-6: no two types
/// share a token); release 3 drops it in a migration of its own if custom types
/// are allowed to share one. The type's level is a foreign key with no
/// navigation, and there is no navigation back to the process. The key is
/// composite, <c>(process_id, backlog_level_id)</c> onto the alternate key
/// <c>(process_id, id)</c> of <c>backlog_levels</c>, so a type cannot sit on a
/// level of another process (BR-M2-1, qa.md D-2).
/// </summary>
internal sealed class WorkItemTypeConfiguration : IEntityTypeConfiguration<WorkItemType>
{
    public void Configure(EntityTypeBuilder<WorkItemType> builder)
    {
        builder.HasKey(type => type.Id);
        builder.Property(type => type.Id).ValueGeneratedNever();

        builder.Property(type => type.Key).HasMaxLength(WorkItemType.KeyMaxLength).IsRequired();
        builder.Property(type => type.Name).HasMaxLength(WorkItemType.NameMaxLength).IsRequired();
        builder.Property(type => type.Appearance)
            .HasConversion(token => token.ToWireName(), name => AppearanceTokenNames.FromWireName(name))
            .HasMaxLength(WorkItemType.AppearanceMaxLength)
            .IsRequired();
        builder.Property(type => type.Position).IsRequired();

        builder.HasOne<BacklogLevel>()
            .WithMany()
            .HasForeignKey(type => new { type.ProcessId, type.BacklogLevelId })
            .HasPrincipalKey(level => new { level.ProcessId, level.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(type => type.States)
            .WithOne()
            .HasForeignKey(state => state.WorkItemTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(type => type.States).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(type => new { type.ProcessId, type.Key })
            .IsUnique()
            .HasDatabaseName("ux_work_item_types_process_id_key");
        builder.HasIndex(type => new { type.ProcessId, type.Appearance })
            .IsUnique()
            .HasDatabaseName("ux_work_item_types_process_id_appearance");
        builder.HasIndex(type => new { type.ProcessId, type.Name })
            .IsUnique()
            .HasDatabaseName("ux_work_item_types_process_id_name");
        builder.HasIndex(type => new { type.ProcessId, type.Position })
            .IsUnique()
            .HasDatabaseName("ux_work_item_types_process_id_position");
    }
}
