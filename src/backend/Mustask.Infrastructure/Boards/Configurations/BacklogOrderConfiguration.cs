using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Boards.Configurations;

/// <summary><c>backlog_orders</c>: one row per reordered backlog, keyed by (team, level); both keys <c>Restrict</c>, as nothing beneath is ever purged (ADR-0026).</summary>
internal sealed class BacklogOrderConfiguration : IEntityTypeConfiguration<BacklogOrder>
{
    public void Configure(EntityTypeBuilder<BacklogOrder> builder)
    {
        builder.HasKey(order => new { order.TeamId, order.BacklogLevelId });

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(order => order.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<BacklogLevel>()
            .WithMany()
            .HasForeignKey(order => order.BacklogLevelId)
            .OnDelete(DeleteBehavior.Restrict);

        // No DbSet names the table: the row is written by raw SQL only (BacklogRepository), so its name is set here.
        builder.ToTable(BacklogOrder.Table);
    }
}
