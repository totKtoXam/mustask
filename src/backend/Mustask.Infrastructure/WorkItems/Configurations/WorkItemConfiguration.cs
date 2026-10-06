using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.WorkItems.Configurations;

/// <summary>
/// <c>work_items</c>. The item's number is unique in its project (BR-M3-5) and
/// so is the key of the submission that created it (BR-M3-26). Its area is an
/// area of its own project: the key is composite, <c>(project_id, area_id)</c>
/// onto mod-002's alternate key <c>(project_id, id)</c> of <c>areas</c>, so the
/// database refuses an area of another project (BR-M3-2, BR-M3-23), and it is
/// <c>Restrict</c>, so an area that holds an item is never removed under it
/// (BR-M3-24). The type and the state are mod-001's rows, referenced by identity
/// (BR-M2-9). The assignee is a user id with no key: the directory may not know
/// it (US-2 «Неизвестный пользователь»). The parent (feat-001) is a column, so an
/// item has one at most (BR-H-1), and its key is composite too,
/// <c>(project_id, parent_id)</c> onto the item's own alternate key
/// <c>(project_id, id)</c>: the database refuses a parent of another project, and
/// a check refuses the item as its own parent.
/// </summary>
internal sealed class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasAlternateKey(item => new { item.ProjectId, item.Id });
        builder.Property(item => item.ProjectId).IsRequired();
        builder.Property(item => item.Number).IsRequired();
        builder.Property(item => item.TypeId).IsRequired();
        builder.Property(item => item.StateId).IsRequired();
        builder.Property(item => item.AreaId).IsRequired();
        builder.Property(item => item.AssigneeId).HasMaxLength(WorkItemLimits.UserIdMaxLength);
        builder.Property(item => item.Title).HasMaxLength(WorkItemLimits.TitleMaxLength).IsRequired();
        builder.Property(item => item.Description).IsRequired();
        builder.Property(item => item.CreationRequestId).IsRequired();
        builder.Property(item => item.CreatedAt).IsRequired();
        builder.Property(item => item.CreatedBy).HasMaxLength(WorkItemLimits.UserIdMaxLength).IsRequired();
        builder.Property(item => item.ChangedAt).IsRequired();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(item => item.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Area>()
            .WithMany()
            .HasForeignKey(item => new { item.ProjectId, item.AreaId })
            .HasPrincipalKey(area => new { area.ProjectId, area.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WorkItemType>()
            .WithMany()
            .HasForeignKey(item => item.TypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StateDefinition>()
            .WithMany()
            .HasForeignKey(item => item.StateId)
            .OnDelete(DeleteBehavior.Restrict);

        // feat-001: Restrict, so a parent is never removed from under its children (BR-H-8; nothing purges in this cut).
        builder.HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(item => new { item.ProjectId, item.ParentId })
            .HasPrincipalKey(parent => new { parent.ProjectId, parent.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.ProjectId, item.Number }, WorkItemsConstraints.ProjectNumber).IsUnique();
        builder.HasIndex(item => new { item.ProjectId, item.CreationRequestId }, WorkItemsConstraints.ProjectCreationRequest).IsUnique();
        builder.HasIndex(item => new { item.ProjectId, item.ChangedAt, item.Number }, WorkItemsConstraints.ProjectChangedAt)
            .IsDescending(false, true, true);
        builder.HasIndex(item => new { item.ProjectId, item.AreaId }, WorkItemsConstraints.ProjectArea);
        // mod-006: the backlog's membership (Boards/BacklogMembership) is an equality on the project and a short list each
        // of types and areas, so every (type, area) pair is one range of this index and no other level's items are read.
        builder.HasIndex(item => new { item.ProjectId, item.TypeId, item.AreaId }, WorkItemsConstraints.ProjectTypeArea);
        // feat-001: the parent's key, and the form's children read (BR-H-7).
        builder.HasIndex(item => new { item.ProjectId, item.ParentId }, WorkItemsConstraints.ProjectParent);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(WorkItemsConstraints.NumberPositive, "number >= 1");
            table.HasCheckConstraint(WorkItemsConstraints.ParentNotSelf, "parent_id IS NULL OR parent_id <> id");
            table.HasCheckConstraint("ck_work_items_title_length", WorkItemsConstraints.TitleLengthCheck);
            table.HasCheckConstraint("ck_work_items_description_length", WorkItemsConstraints.DescriptionLengthCheck);
        });
    }
}
