using Mustask.Domain.Process;

namespace Mustask.Domain.Imports.DryRun;

public sealed record DryRunProjectRef(Guid Id, string Name, bool Active);

public sealed record DryRunStateRef(Guid Id, string Name, bool Removed);

public sealed record DryRunTypeRef(Guid Id, string Name, AppearanceToken Kind, IReadOnlyList<DryRunStateRef> States);

public sealed record DryRunMemberRef(string Id, string Name, bool Active);

/// <summary>An existing item as the dry run compares it: <see cref="Values"/> are canonical texts keyed by field id (see <see cref="DryRunEngine"/>).</summary>
public sealed record DryRunItemRef(Guid Id, string Identifier, Guid ProjectId, Guid TypeId, bool Deleted, IReadOnlyDictionary<string, string?> Values);

/// <summary>
/// What a dry run knows about the organization (BR-IE-12), read once: the active projects, the process's types and states, the members, the
/// teams and their links to projects, the items the file's identifiers name, and the importer's reach per project. Nothing here is written.
/// </summary>
public sealed class DryRunContext
{
    public required IReadOnlyDictionary<Guid, DryRunProjectRef> Projects { get; init; }

    public required IReadOnlyDictionary<Guid, DryRunTypeRef> Types { get; init; }

    public required IReadOnlyDictionary<string, DryRunMemberRef> Members { get; init; }

    public required IReadOnlyDictionary<Guid, string> Teams { get; init; }

    public required IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> ProjectTeams { get; init; }

    /// <summary>Items by <see cref="DryRunEngine.NormalizeIdentifier"/>.</summary>
    public required IReadOnlyDictionary<string, DryRunItemRef> Items { get; init; }

    /// <summary>Whether the importer holds <c>imports.import.run</c> and <c>workitems.workitem.write</c> on the project.</summary>
    public required Func<Guid, bool> CanImportInto { get; init; }

    /// <summary>Whether the importer may read the project's items.</summary>
    public required Func<Guid, bool> CanRead { get; init; }
}

/// <summary>What the organization holds that a template's targets and a file's identifiers are checked against (read, never written).</summary>
public sealed record ImportReferences(
    IReadOnlyDictionary<Guid, DryRunProjectRef> Projects,
    IReadOnlyDictionary<Guid, DryRunTypeRef> Types,
    IReadOnlyDictionary<string, DryRunMemberRef> Members,
    IReadOnlyDictionary<Guid, string> Teams,
    IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> ProjectTeams,
    IReadOnlyDictionary<string, DryRunItemRef> Items)
{
    /// <summary>The dry run's context over these references and the importer's reach.</summary>
    public DryRunContext ToContext(Func<Guid, bool> canImportInto, Func<Guid, bool> canRead) => new()
    {
        Projects = Projects,
        Types = Types,
        Members = Members,
        Teams = Teams,
        ProjectTeams = ProjectTeams,
        Items = Items,
        CanImportInto = canImportInto,
        CanRead = canRead,
    };
}
