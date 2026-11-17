using Mustask.Domain.Images;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Images.Configurations;

/// <summary><c>images.image_contents</c>: the bytes, one row per image, deleted with it. The column is stored out of line uncompressed (set in the migration).</summary>
internal sealed class ImageContentConfiguration : IEntityTypeConfiguration<ImageContent>
{
    public void Configure(EntityTypeBuilder<ImageContent> builder)
    {
        builder.HasKey(content => content.ImageId);
        builder.Property(content => content.ImageId).ValueGeneratedNever();
        builder.Property(content => content.Content).IsRequired();

        builder.HasOne<Image>()
            .WithOne()
            .HasForeignKey<ImageContent>(content => content.ImageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("image_contents", ImageConfiguration.SchemaName);
    }
}
