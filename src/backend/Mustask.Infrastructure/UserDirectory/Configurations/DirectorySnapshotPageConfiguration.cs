using Mustask.Domain.UserDirectory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.UserDirectory.Configurations;

/// <summary><c>directory_snapshot_pages</c> — one row per applied page; goes with its snapshot.</summary>
internal sealed class DirectorySnapshotPageConfiguration : IEntityTypeConfiguration<DirectorySnapshotPage>
{
    public const string TableName = "directory_snapshot_pages";

    public void Configure(EntityTypeBuilder<DirectorySnapshotPage> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(p => new { p.SnapshotId, p.PageNumber });

        builder.Property(p => p.Received).IsRequired();
        builder.Property(p => p.Added).IsRequired();
        builder.Property(p => p.Changed).IsRequired();
        builder.Property(p => p.Unchanged).IsRequired();
        builder.Property(p => p.HeldAsDeleted).IsRequired();
        builder.Property(p => p.AppliedAt).IsRequired();

        builder.Ignore(p => p.Tally);

        builder.HasOne<DirectorySnapshot>()
            .WithMany()
            .HasForeignKey(p => p.SnapshotId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
