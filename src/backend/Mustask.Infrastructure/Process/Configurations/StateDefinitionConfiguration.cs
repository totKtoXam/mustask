using Mustask.Domain.Process;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Process.Configurations;

/// <summary>
/// <c>state_definitions</c>. A position and a name are each unique within the
/// type. The category is stored by doc-007's code as written (<c>InProgress</c>),
/// never as a number and never as its Russian label, which is derived
/// (<see cref="StateCategoryLabels"/>).
/// </summary>
internal sealed class StateDefinitionConfiguration : IEntityTypeConfiguration<StateDefinition>
{
    public void Configure(EntityTypeBuilder<StateDefinition> builder)
    {
        builder.HasKey(state => state.Id);
        builder.Property(state => state.Id).ValueGeneratedNever();

        builder.Property(state => state.Name).HasMaxLength(StateDefinition.NameMaxLength).IsRequired();
        builder.Property(state => state.Category)
            .HasConversion<string>()
            .HasMaxLength(StateDefinition.CategoryMaxLength)
            .IsRequired();
        builder.Property(state => state.Position).IsRequired();

        builder.HasIndex(state => new { state.WorkItemTypeId, state.Position })
            .IsUnique()
            .HasDatabaseName("ux_state_definitions_work_item_type_id_position");
        builder.HasIndex(state => new { state.WorkItemTypeId, state.Name })
            .IsUnique()
            .HasDatabaseName("ux_state_definitions_work_item_type_id_name");
    }
}
