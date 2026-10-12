using System.Text.RegularExpressions;

namespace Mustask.Architecture.Tests;

/// <summary>
/// bug-004 D-3, review round 1 (R1-4): a state name has one category on every type of a process, and the code that
/// brings a state into a process asks <c>StateNameRule</c> first (<c>ProcessDefinition.Seed</c> today). The domain test
/// <c>ProcessWritePathTests</c> pins the aggregate's public surface only; a write that goes round the aggregate —
/// raw SQL, <c>ExecuteUpdate</c>, a tracked write on the set — would pass it silently, and raw-SQL writes are already a
/// pattern here (<c>BacklogRepository</c>). This rule closes that gap at the level of source text: outside the process
/// sub-slice, nothing names the table <c>state_definitions</c> or writes to the <c>StateDefinitions</c> set. Reading the
/// set (a join, a <c>Where</c>) stays free; the work item and board read models do exactly that.
/// </summary>
/// <remarks>
/// A text rule, not a proof. It sees the table by name and the set's write calls by shape; a write through
/// <c>Entry(...)</c> on a tracked state, or through a helper that takes the set as an argument, is not seen — the
/// state's setters are private, which is the first line, and this is the second.
/// </remarks>
public sealed class ProcessStateWritesRules
{
    private static readonly string[] Layers = ["Mustask.Domain", "Mustask.Application", "Mustask.Infrastructure", "Mustask.Api"];

    /// <summary>The aggregate, the process sub-slice of the storage (the seed's store, the configurations), the context and the migrations.</summary>
    private static readonly string[] AllowedIn =
    [
        "Mustask.Domain/Process/",
        "Mustask.Infrastructure/Process/",
        "Mustask.Infrastructure/MustaskDbContext.cs",
        "Mustask.Infrastructure/Migrations/",
    ];

    private const string TheSet = @"(?:\bStateDefinitions|\bSet<StateDefinition>\s*\(\s*\))";

    private static readonly (string Name, Regex Pattern)[] Patterns =
    [
        ("state_definitions", new Regex(@"\bstate_definitions\b", RegexOptions.CultureInvariant)),
        ("tracked write", new Regex(TheSet + @"\s*\.\s*(?:Add|Remove|Update|Attach)\w*\s*\(", RegexOptions.CultureInvariant)),
        ("bulk update or delete", new Regex(TheSet + @"[^;]*?\.\s*Execute(?:Update|Delete)\w*\s*\(", RegexOptions.CultureInvariant)),
    ];

    [Fact]
    public void States_are_written_only_by_the_process_sub_slice()
    {
        var violations = Violations(Layers.SelectMany(RepositoryLayout.SourceFilesOf).Select(file => (RelativePath(file), CodeOf(File.ReadLines(file.FullName)))));

        Assert.True(
            violations.Count == 0,
            "Code outside the process sub-slice names the table `state_definitions` or writes to the `StateDefinitions` set — "
            + "a state is created by `ProcessDefinition.Seed` only, and a path that creates or renames one must ask "
            + "`StateNameRule` first (bug-004 D-3):"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    /// <summary>The rule cannot pass vacuously: the table and the set are named where they are allowed and read elsewhere, and a write elsewhere is flagged.</summary>
    [Fact]
    public void State_writes_rule_is_not_vacuous()
    {
        var sources = Layers
            .SelectMany(RepositoryLayout.SourceFilesOf)
            .Select(file => (Path: RelativePath(file), Code: CodeOf(File.ReadLines(file.FullName))))
            .ToList();

        Assert.Contains(sources, file => IsAllowed(file.Path) && Patterns[0].Pattern.IsMatch(file.Code));
        Assert.Contains(
            sources,
            file => !IsAllowed(file.Path) && file.Code.Contains("StateDefinitions", StringComparison.Ordinal));

        var flagged = Violations(
        [
            ("Mustask.Infrastructure/Boards/Raw.cs", "await context.Database.ExecuteSqlRawAsync(\"UPDATE state_definitions SET name = 'x'\");"),
            ("Mustask.Infrastructure/Organizations/Bulk.cs", "await context.StateDefinitions\n    .Where(state => state.Name == \"a\")\n    .ExecuteUpdateAsync(state => state.SetProperty(s => s.Category, StateCategory.Resolved));"),
            ("Mustask.Application/Features/Process/Rename/RenameHandler.cs", "context.Set<StateDefinition>().Remove(state);"),
            ("Mustask.Api/Features/Process/Endpoints.cs", "db.StateDefinitions.AddRange(states);"),
            ("Mustask.Infrastructure/WorkItems/Read.cs", "join state in context.StateDefinitions on item.StateId equals state.Id"),
            ("Mustask.Infrastructure/WorkItems/Refs.cs", "context.StateDefinitions.Where(state => state.Category == StateCategory.Removed).Select(state => state.Id);"),
            ("Mustask.Infrastructure/Process/ProcessSeedStore.cs", "context.StateDefinitions.AddRange(states);"),
            ("Mustask.Infrastructure/Organizations/Comment.cs", CodeOf(["// UPDATE state_definitions", "/// context.StateDefinitions.Add(state)"])),
        ]);

        Assert.Equal(
            [
                "Mustask.Infrastructure/Boards/Raw.cs: state_definitions",
                "Mustask.Infrastructure/Organizations/Bulk.cs: bulk update or delete",
                "Mustask.Application/Features/Process/Rename/RenameHandler.cs: tracked write",
                "Mustask.Api/Features/Process/Endpoints.cs: tracked write",
            ],
            flagged);
    }

    private static bool IsAllowed(string path) => AllowedIn.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal));

    private static List<string> Violations(IEnumerable<(string Path, string Code)> files) =>
        files
            .Where(file => !IsAllowed(file.Path))
            .SelectMany(file => Patterns
                .Where(pattern => pattern.Pattern.IsMatch(file.Code))
                .Select(pattern => $"{file.Path}: {pattern.Name}"))
            .ToList();

    /// <summary>The file's lines without the comment lines, joined: a statement that spans lines stays one piece of text.</summary>
    private static string CodeOf(IEnumerable<string> lines) =>
        string.Join('\n', lines.Where(line => !IsComment(line)));

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
