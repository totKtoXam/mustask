using System.Text.RegularExpressions;

namespace Mustask.Architecture.Tests;

/// <summary>
/// mod-005 (ADR-0030 rule 2, plan-backend § 6.4): a work item is deleted by its state, and a read that forgets to say which
/// items it wants leaks one. Two rules at the level of source text keep the common shape from compiling:
/// <list type="number">
/// <item><b>Reads.</b> In <c>Mustask.Infrastructure</c>, the context's <c>WorkItems</c> set and <c>Set&lt;WorkItem&gt;()</c>
/// are named only in <c>WorkItemSets.cs</c> — <c>Live</c>, <c>Deleted</c>, <c>InAnyState</c> — and in the two backlog
/// memberships, which keep their own single place and are pinned by the parity test. Any other read starts from an entry
/// point, so it states its intent by name.</item>
/// <item><b>Writes.</b> Outside <c>Mustask.Infrastructure/WorkItems/</c> and the migrations, nothing writes <c>work_items</c> by
/// raw SQL, by <c>ExecuteUpdate</c> or <c>ExecuteDelete</c> over a work-item query, or by a <c>Remove</c> straight on the
/// context. Inside it, a raw <c>UPDATE work_items</c> names neither <c>state_id</c> nor <c>deleted_</c>: only the aggregate
/// writes the state and the marks of a deletion. Today's raw writers are the team release and the purge's
/// <c>parent_id = NULL</c>.</item>
/// </list>
/// </summary>
/// <remarks>
/// A text rule, not a proof. It sees the set by its name and the statements by their text; a read through a helper that
/// takes the set as an argument is not seen. <c>DeletedStateParityTests</c> and <c>DeletedItemSurfaceMatrixTests</c> are the
/// proof, this is the second line — in the family of <see cref="ProcessStateWritesRules"/>.
/// </remarks>
public sealed class WorkItemAccessRules
{
    private static readonly string[] Layers = ["Mustask.Domain", "Mustask.Application", "Mustask.Infrastructure", "Mustask.Api"];

    /// <summary>The one place the set is named, the two memberships (each with its reason), the context and the migrations.</summary>
    private static readonly string[] ReadsAllowedIn =
    [
        "Mustask.Infrastructure/WorkItems/WorkItemSets.cs",

        // The memberships take the scope's shown-state ids (the backlog level's non-Removed states): their own single place of
        // "what a backlog holds", pinned equal to the entry points by DeletedStateParityTests.
        "Mustask.Infrastructure/Boards/BacklogMembership.cs",
        "Mustask.Infrastructure/Boards/ProjectBacklogMembership.cs",

        "Mustask.Infrastructure/MustaskDbContext.cs",
        "Mustask.Infrastructure/Migrations/",
    ];

    private static readonly string[] WritesAllowedIn =
    [
        "Mustask.Infrastructure/WorkItems/",
        "Mustask.Infrastructure/Migrations/",
    ];

    private static readonly (string Name, Regex Pattern)[] ReadPatterns =
    [
        ("the context's WorkItems set", new Regex(@"\b\w*[cC]ontext\s*\.\s*WorkItems\b", RegexOptions.CultureInvariant)),
        ("Set<WorkItem>()", new Regex(@"\bSet<WorkItem>\s*\(", RegexOptions.CultureInvariant)),
    ];

    /// <summary>A query over work items: an entry point of <c>WorkItemSets</c>, the context's set, or <c>Set&lt;WorkItem&gt;()</c>.</summary>
    private const string TheItems =
        @"(?:\bWorkItemSets\s*\.\s*(?:Live|Deleted|InAnyState)\s*\(|\b\w*[cC]ontext\s*\.\s*WorkItems\b|\bSet<WorkItem>\s*\(\s*\))";

