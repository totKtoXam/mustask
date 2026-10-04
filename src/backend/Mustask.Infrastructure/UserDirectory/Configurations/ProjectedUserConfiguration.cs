using Mustask.Domain.UserDirectory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.UserDirectory.Configurations;

/// <summary>
/// <c>directory_users</c> — the user projection (mod-014 plan-backend.md § 4).
/// A search scans it with two <c>LIKE</c>s over the pre-normalised columns and
/// orders by the stored collation key; the index serves the default "active
/// only" search in its order.
/// </summary>
internal sealed class ProjectedUserConfiguration : IEntityTypeConfiguration<ProjectedUser>
{
    public const string TableName = "directory_users";

    /// <summary>The longest lifecycle or state name stored as a string.</summary>
    internal const int EnumColumnLength = 16;

    /// <summary>A <see cref="DirectoryCollation.Version"/> fits with room to spare.</summary>
    internal const int SortKeyVersionLength = 64;

    public void Configure(EntityTypeBuilder<ProjectedUser> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasMaxLength(UserDirectoryLimits.FieldMaxLength).ValueGeneratedNever();

        builder.Property(u => u.Username).HasMaxLength(UserDirectoryLimits.FieldMaxLength).IsRequired();
        builder.Property(u => u.FirstName).HasMaxLength(UserDirectoryLimits.FieldMaxLength).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(u => u.LastName).HasMaxLength(UserDirectoryLimits.FieldMaxLength).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(u => u.Email).HasMaxLength(UserDirectoryLimits.FieldMaxLength).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(u => u.Enabled).IsRequired();

        builder.Property(u => u.Lifecycle)
            .HasConversion<string>()
            .HasMaxLength(EnumColumnLength)
            .IsRequired();

        builder.Property(u => u.Moment).IsRequired();

        builder.Property(u => u.DisplayName).HasMaxLength(UserDirectoryLimits.DisplayNameMaxLength).IsRequired();
        builder.Property(u => u.SearchName).HasColumnType("text").IsRequired();
        builder.Property(u => u.SearchEmail).HasColumnType("text").IsRequired();
        builder.Property(u => u.SortKey).HasColumnType("bytea").IsRequired();
        builder.Property(u => u.SortKeyVersion).HasMaxLength(SortKeyVersionLength).IsRequired();
        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.LastSnapshotId);
        builder.Property(u => u.ChangedAt).IsRequired();

        builder.HasIndex(u => new { u.IsActive, u.SortKey });
    }
}
