using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.WorkItems.Configurations;

/// <summary><c>work_item_hierarchy_locks</c>: one row per project that has written a parent, keyed by the project.</summary>
internal sealed class WorkItemHierarchyLockConfiguration : IEntityTypeConfiguration<WorkItemHierarchyLock>
{
    public void Configure(EntityTypeBuilder<WorkItemHierarchyLock> builder)
    {
        builder.HasKey(hierarchy => hierarchy.ProjectId);
        builder.Property(hierarchy => hierarchy.ProjectId).ValueGeneratedNever();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(hierarchy => hierarchy.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        // No DbSet names the table: the row is written by raw SQL only (WorkItemRepository), so its name is set here.
        builder.ToTable(WorkItemHierarchyLock.Table);
    }
}
