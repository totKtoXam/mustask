using Mustask.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Mustask.Infrastructure.Tests.Conventions;

/// <summary>
/// mod-005 (ADR-0030 rule 7, BR-DR-16): data added later that is keyed to a work item must leave with its purge, and a line
/// that adds it must meet a failing build instead of a convention. A model test over the real <see cref="MustaskDbContext"/>
/// in the family of <see cref="CodeUniqueIndexModelTests"/> — it needs no database (every table is an EF entity, ADR-0009,
/// so a new table cannot hide from the model). It enumerates every foreign key whose principal is <see cref="WorkItem"/> and
/// every property named <c>WorkItemId</c>, or ending in it, that has no such key. Each must be exactly one of:
/// <list type="bullet">
/// <item><b>Cascade</b> — the key deletes with the item (the default, ADR-0030 rule 7 (3));</item>
/// <item><b>ClearedByPurge</b> — the purge statement clears it (today <c>WorkItem.ParentId</c> alone, whose key is
/// <c>Restrict</c>, so a forgotten clear breaks the delete);</item>
/// <item><b>Kept</b> — the data stays by design, listed here with its reason (none today).</item>
/// </list>
/// A reference to a line's step (<c>[PurgeStepCovers]</c>) is the fourth, added with the step contract.
/// </summary>
/// <remarks>
/// The limit is stated: the model does not see <c>fk_backlog_places_work_items_owner</c> (SQL-only, <c>NO ACTION</c>; EF
/// cannot declare it, mod-017) — the purge of a team-placed item pins it — nor a reference inside a JSON or text column or a
/// store outside the database; those are seen only through a step that declares them.
/// </remarks>
public sealed class WorkItemReferenceInventoryTests
{
    private const string ConnectionStringEnvVar = "ConnectionStrings__mustask";

    /// <summary>The references the purge statement clears itself, by <c>Entity.Property</c>. Today: a child's link to its parent.</summary>
    private static readonly HashSet<string> ClearedByPurge = ["WorkItem.ParentId"];

    /// <summary>The references that stay after a purge by design, with the reason. None today.</summary>
    private static readonly Dictionary<string, string> Kept = [];

