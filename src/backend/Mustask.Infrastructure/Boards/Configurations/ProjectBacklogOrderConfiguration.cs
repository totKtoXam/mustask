using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Boards.Configurations;

/// <summary><c>project_backlog_orders</c>: one row per reordered project backlog, keyed by (project, level); both keys <c>Restrict</c>, as nothing beneath is ever purged (ADR-0026).</summary>
internal sealed class ProjectBacklogOrderConfiguration : IEntityTypeConfiguration<ProjectBacklogOrder>
{
    public void Configure(EntityTypeBuilder<ProjectBacklogOrder> builder)
    {
        builder.HasKey(order => new { order.ProjectId, order.BacklogLevelId });

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(order => order.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<BacklogLevel>()
            .WithMany()
            .HasForeignKey(order => order.BacklogLevelId)
            .OnDelete(DeleteBehavior.Restrict);

        // No DbSet names the table: the row is written by raw SQL only (ProjectBacklogRepository), so its name is set here.
        builder.ToTable(ProjectBacklogOrder.Table);
    }
}
