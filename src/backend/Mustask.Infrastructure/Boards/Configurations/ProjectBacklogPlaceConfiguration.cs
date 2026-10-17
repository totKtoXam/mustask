using Mustask.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Boards.Configurations;

/// <summary>
/// <c>project_backlog_places</c>: the key is the work item, so an item holds one project place at most. The place
/// belongs to its backlog's <c>project_backlog_orders</c> row, whose key index finds a backlog's places for the
/// reorder's drop; the membership read joins a place by its item. A place goes with its item, the one key that
/// cascades.
/// </summary>
internal sealed class ProjectBacklogPlaceConfiguration : IEntityTypeConfiguration<ProjectBacklogPlace>
{
    public void Configure(EntityTypeBuilder<ProjectBacklogPlace> builder)
    {
        builder.HasKey(place => place.WorkItemId);
        builder.Property(place => place.WorkItemId).ValueGeneratedNever();
        builder.Property(place => place.Position).IsRequired();

        builder.HasOne<WorkItem>()
            .WithOne()
            .HasForeignKey<ProjectBacklogPlace>(place => place.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ProjectBacklogOrder>()
            .WithMany()
            .HasForeignKey(place => new { place.ProjectId, place.BacklogLevelId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(ProjectBacklogPlace.Table, table => table.HasCheckConstraint("ck_project_backlog_places_position", "position >= 1"));
    }
}
