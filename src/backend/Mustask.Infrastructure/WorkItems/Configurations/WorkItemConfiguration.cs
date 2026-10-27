using System.Text.Json;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.WorkItems.Configurations;

/// <summary>
/// <c>work_items</c>. The item's number is unique in its project (BR-M3-5) and
/// so is the key of the submission that created it (BR-M3-26). An item has no area of its own (mod-018 BR-AW-2, BR-AW-11): its place in the tree is
/// its project's links. The type and the state are mod-001's rows, referenced by identity
/// (BR-M2-9). The assignee is a user id with no key: the directory may not know
/// it (US-2 «Неизвестный пользователь»). The parent (feat-001) is a column, so an
/// item has one at most (BR-H-1), and its key is composite too,
/// <c>(project_id, parent_id)</c> onto the item's own alternate key
/// <c>(project_id, id)</c>: the database refuses a parent of another project, and
/// a check refuses the item as its own parent. mod-017 adds the owning team: a column, with a composite key onto the
/// project's teams (<c>(project_id, owning_team_id)</c>) in the same way.
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
        builder.Property(item => item.AssigneeId).HasMaxLength(WorkItemLimits.UserIdMaxLength);
        builder.Property(item => item.Title).HasMaxLength(WorkItemLimits.TitleMaxLength).IsRequired();
        builder.Property(item => item.Description).IsRequired();
        builder.Property(item => item.CreationRequestId).IsRequired();
        builder.Property(item => item.CreatedAt).IsRequired();
        builder.Property(item => item.CreatedBy).HasMaxLength(WorkItemLimits.UserIdMaxLength).IsRequired();
        builder.Property(item => item.ChangedAt).IsRequired();

        // mod-005 (ADR-0030 rule 1): the marks of a deletion — three nullable columns, all-or-none by the check below. Only
        // Delete and Restore write them (private setters). The deleter is a user id with no key, as the creator is.
        builder.Property(item => item.DeletedFromStateId);
        builder.Property(item => item.DeletedBy).HasMaxLength(WorkItemLimits.UserIdMaxLength);
        builder.Property(item => item.DeletedAt);

        // feat-022: the fields per type. Priority defaults to 2 in the database too, so a row written outside the domain
        // (the migration's existing rows, a bulk insert) takes BR-M3-37's value; the others are null where the type lacks
        // them, and the domain decides which a type has. Numbers keep two decimals in at most four integer digits.
        builder.Property(item => item.Priority).IsRequired().HasDefaultValue(WorkItemFieldRules.DefaultPriority);
        builder.Property(item => item.StoryPoints).HasPrecision(WorkItemsConstraints.NumberPrecision, WorkItemFieldRules.NumberDecimals);
        builder.Property(item => item.Effort).HasPrecision(WorkItemsConstraints.NumberPrecision, WorkItemFieldRules.NumberDecimals);
        builder.Property(item => item.TimeCriticality).HasPrecision(WorkItemsConstraints.NumberPrecision, WorkItemFieldRules.NumberDecimals);
        builder.Property(item => item.OriginalEstimate).HasPrecision(WorkItemsConstraints.NumberPrecision, WorkItemFieldRules.NumberDecimals);
        builder.Property(item => item.RemainingWork).HasPrecision(WorkItemsConstraints.NumberPrecision, WorkItemFieldRules.NumberDecimals);
        builder.Property(item => item.CompletedWork).HasPrecision(WorkItemsConstraints.NumberPrecision, WorkItemFieldRules.NumberDecimals);
        builder.Property(item => item.Activity).HasConversion<string>().HasMaxLength(WorkItemsConstraints.ListKeyMaxLength);
        builder.Property(item => item.ValueArea).HasConversion<string>().HasMaxLength(WorkItemsConstraints.ListKeyMaxLength);

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
        };
        var criteriaComparer = new ValueComparer<IReadOnlyList<AcceptanceCriterion>?>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c == null ? 0 : c.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            c => c == null ? null : c.ToList());

        builder.Property(item => item.AcceptanceCriteria)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, jsonOptions),
                v => v == null ? null : (string.IsNullOrWhiteSpace(v) || v == "[]"
                    ? new List<AcceptanceCriterion>()
                    : (v.StartsWith('[')
                        ? (JsonSerializer.Deserialize<List<AcceptanceCriterion>>(v, jsonOptions) ?? new List<AcceptanceCriterion>())
                        : new List<AcceptanceCriterion> { new(string.Empty, string.Empty, v) })))
            .Metadata.SetValueComparer(criteriaComparer);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(item => item.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        // mod-017 (BR-OT-1), feat-028 (BR-OL-6): the owning team is a team linked to the item's own project. The key is
        // composite, (project_id, owning_team_id) onto the link's primary key (project_id, team_id), so the database refuses a
        // team that is not linked to the project; Restrict, so a link that owns an item is never removed from under it (an
        // unlink and a deletion reassign the items first, ADR-0028 rule 7). A null team leaves the key unchecked
        // (MATCH SIMPLE): «Без команды».
        builder.HasOne<ProjectTeam>()
            .WithMany()
            .HasForeignKey(item => new { item.ProjectId, item.OwningTeamId })
            .HasPrincipalKey(link => new { link.ProjectId, link.TeamId })
            .HasConstraintName(WorkItemsConstraints.OwningTeamKey)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WorkItemType>()
            .WithMany()
            .HasForeignKey(item => item.TypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StateDefinition>()
            .WithMany()
            .HasForeignKey(item => item.StateId)
            .OnDelete(DeleteBehavior.Restrict);

        // mod-005: a second key to the same principal as StateId. Restrict: a state an item remembers cannot be removed from
        // under it — the database's half of doc-001 BR-14 (b). A null mark leaves the key unchecked.
        builder.HasOne<StateDefinition>()
            .WithMany()
            .HasForeignKey(item => item.DeletedFromStateId)
            .OnDelete(DeleteBehavior.Restrict);

        // feat-001: Restrict, so a parent is never removed from under its children (BR-H-8). mod-005's purge clears the
        // children's link first, in the same transaction (ADR-0030 rule 5), so a forgotten clear fails the delete loudly.
        builder.HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(item => new { item.ProjectId, item.ParentId })
            .HasPrincipalKey(parent => new { parent.ProjectId, parent.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.ProjectId, item.Number }, WorkItemsConstraints.ProjectNumber).IsUnique();
        builder.HasIndex(item => new { item.ProjectId, item.CreationRequestId }, WorkItemsConstraints.ProjectCreationRequest).IsUnique();
        builder.HasIndex(item => new { item.ProjectId, item.ChangedAt, item.Number }, WorkItemsConstraints.ProjectChangedAt)
            .IsDescending(false, true, true);
        // mod-006: the backlog's membership (Boards/BacklogMembership) is an equality on the project and a short list each
        // of types, so every type is one range of this index and no other level's items are read (ADR-0028 rule 13).
        builder.HasIndex(item => new { item.ProjectId, item.TypeId }, WorkItemsConstraints.ProjectType);
        // feat-001: the parent's key, and the form's children read (BR-H-7).
        builder.HasIndex(item => new { item.ProjectId, item.ParentId }, WorkItemsConstraints.ProjectParent);
        // mod-017: a team's views are an equality on the project, the team and a short list of types, so every (team, type)
        // pair is one range of this index; it also serves the owner key above, so EF adds no second index for it.
        builder.HasIndex(item => new { item.ProjectId, item.OwningTeamId, item.TypeId }, WorkItemsConstraints.ProjectOwningTeamType);
        // feat-040 (ADR-0028 rule 13): a team's views across its projects are an equality on the team and a short list of types, so
        // every (team, type) pair is one range of this index; the project-led one above cannot serve them without a skip scan.
        builder.HasIndex(item => new { item.OwningTeamId, item.TypeId }, WorkItemsConstraints.OwningTeamType);
        // mod-017 (ADR-0028 rule 5): the target of the team place's key (work_item_id, team_id) -> (id, owning_team_id),
        // which the second migration adds in SQL: EF cannot declare an alternate key over a nullable column.
        builder.HasIndex(item => new { item.Id, item.OwningTeamId }, WorkItemsConstraints.IdOwningTeam).IsUnique();
        // mod-005 (plan-backend D-P6): a partial index over the few deleted rows, declared so EF adds no conventional full
        // index for the new key.
        builder.HasIndex(item => item.DeletedFromStateId, WorkItemsConstraints.DeletedFromStateIndex)
            .HasFilter("deleted_from_state_id IS NOT NULL");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(WorkItemsConstraints.NumberPositive, "number >= 1");
            table.HasCheckConstraint(WorkItemsConstraints.ParentNotSelf, "parent_id IS NULL OR parent_id <> id");
            table.HasCheckConstraint(WorkItemsConstraints.DeletionMarksCheck, WorkItemsConstraints.DeletionMarksSql);
            table.HasCheckConstraint("ck_work_items_title_length", WorkItemsConstraints.TitleLengthCheck);
            table.HasCheckConstraint("ck_work_items_description_length", WorkItemsConstraints.DescriptionLengthCheck);

            // feat-022: the ranges of its api-contract § 1, as the database's backstop to the domain's checks.
            foreach (var (name, sql) in WorkItemsConstraints.FieldChecks)
            {
                table.HasCheckConstraint(name, sql);
            }
        });
    }
}
