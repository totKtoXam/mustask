using Mustask.Domain.Images;
using Mustask.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Images.Configurations;

/// <summary>
/// <c>images.images</c> (ADR-0045 decision 1): the metadata of a stored picture, in the schema of its own. The key onto
/// <c>work_items</c> cascades (ADR-0030 rule 7): the purge removes bound and unbound uploads alike in its transaction.
/// </summary>
internal sealed class ImageConfiguration : IEntityTypeConfiguration<Image>
{
    public const string SchemaName = "images";

    public const int StringMaxLength = 32;

    public void Configure(EntityTypeBuilder<Image> builder)
    {
        builder.HasKey(image => image.Id);
        builder.Property(image => image.Id).ValueGeneratedNever();
        builder.Property(image => image.Purpose).HasConversion<string>().HasMaxLength(StringMaxLength).IsRequired();
        builder.Property(image => image.Format).HasConversion<string>().HasMaxLength(StringMaxLength).IsRequired();
        builder.Property(image => image.ByteLength).IsRequired();
        builder.Property(image => image.Width);
        builder.Property(image => image.Height);
        builder.Property(image => image.FrameCount).IsRequired();
        builder.Property(image => image.UploadedBy).HasMaxLength(WorkItemLimits.UserIdMaxLength).IsRequired();
        builder.Property(image => image.UploadedAt).IsRequired();
        builder.Property(image => image.BoundAt);
        builder.Property(image => image.WorkItemId);

        builder.HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(image => image.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(image => image.WorkItemId, "ix_images_work_item_id");
        builder.HasIndex(image => new { image.UploadedAt, image.Id }, "ix_images_unbound_uploaded_at").HasFilter("bound_at IS NULL");

        builder.ToTable(
            "images",
            SchemaName,
            table =>
            {
                table.HasCheckConstraint("ck_images_purpose_owner", "purpose = 'WorkItemDescription' AND work_item_id IS NOT NULL");
                table.HasCheckConstraint("ck_images_format", "format IN ('Png', 'Jpeg', 'Gif', 'Webp')");
                table.HasCheckConstraint("ck_images_byte_length", "byte_length BETWEEN 1 AND 5242880");
                table.HasCheckConstraint(
                    "ck_images_dimensions",
                    "(width IS NULL OR width BETWEEN 1 AND 20000) AND (height IS NULL OR height BETWEEN 1 AND 20000) AND frame_count BETWEEN 1 AND 1000");
            });
    }
}
