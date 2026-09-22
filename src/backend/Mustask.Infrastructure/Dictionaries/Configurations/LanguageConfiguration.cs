using Mustask.Domain.Dictionaries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Dictionaries.Configurations;

/// <summary>`languages`. Открытый справочник — код это string, а не enum (ADR-0021 §2).</summary>
internal sealed class LanguageConfiguration : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Code).HasMaxLength(LanguageCode.MaxLength).IsRequired();
        // Уникальный индекс на Code — CodeUniqueIndexConvention, не здесь.

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_languages_code_length",
            $"char_length(code) BETWEEN {LanguageCode.MinLength} AND {LanguageCode.MaxLength}"));

        builder.Property(l => l.Endonym)
            .HasMaxLength(DictionaryLimits.EndonymMaxLength)
            .IsRequired()
            .HasDefaultValue(string.Empty);

        builder.Property(l => l.IsDefault).IsRequired();
        builder.Property(l => l.IsActive).IsRequired();
        builder.Property(l => l.SortOrder).IsRequired();

        builder.Property(l => l.UpdatedAt).IsRequired();
        builder.Property(l => l.UpdatedBy).HasMaxLength(256).IsRequired();

        // Ровно один язык по умолчанию — гарантия на уровне хранилища,
        // не только доменного правила Language.MakeDefault.
        builder.HasIndex(l => l.IsDefault)
            .HasDatabaseName("ix_languages_is_default")
            .IsUnique()
            .HasFilter("is_default");
    }
}
