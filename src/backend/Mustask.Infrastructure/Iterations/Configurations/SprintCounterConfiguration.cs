using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Iterations.Configurations;

/// <summary><c>sprint_counters</c>: one row per team that has created a sprint, keyed by the team.</summary>
internal sealed class SprintCounterConfiguration : IEntityTypeConfiguration<SprintCounter>
{
    public void Configure(EntityTypeBuilder<SprintCounter> builder)
    {
        builder.HasKey(counter => counter.TeamId);
        builder.Property(counter => counter.TeamId).ValueGeneratedNever();
        builder.Property(counter => counter.LastNumber).IsRequired();

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(counter => counter.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(SprintCounter.Table, table => table.HasCheckConstraint("ck_sprint_counters_last_number", "last_number >= 0"));
    }
}