    private static readonly (string Name, Regex Pattern)[] WritePatterns =
    [
        ("UPDATE work_items", new Regex(@"\bUPDATE\s+work_items\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)),
        ("DELETE FROM work_items", new Regex(@"\bDELETE\s+FROM\s+work_items\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)),
        ("INSERT INTO work_items", new Regex(@"\bINSERT\s+INTO\s+work_items\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)),

        // ADR-0030 rule 2 names these two shapes as well (mod-005 code review CR-4). The first stays inside one statement.
        // The second cannot tell by text which entity is removed: a Remove straight on the context passes the entity as an
        // argument, so it is flagged for every entity, and the other slices remove through their own set (`context.Areas.Remove`).
        ("bulk update or delete of work items", new Regex(TheItems + @"[^;]*?\.\s*Execute(?:Update|Delete)\w*\s*\(", RegexOptions.CultureInvariant)),
        ("tracked remove through the context", new Regex(@"\b\w*[cC]ontext\s*\.\s*Remove(?:Range)?\s*\(", RegexOptions.CultureInvariant)),
    ];

    /// <summary>A raw <c>UPDATE work_items</c> statement, up to the quote that closes it.</summary>
    private static readonly Regex UpdateStatement = new(
        @"\bUPDATE\s+work_items\b[^""]*", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ForbiddenColumns = new(@"\b(?:state_id|deleted_\w*)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    [Fact]
    public void Work_item_reads_start_from_a_named_entry_point()
    {
        var violations = ReadViolations(Infrastructure().Select(file => (RelativePath(file), CodeOf(File.ReadLines(file.FullName)))));

        Assert.True(
            violations.Count == 0,
            "Code in Mustask.Infrastructure names the context's WorkItems set outside WorkItemSets.cs — a read of work items "
            + "starts from WorkItemSets.Live, Deleted or InAnyState, so a deleted item is hidden or shown by intent (mod-005, ADR-0030 rule 2):"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Work_items_are_written_only_by_the_work_items_sub_slice_and_the_aggregate_owns_state_and_marks()
    {
        var violations = WriteViolations(Layers.SelectMany(RepositoryLayout.SourceFilesOf).Select(file => (RelativePath(file), CodeOf(File.ReadLines(file.FullName)))));

        Assert.True(
            violations.Count == 0,
            "Code outside Mustask.Infrastructure/WorkItems/ writes `work_items` by raw SQL, by ExecuteUpdate/ExecuteDelete over a "
            + "work-item query, or by a Remove straight on the context (it cannot be told which entity that removes; remove through the "
            + "entity's own set), or a raw UPDATE of `work_items` names `state_id` or a `deleted_` mark — only the aggregate writes them, "
            + "through Delete and Restore (mod-005, ADR-0030 rule 2):"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    /// <summary>The rules cannot pass vacuously: the set is named where it is allowed, and every shape of a violation is flagged.</summary>
    [Fact]
    public void Work_item_access_rules_are_not_vacuous()
    {
        var sources = Layers
            .SelectMany(RepositoryLayout.SourceFilesOf)
            .Select(file => (Path: RelativePath(file), Code: CodeOf(File.ReadLines(file.FullName))))
            .ToList();

        // The reads rule: the allowed places do name the set, and elsewhere in Infrastructure the entry points are used.
        Assert.Contains(sources, file => file.Path == "Mustask.Infrastructure/WorkItems/WorkItemSets.cs" && ReadPatterns[0].Pattern.IsMatch(file.Code));
        Assert.Contains(sources, file => file.Path == "Mustask.Infrastructure/Boards/BacklogMembership.cs" && ReadPatterns[0].Pattern.IsMatch(file.Code));
        Assert.Contains(sources, file => file.Path == "Mustask.Infrastructure/MustaskDbContext.cs" && file.Code.Contains("DbSet<WorkItem>", StringComparison.Ordinal));
        Assert.Contains(
            sources,
            file => file.Path.StartsWith("Mustask.Infrastructure/WorkItems/", StringComparison.Ordinal)
                && !ReadsAllowedIn.Contains(file.Path)
                && file.Code.Contains("WorkItemSets.", StringComparison.Ordinal));

        var flaggedReads = ReadViolations(
        [
            ("Mustask.Infrastructure/Boards/Read.cs", "from item in context.WorkItems.AsNoTracking()"),
            ("Mustask.Infrastructure/Organizations/Spaced.cs", "await dbContext\n    .WorkItems\n    .AnyAsync(item => item.Id == id);"),
            ("Mustask.Infrastructure/WorkItems/Other.cs", "return _context.Set<WorkItem>()\n    .Count();"),
            ("Mustask.Infrastructure/WorkItems/Add.cs", "public void Add(WorkItem item) => context.WorkItems.Add(item);"),
            ("Mustask.Infrastructure/WorkItems/Fine.cs", "WorkItemSets.Live(context).Where(item => item.ProjectId == projectId); context.WorkItemTypes.Where(t => true);"),
            ("Mustask.Infrastructure/WorkItems/Quoted.cs", CodeOf(["// context.WorkItems.Add(item)", "/// <c>context.WorkItems</c>", "using Mustask.Application.Features.WorkItems;"])),
            ("Mustask.Infrastructure/WorkItems/WorkItemSets.cs", "context.WorkItems.Where(item => true)"),
            ("Mustask.Infrastructure/Boards/ProjectBacklogMembership.cs", "context.WorkItems.AsNoTracking()"),
        ]);

        Assert.Equal(
            [
                "Mustask.Infrastructure/Boards/Read.cs: the context's WorkItems set",
                "Mustask.Infrastructure/Organizations/Spaced.cs: the context's WorkItems set",
                "Mustask.Infrastructure/WorkItems/Other.cs: Set<WorkItem>()",
                "Mustask.Infrastructure/WorkItems/Add.cs: the context's WorkItems set",
            ],
            flaggedReads);

        // The writes rule: the team release is allowed in the sub-slice, and every shape of a write elsewhere is flagged.
        Assert.Contains(sources, file => file.Path == "Mustask.Infrastructure/WorkItems/WorkItemTeamOwnership.cs" && WritePatterns[0].Pattern.IsMatch(file.Code));

        var flaggedWrites = WriteViolations(
        [
            ("Mustask.Infrastructure/Boards/Raw.cs", "await context.Database.ExecuteSqlRawAsync(\"UPDATE work_items SET title = 'x'\");"),
            ("Mustask.Infrastructure/Organizations/Raw.cs", "\"DELETE FROM work_items WHERE project_id = {p}\""),
            ("Mustask.Application/Features/WorkItems/Raw.cs", "\"insert into work_items (id) values (1)\""),
            ("Mustask.Infrastructure/WorkItems/Release.cs", "\"UPDATE work_items SET owning_team_id = NULL, changed_at = {now} WHERE project_id = {p}\""),
            ("Mustask.Infrastructure/WorkItems/Unlink.cs", "\"UPDATE work_items SET parent_id = NULL, changed_at = {now} WHERE project_id = {p} AND parent_id = {id}\""),
            ("Mustask.Infrastructure/WorkItems/State.cs", "\"UPDATE work_items SET state_id = {s} WHERE id = {id}\""),
            ("Mustask.Infrastructure/WorkItems/Mark.cs", "\"UPDATE work_items SET changed_at = {now}, deleted_by = NULL WHERE id = {id}\""),
            ("Mustask.Infrastructure/WorkItems/Multi.cs", "$\"\"\"\n    UPDATE work_items\n    SET state_id = {s}\n    WHERE id = {id}\n    \"\"\""),
            ("Mustask.Infrastructure/Migrations/20261018_Move.cs", "\"UPDATE work_items SET state_id = x\""),
            ("Mustask.Infrastructure/Organizations/Comment.cs", CodeOf(["// UPDATE work_items SET x", "/// DELETE FROM work_items"])),

            // CR-4: the shapes the raw-SQL patterns do not see.
            ("Mustask.Infrastructure/Boards/Bulk.cs", "await WorkItemSets.Live(context)\n    .Where(item => item.ProjectId == projectId)\n    .ExecuteUpdateAsync(item => item.SetProperty(i => i.Title, \"x\"));"),
            ("Mustask.Infrastructure/Organizations/BulkDelete.cs", "await WorkItemSets.InAnyState(dbContext).Where(item => item.AreaId == areaId).ExecuteDeleteAsync(cancellationToken);"),
            ("Mustask.Infrastructure/Organizations/BulkSet.cs", "await context.WorkItems.ExecuteDeleteAsync(ct);"),
            ("Mustask.Infrastructure/Boards/BulkGeneric.cs", "_context.Set<WorkItem>().Where(item => true).ExecuteUpdate(item => item);"),
            ("Mustask.Infrastructure/Boards/Gone.cs", "context.Remove(item);"),
            ("Mustask.Infrastructure/Boards/GoneMany.cs", "dbContext.RemoveRange(items);"),

            // Not flagged: another entity's bulk write and its own set's Remove, a read followed by another statement's bulk write,
            // and the sub-slice that owns the writes.
            ("Mustask.Infrastructure/Organizations/Teams.cs", "await context.Teams.Where(team => true).ExecuteDeleteAsync(ct);\ncontext.Areas.Remove(area);\ncache.Remove(key);"),
            ("Mustask.Infrastructure/Organizations/ReadThenWrite.cs", "var rows = await WorkItemSets.Live(context).ToListAsync(ct);\nawait context.Teams.ExecuteDeleteAsync(ct);"),
            ("Mustask.Infrastructure/WorkItems/WorkItemRepository.cs", "context.Remove(item);\nawait WorkItemSets.Live(context).Where(item => true).ExecuteDeleteAsync(ct);"),
        ]);

        Assert.Equal(
            [
                "Mustask.Infrastructure/Boards/Raw.cs: UPDATE work_items",
                "Mustask.Infrastructure/Organizations/Raw.cs: DELETE FROM work_items",
                "Mustask.Application/Features/WorkItems/Raw.cs: INSERT INTO work_items",
                "Mustask.Infrastructure/WorkItems/State.cs: UPDATE work_items names state_id or a deleted_ mark",
                "Mustask.Infrastructure/WorkItems/Mark.cs: UPDATE work_items names state_id or a deleted_ mark",
                "Mustask.Infrastructure/WorkItems/Multi.cs: UPDATE work_items names state_id or a deleted_ mark",
                "Mustask.Infrastructure/Boards/Bulk.cs: bulk update or delete of work items",
                "Mustask.Infrastructure/Organizations/BulkDelete.cs: bulk update or delete of work items",
                "Mustask.Infrastructure/Organizations/BulkSet.cs: bulk update or delete of work items",
                "Mustask.Infrastructure/Boards/BulkGeneric.cs: bulk update or delete of work items",
                "Mustask.Infrastructure/Boards/Gone.cs: tracked remove through the context",
                "Mustask.Infrastructure/Boards/GoneMany.cs: tracked remove through the context",
            ],
            flaggedWrites);
    }

    private static IEnumerable<FileInfo> Infrastructure() => RepositoryLayout.SourceFilesOf("Mustask.Infrastructure");

    private static List<string> ReadViolations(IEnumerable<(string Path, string Code)> files) =>
        files
            .Where(file => !ReadsAllowedIn.Any(prefix => file.Path.StartsWith(prefix, StringComparison.Ordinal)))
            .SelectMany(file => ReadPatterns
                .Where(pattern => pattern.Pattern.IsMatch(file.Code))
                .Select(pattern => $"{file.Path}: {pattern.Name}"))
            .ToList();

    private static List<string> WriteViolations(IEnumerable<(string Path, string Code)> files)
    {
        var violations = new List<string>();

        foreach (var file in files)
        {
            var inside = WritesAllowedIn.Any(prefix => file.Path.StartsWith(prefix, StringComparison.Ordinal));

            if (!inside)
            {
                violations.AddRange(WritePatterns
                    .Where(pattern => pattern.Pattern.IsMatch(file.Code))
                    .Select(pattern => $"{file.Path}: {pattern.Name}"));

                continue;
            }

            // Inside the sub-slice a raw UPDATE may move a reference and "changed_at", never the state or a mark of a deletion.
            if (!file.Path.StartsWith("Mustask.Infrastructure/Migrations/", StringComparison.Ordinal)
                && UpdateStatement.Matches(file.Code).Any(statement => ForbiddenColumns.IsMatch(statement.Value)))
            {
                violations.Add($"{file.Path}: UPDATE work_items names state_id or a deleted_ mark");
            }
        }

        return violations;
    }

    /// <summary>
    /// The file's lines without the comment lines and without <c>using</c> and <c>namespace</c> lines — a namespace is
    /// <c>…Features.WorkItems</c> — joined: a statement that spans lines stays one piece of text.
    /// </summary>
    private static string CodeOf(IEnumerable<string> lines) =>
        string.Join('\n', lines.Where(line => !IsComment(line) && !IsDirective(line)));

    private static bool IsDirective(string line)
    {
        var trimmed = line.TrimStart();

        return trimmed.StartsWith("using ", StringComparison.Ordinal) || trimmed.StartsWith("namespace ", StringComparison.Ordinal);
    }

    private static bool IsComment(string line)
    {
        var trimmed = line.TrimStart();

        return trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith("/*", StringComparison.Ordinal)
            || trimmed.StartsWith('*');
    }

    private static string RelativePath(FileInfo file) =>
        Path.GetRelativePath(RepositoryLayout.Source.FullName, file.FullName).Replace('\\', '/');
}
