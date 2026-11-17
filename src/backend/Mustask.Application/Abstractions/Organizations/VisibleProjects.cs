namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// The projects of the organization the caller sees (doc-016 BR-TD-12, BR-TD-17), as a read of several projects' rows takes them:
/// every project for the administrator (<see cref="All"/>, no predicate), else the ids <see cref="AccessSet.VisibleProjects"/>
/// computed, from the one set form of the sight rule (feat-041). A read narrows by it <b>in the statement</b>, before any paging,
/// so no count, total, page, cursor or «ещё» is ever computed over a hidden project (feat-062).
/// </summary>
public sealed class VisibleProjects
{
    private readonly HashSet<Guid>? _ids;

    private VisibleProjects(HashSet<Guid>? ids) => _ids = ids;

    /// <summary>Every project of the organization: the administrator's sight.</summary>
    public static VisibleProjects All { get; } = new(null);

    /// <summary>Exactly the projects <paramref name="ids"/>.</summary>
    public static VisibleProjects Only(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        return new VisibleProjects(ids.ToHashSet());
    }

    /// <summary>Whether no project is left out.</summary>
    public bool IsAll => _ids is null;

    /// <summary>The visible ids, <c>null</c> for <see cref="All"/>.</summary>
    public IReadOnlyCollection<Guid>? Ids => _ids;

    /// <summary>Whether the project is seen.</summary>
    public bool Sees(Guid projectId) => _ids is null || _ids.Contains(projectId);
}