    // BR-DR-16 · "Data added later cannot be forgotten": every reference of the model is classified, and none is stale.
    [Fact]
    public void EveryReferenceToAWorkItem_IsClassified_AndNoListedEntryIsStale()
    {
        var references = ReferencesOfTheModel();

        var failures = Evaluate(references, ClearedByPurge, Kept);

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    // The guard against passing vacuously: today's three references must be found.
    [Fact]
    public void TheInventory_FindsTodaysThreeReferences()
    {
        var found = ReferencesOfTheModel().Select(reference => reference.Name).Order().ToList();

        Assert.Equal(["BacklogPlace.WorkItemId", "ProjectBacklogPlace.WorkItemId", "WorkItem.ParentId"], found);
        Assert.Equal(
            DeleteBehavior.Cascade,
            ReferencesOfTheModel().Single(reference => reference.Name == "BacklogPlace.WorkItemId").Behavior);
        Assert.Equal(
            DeleteBehavior.Restrict,
            ReferencesOfTheModel().Single(reference => reference.Name == "WorkItem.ParentId").Behavior);
    }

    // The check itself, on planted data: an entity with a WorkItemId and no key, and one with a Restrict key, are failures.
    [Fact]
    public void TheCheck_FailsOnAnUnlistedReference_AStaleEntry_AndARestrictKeyThatNobodyClears()
    {
        var today = ReferencesOfTheModel();

        // A new table that carries a WorkItemId and no key at all: not listed anywhere.
        var withoutKey = today.Append(new Reference("Comment", "WorkItemId", HasKey: false, Behavior: null)).ToList();
        Assert.Contains(Evaluate(withoutKey, ClearedByPurge, Kept), failure => failure.Contains("Comment.WorkItemId", StringComparison.Ordinal));

        // A new table with a key left at Restrict: the purge would fail loudly, so the check fails first.
        var restrict = today.Append(new Reference("Attachment", "WorkItemId", HasKey: true, Behavior: DeleteBehavior.Restrict)).ToList();
        Assert.Contains(Evaluate(restrict, ClearedByPurge, Kept), failure => failure.Contains("Attachment.WorkItemId", StringComparison.Ordinal));

        // A new table with a cascading key passes.
        var cascading = today.Append(new Reference("Comment", "WorkItemId", HasKey: true, Behavior: DeleteBehavior.Cascade)).ToList();
        Assert.Empty(Evaluate(cascading, ClearedByPurge, Kept));

        // A listed entry that names nothing in the model is stale, whether it claims to be cleared or kept.
        var staleCleared = new HashSet<string>(ClearedByPurge) { "Comment.WorkItemId" };
        var staleKept = new Dictionary<string, string>(Kept) { ["Tag.WorkItemId"] = "a reason" };
        Assert.Contains(Evaluate(today, staleCleared, Kept), failure => failure.Contains("Comment.WorkItemId", StringComparison.Ordinal));
        Assert.Contains(Evaluate(today, ClearedByPurge, staleKept), failure => failure.Contains("Tag.WorkItemId", StringComparison.Ordinal));

        // A reference that is both cleared and kept is not "exactly one".
        var both = new Dictionary<string, string>(Kept) { ["WorkItem.ParentId"] = "a reason" };
        Assert.Contains(Evaluate(today, ClearedByPurge, both), failure => failure.Contains("WorkItem.ParentId", StringComparison.Ordinal));

        // A kept reference passes when it has a reason, and fails without one.
        var keptReference = today.Append(new Reference("History", "WorkItemId", HasKey: false, Behavior: null)).ToList();
        Assert.Empty(Evaluate(keptReference, ClearedByPurge, new Dictionary<string, string>(Kept) { ["History.WorkItemId"] = "an audit record" }));
        Assert.Contains(
            Evaluate(keptReference, ClearedByPurge, new Dictionary<string, string>(Kept) { ["History.WorkItemId"] = " " }),
            failure => failure.Contains("History.WorkItemId", StringComparison.Ordinal));
    }

    // --- The check --------------------------------------------------------------------------------------------------

    /// <param name="Entity">The CLR name of the referencing entity.</param>
    /// <param name="Property">The property that names the item; the last column of a composite key.</param>
    /// <param name="HasKey">Whether a foreign key onto <see cref="WorkItem"/> exists for it.</param>
    /// <param name="Behavior">The key's delete behavior; <c>null</c> without a key.</param>
    private sealed record Reference(string Entity, string Property, bool HasKey, DeleteBehavior? Behavior)
    {
        public string Name => $"{Entity}.{Property}";
    }

    private static List<string> Evaluate(
        IReadOnlyCollection<Reference> references, IReadOnlySet<string> clearedByPurge, IReadOnlyDictionary<string, string> kept)
    {
        var failures = new List<string>();
        var names = references.Select(reference => reference.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var entry in clearedByPurge.Concat(kept.Keys).Where(entry => !names.Contains(entry)).Distinct())
        {
            failures.Add($"{entry}: listed as cleared by the purge or kept, but the model has no such reference — remove the stale entry.");
        }

        foreach (var entry in kept.Where(entry => string.IsNullOrWhiteSpace(entry.Value)))
        {
            failures.Add($"{entry.Key}: a kept reference needs its reason.");
        }

        foreach (var reference in references)
        {
            var cascades = reference.HasKey && reference.Behavior == DeleteBehavior.Cascade;
            var cleared = clearedByPurge.Contains(reference.Name);
            var isKept = kept.ContainsKey(reference.Name);
            var classes = new[] { cascades, cleared, isKept }.Count(flag => flag);

            if (classes == 0)
            {
                failures.Add(
                    reference.HasKey
                        ? $"{reference.Name}: a foreign key onto work_items that is {reference.Behavior} — the purge would fail. Make it Cascade, "
                          + "clear it in the purge, cover it with a purge step, or list it as kept with a reason (ADR-0030 rule 7)."
                        : $"{reference.Name}: a work item id with no foreign key — it would outlive the purge. Give it a cascading key, "
                          + "cover it with a purge step, or list it as kept with a reason (ADR-0030 rule 7).");
            }
            else if (classes > 1)
            {
                failures.Add($"{reference.Name}: classified more than once — it is exactly one of cascade, cleared by the purge, kept.");
            }
        }

        return failures;
    }

    // --- The model --------------------------------------------------------------------------------------------------

    private static List<Reference> ReferencesOfTheModel()
    {
        Environment.SetEnvironmentVariable(ConnectionStringEnvVar, "Host=localhost;Database=mustask;Username=u;Password=p");

        try
        {
            using var context = new MustaskDbContextFactory().CreateDbContext([]);

            var references = new List<Reference>();
            var keyed = new HashSet<(IReadOnlyEntityType Entity, string Property)>();

            foreach (var entity in context.Model.GetEntityTypes())
            {
                foreach (var key in entity.GetForeignKeys().Where(key => key.PrincipalEntityType.ClrType == typeof(WorkItem)))
                {
                    var property = key.Properties[^1].Name;

                    keyed.Add((entity, property));
                    references.Add(new Reference(entity.ClrType.Name, property, HasKey: true, key.DeleteBehavior));
                }
            }

            foreach (var entity in context.Model.GetEntityTypes())
            {
                foreach (var property in entity.GetProperties().Where(property =>
                             property.Name.EndsWith("WorkItemId", StringComparison.Ordinal) && !keyed.Contains((entity, property.Name))))
                {
                    references.Add(new Reference(entity.ClrType.Name, property.Name, HasKey: false, Behavior: null));
                }
            }

            return references;
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, null);
        }
    }
}
