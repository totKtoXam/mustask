using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.WorkItems.Configurations;

/// <summary><c>work_item_counters</c>: one row per project that has created an item, keyed by the project.</summary>
internal sealed class WorkItemCounterConfiguration : IEntityTypeConfiguration<WorkItemCounter>
{
    public void Configure(EntityTypeBuilder<WorkItemCounter> builder)
    {
        builder.HasKey(counter => counter.ProjectId);
        builder.Property(counter => counter.ProjectId).ValueGeneratedNever();
        builder.Property(counter => counter.LastNumber).IsRequired();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(counter => counter.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        // No DbSet names the table: the counter is written by raw SQL only (WorkItemRepository), so its name is set here.
        builder.ToTable(WorkItemCounter.Table, table => table.HasCheckConstraint("ck_work_item_counters_last_number", "last_number >= 0"));
    }
}
