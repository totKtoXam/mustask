using Mustask.Domain.Iterations;
using Mustask.Domain.WorkItems;
using Mustask.Infrastructure.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Iterations.Configurations;

/// <summary>
/// <c>sprint_snapshot_items</c> (BR-14): the close record. It leaves with its sprint (a Closed sprint is never removed) and has no key
/// onto <c>work_items</c> on purpose, so a purge keeps it.
/// </summary>
internal sealed class SprintSnapshotItemConfiguration : IEntityTypeConfiguration<SprintSnapshotItem>
{
    public void Configure(EntityTypeBuilder<SprintSnapshotItem> builder)
    {
        builder.HasKey(item => new { item.SprintId, item.WorkItemId });
        builder.Property(item => item.ProjectId).IsRequired();
        builder.Property(item => item.TypeId).IsRequired();
        builder.Property(item => item.Number).IsRequired();
        builder.Property(item => item.LevelId).IsRequired();
        builder.Property(item => item.AssigneeId).HasMaxLength(WorkItemLimits.UserIdMaxLength);
        builder.Property(item => item.StoryPoints).HasPrecision(WorkItemsConstraints.NumberPrecision, 2).IsRequired();
        builder.Property(item => item.IsDone).IsRequired();

        builder.HasOne<Sprint>()
            .WithMany()
            .HasForeignKey(item => item.SprintId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.SprintId, item.LevelId }, IterationsConstraints.SnapshotSprintLevel);

        builder.ToTable(SprintSnapshotItem.Table);
    }
}
