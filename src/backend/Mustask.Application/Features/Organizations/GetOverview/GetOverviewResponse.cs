namespace Mustask.Application.Features.Organizations.GetOverview;

/// <summary>An object of a list of «Обзор»: id and name.</summary>
public sealed record OverviewRefResponse(Guid Id, string Name);

/// <summary>A project of «Обзор»: id, name and key.</summary>
public sealed record OverviewProjectResponse(Guid Id, string Name, string Key);

/// <summary>A team of «Обзор»: id, name and whether the caller is in it.</summary>
public sealed record OverviewTeamResponse(Guid Id, string Name, bool IsMember);

/// <summary>
/// «Обзор» (feat-043 api-contract § 1): the counts are of every object, the three lists hold only the first rows by name, and
/// <paramref name="MyTeams"/> are all the caller's own teams by name.
/// </summary>
public sealed record OverviewResponse(
    int DepartmentCount,
    int ProjectCount,
    int TeamCount,
    IReadOnlyList<OverviewRefResponse> Departments,
    IReadOnlyList<OverviewProjectResponse> Projects,
    IReadOnlyList<OverviewTeamResponse> Teams,
    IReadOnlyList<OverviewTeamResponse> MyTeams);
