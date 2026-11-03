namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// The bounded reads of feat-043's navigation: the organization's «Обзор» and the selectors' offers and lists. Every method takes
/// the <see cref="CallerAccess"/> as its first, required argument (BR-NV-15, ADR-0029 §§ 7-8), so that
/// <c>feat-041-project-visibility</c> adds the set of visible projects inside the read; a read without the caller does not compile
/// and a signature test fails on a method that lacks it. A fixed number of statements whatever the volume (NFR Volumes).
/// </summary>
public interface INavigationReadModel
{
    /// <summary>
    /// «Обзор» (US-2): the counts of departments, active projects and active teams, the first
    /// <see cref="NavigationLimits.OverviewRows"/> of each by name (Russian collation, then id), and the caller's own teams. Seven
    /// statements: three counts, three first-rows reads, the caller's teams.
    /// </summary>
    Task<OverviewRows> ReadOverviewAsync(CallerAccess caller, CancellationToken cancellationToken);

    /// <summary>
    /// The selectors' offers and the lists «Проекты» and «Команды» (US-2, US-8): every department, active project (with its place) and
    /// active team of the organization, with the links the panel needs to judge which values fit each other (BR-NV-17). Five statements.
    /// </summary>
    Task<SelectorOfferRows> ReadSelectorOffersAsync(CallerAccess caller, CancellationToken cancellationToken);
}

/// <summary>Limits of the navigation reads.</summary>
public static class NavigationLimits
{
    /// <summary>The rows each list of «Обзор» shows before its link to the full list (design § 5.1, D-2).</summary>
    public const int OverviewRows = 5;
}

/// <summary>An object named by id and name.</summary>
public sealed record NavigationRef(Guid Id, string Name);

/// <summary>A project named by id, name and key.</summary>
public sealed record NavigationProjectRef(Guid Id, string Name, string Key);

/// <summary>A team named by id and name, with whether the caller is in it.</summary>
public sealed record NavigationTeamRef(Guid Id, string Name, bool IsMember);

/// <summary>The rows of «Обзор»: the counts are of all, the lists are cut to the first rows.</summary>
public sealed record OverviewRows(
    int DepartmentCount,
    int ProjectCount,
    int TeamCount,
    IReadOnlyList<NavigationRef> Departments,
    IReadOnlyList<NavigationProjectRef> Projects,
    IReadOnlyList<NavigationTeamRef> Teams,
    IReadOnlyList<NavigationTeamRef> MyTeams);

/// <summary>An active project with its place (the department holding its node, none at the top level) and the active teams linked to it.</summary>
public sealed record OfferProjectRow(
    Guid Id, string Name, string Key, Guid? DepartmentId, string? DepartmentName, IReadOnlyList<Guid> TeamIds);

/// <summary>An active team, whether the caller is in it, and the departments it is linked to directly.</summary>
public sealed record OfferTeamRow(Guid Id, string Name, bool IsMember, IReadOnlyList<Guid> DepartmentIds);

/// <summary>The selectors' offers: departments, projects and teams, each by name (Russian collation, then id).</summary>
public sealed record SelectorOfferRows(
    IReadOnlyList<NavigationRef> Departments,
    IReadOnlyList<OfferProjectRow> Projects,
    IReadOnlyList<OfferTeamRow> Teams);
