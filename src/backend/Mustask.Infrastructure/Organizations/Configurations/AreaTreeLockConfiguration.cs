using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Organizations.Configurations;

/// <summary>
/// <c>area_tree_locks</c>: one row per organization that has written the tree's shape, locked <c>FOR UPDATE</c> by a creation, a
/// move and a deletion before they read the tree they decide on (ADR-0028 rule 5). Written by raw SQL only, like
/// <c>work_item_hierarchy_locks</c>.
/// </summary>
internal sealed class AreaTreeLock
{
    public const string Table = "area_tree_locks";

    private AreaTreeLock()
    {
        // EF materialization.
    }

    public Guid OrganizationId { get; private set; }
}

internal sealed class AreaTreeLockConfiguration : IEntityTypeConfiguration<AreaTreeLock>
{
    public void Configure(EntityTypeBuilder<AreaTreeLock> builder)
    {
        builder.HasKey(row => row.OrganizationId);
        builder.Property(row => row.OrganizationId).ValueGeneratedNever();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(row => row.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(AreaTreeLock.Table);
    }
}
