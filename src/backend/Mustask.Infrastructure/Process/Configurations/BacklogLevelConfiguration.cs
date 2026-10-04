using Mustask.Domain.Process;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Process.Configurations;

/// <summary>
/// <c>backlog_levels</c>. A rank and a name are each unique in the process. The
/// kind is stored by its wire name (<see cref="BacklogLevelKindNames"/>), the
/// same spelling the API serves, and never as a number. <c>(process_id, id)</c>
/// is an alternate key: the target of the composite foreign key of
/// <c>work_item_types</c>, which lets the database refuse a type placed on a
/// level of another process (BR-M2-1, qa.md D-2).
/// </summary>
internal sealed class BacklogLevelConfiguration : IEntityTypeConfiguration<BacklogLevel>
{
    public void Configure(EntityTypeBuilder<BacklogLevel> builder)
    {
        builder.HasKey(level => level.Id);
        builder.Property(level => level.Id).ValueGeneratedNever();
        builder.HasAlternateKey(level => new { level.ProcessId, level.Id });

        builder.Property(level => level.Name).HasMaxLength(BacklogLevel.NameMaxLength).IsRequired();
        builder.Property(level => level.Rank).IsRequired();
        builder.Property(level => level.Kind)
            .HasConversion(kind => kind.ToWireName(), name => BacklogLevelKindNames.FromWireName(name))
            .HasMaxLength(BacklogLevel.KindMaxLength)
            .IsRequired();

        builder.HasIndex(level => new { level.ProcessId, level.Rank })
            .IsUnique()
            .HasDatabaseName("ux_backlog_levels_process_id_rank");
        builder.HasIndex(level => new { level.ProcessId, level.Name })
            .IsUnique()
            .HasDatabaseName("ux_backlog_levels_process_id_name");
    }
}
