using Mustask.Domain.UserDirectory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.UserDirectory.Configurations;

/// <summary><c>directory_snapshots</c> — reconciliation snapshots, pruned to the newest few at each begin.</summary>
internal sealed class DirectorySnapshotConfiguration : IEntityTypeConfiguration<DirectorySnapshot>
{
    public const string TableName = "directory_snapshots";

    public void Configure(EntityTypeBuilder<DirectorySnapshot> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.EnumerationStart).IsRequired();

        builder.Property(s => s.State)
            .HasConversion<string>()
            .HasMaxLength(ProjectedUserConfiguration.EnumColumnLength)
            .IsRequired();

        builder.Property(s => s.BegunAt).IsRequired();
        builder.Property(s => s.CompletedAt);
        builder.Property(s => s.PageCount);
        builder.Property(s => s.EntryCount);
        builder.Property(s => s.Received).IsRequired();
        builder.Property(s => s.Added).IsRequired();
        builder.Property(s => s.Changed).IsRequired();
        builder.Property(s => s.Unchanged).IsRequired();
        builder.Property(s => s.HeldAsDeleted).IsRequired();
        builder.Property(s => s.MarkedAbsent).IsRequired();

        builder.Ignore(s => s.Tally);

        builder.HasIndex(s => s.EnumerationStart);
    }
}
