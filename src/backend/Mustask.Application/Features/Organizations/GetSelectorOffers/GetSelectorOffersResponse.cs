namespace Mustask.Application.Features.Organizations.GetSelectorOffers;

/// <summary>A department: id and name.</summary>
public sealed record OfferDepartmentResponse(Guid Id, string Name);

/// <summary>
/// An active project: its place (<paramref name="DepartmentId"/> and <paramref name="DepartmentName"/>, both <c>null</c> at the top
/// level) and the ids of the active teams linked to it.
/// </summary>
public sealed record OfferProjectResponse(
    Guid Id, string Name, string Key, Guid? DepartmentId, string? DepartmentName, IReadOnlyList<Guid> TeamIds);

/// <summary>An active team: whether the caller is in it and the ids of the departments it is linked to directly.</summary>
public sealed record OfferTeamResponse(Guid Id, string Name, bool IsMember, IReadOnlyList<Guid> DepartmentIds);

/// <summary>The selectors' offers (feat-043 api-contract § 2): each list by name, the links to judge BR-NV-17's fit on the panel.</summary>
public sealed record SelectorOffersResponse(
    IReadOnlyList<OfferDepartmentResponse> Departments,
    IReadOnlyList<OfferProjectResponse> Projects,
    IReadOnlyList<OfferTeamResponse> Teams);
