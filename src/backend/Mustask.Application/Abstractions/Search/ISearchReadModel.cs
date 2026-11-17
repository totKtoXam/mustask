using Mustask.Domain.Search;

namespace Mustask.Application.Abstractions.Search;

/// <summary>
/// What the caller sees in one organization, as the id arrays the read filters by (feat-071 plan § 2.2). <c>null</c> for a kind is
/// "no restriction" (the organization administrator): no ids are read or passed for it.
/// </summary>
public sealed record SearchVisibility(
    IReadOnlyList<Guid>? Projects,
    IReadOnlyList<Guid>? Teams,
    IReadOnlyList<Guid>? Departments,
    IReadOnlyList<Guid>? Areas)
{
    public static SearchVisibility Unrestricted { get; } = new(null, null, null, null);
}

/// <summary>One organization the read covers. The read takes a list of them so the all-organizations line adds no second read.</summary>
public sealed record SearchScope(Guid OrganizationId, bool UsersAllowed, SearchVisibility Visibility);

/// <summary>The ids of the organization's active objects per kind: the candidates the sight set is applied to in memory.</summary>
public sealed record SearchCandidates(
    IReadOnlyList<Guid> Projects,
    IReadOnlyList<Guid> Teams,
    IReadOnlyList<Guid> Departments,
    IReadOnlyList<Guid> Areas);

/// <summary>One matched row as the read returns it; the fields a kind does not have are <c>null</c>.</summary>
public sealed record SearchRow(
    SearchGroupKind Kind,
    string Id,
    string Name,
    string? Key,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? ProjectId,
    string? ProjectName,
    string? Color,
    string? Email,
    bool? Inactive,
    int Tier);

/// <summary>The rows of one group: ordered by tier, then the group's tie-break, at most <c>take</c> of them.</summary>
public sealed record SearchGroupRows(SearchGroupKind Kind, IReadOnlyList<SearchRow> Rows);

/// <summary>The reads of the quick search (feat-071). Implemented in Infrastructure over the one database context.</summary>
public interface ISearchReadModel
{
    /// <summary>The ids of the organization's active objects of the asked kinds (one statement; deactivated and deleted are excluded).</summary>
    Task<SearchCandidates> ReadCandidatesAsync(Guid organizationId, IReadOnlyCollection<SearchGroupKind> kinds, CancellationToken cancellationToken);

    /// <summary>
    /// One statement per asked kind, each already restricted to the visible ids and taking at most <paramref name="take"/> rows
    /// (the group's limit plus one).
    /// </summary>
    Task<IReadOnlyList<SearchGroupRows>> ReadAsync(
        IReadOnlyList<SearchScope> scopes,
        SearchQueryText query,
        IReadOnlyCollection<SearchGroupKind> kinds,
        int take,
        CancellationToken cancellationToken);
}
