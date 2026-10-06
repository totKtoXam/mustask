using Mustask.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Boards.Configurations;

/// <summary>
/// <c>backlog_places</c>: the key is the work item, so an item holds one place
/// at most (spec § The stack rank). The place belongs to its backlog's
/// <c>backlog_orders</c> row, whose key index finds a backlog's places for the
/// reorder's drop; the membership read joins a place by its item. A place goes
/// with its item, the one key that cascades.
/// </summary>
internal sealed class BacklogPlaceConfiguration : IEntityTypeConfiguration<BacklogPlace>
{
    public void Configure(EntityTypeBuilder<BacklogPlace> builder)
    {
        builder.HasKey(place => place.WorkItemId);
        builder.Property(place => place.WorkItemId).ValueGeneratedNever();
        builder.Property(place => place.Position).IsRequired();

        builder.HasOne<WorkItem>()
            .WithOne()
            .HasForeignKey<BacklogPlace>(place => place.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<BacklogOrder>()
            .WithMany()
            .HasForeignKey(place => new { place.TeamId, place.BacklogLevelId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(BacklogPlace.Table, table => table.HasCheckConstraint("ck_backlog_places_position", "position >= 1"));
    }
}
