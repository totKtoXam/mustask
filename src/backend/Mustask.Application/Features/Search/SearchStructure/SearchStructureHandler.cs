using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Search;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Domain.Search;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Search.SearchStructure;

/// <summary>
/// The quick search over departments, teams, projects, sub-areas and users of one organization (feat-071 plan § 2). The order is
/// fixed by BR-GS-4/-5: sight first (the caller's <see cref="AccessSet"/> turns the organization's active objects into the visible
/// id arrays; the administrator has none to pass), then match, rank and limit in the database, one statement per group, so a hidden
/// object is never seen by the read and cannot change a row, an order or "more". The users group needs
/// <c>organizations.membership.read</c> and is absent without it. The handler writes no log line: the typed text must never reach one.
/// </summary>
public sealed class SearchStructureHandler(ISearchReadModel reads, IOrganizationAccess access)
{
    /// <summary>Rows a group shows on the first read.</summary>
    public const int FirstReadLimit = 5;

    /// <summary>Rows a group shows when expanded.</summary>
    public const int ExpandedLimit = 20;

    public async Task<SearchStructureResponse> Handle(SearchStructureQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The address is judged before the body: a malformed or foreign organization is the organization's 404, whatever was typed.
        var organizationId = AddressedId.TryParse(request.OrganizationId, out var id)
            ? id
            : throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);

        var (query, expand) = SearchStructureValidator.Validate(request);

        var usersAllowed = access.Holds(PermissionCatalog.OrganizationsMembershipRead);
        var kinds = (expand is { } one ? [one] : Enum.GetValues<SearchGroupKind>())
            .Where(kind => kind != SearchGroupKind.Users || usersAllowed)
            .ToList();

        if (kinds.Count == 0)
        {
            return new SearchStructureResponse([]);
        }

        var visibility = await VisibilityAsync(organizationId, kinds, cancellationToken);
        var limit = expand is null ? FirstReadLimit : ExpandedLimit;

        var groups = await reads.ReadAsync(
            [new SearchScope(organizationId, usersAllowed, visibility)], query, kinds, limit + 1, cancellationToken);

        var answered = new List<(SearchGroupKind Kind, int BestTier, SearchGroupResponse Group)>();

        foreach (var group in groups)
        {
            var (rows, hasMore) = SimilarityRule.Fill(group.Rows, row => row.Tier, limit);

            if (rows.Count == 0)
            {
                continue;
            }

            answered.Add((
                group.Kind,
                rows.Min(row => row.Tier),
                new SearchGroupResponse(group.Kind.ToWireName(), expand is null && hasMore, rows.Select(Map).ToList())));
        }

        return new SearchStructureResponse(
            answered.OrderBy(group => group.BestTier).ThenBy(group => group.Kind).Select(group => group.Group).ToList());
    }

    private async Task<SearchVisibility> VisibilityAsync(Guid organizationId, IReadOnlyCollection<SearchGroupKind> kinds, CancellationToken cancellationToken)
    {
        // The administrator sees everything: no ids are read or passed (BR-TD-12..-14).
        if (access.Role == OrganizationRole.Admin)
        {
            return SearchVisibility.Unrestricted;
        }

        if (!kinds.Any(kind => kind != SearchGroupKind.Users))
        {
            return new SearchVisibility([], [], [], []);
        }

        var set = await access.SetAsync(cancellationToken);
        var candidates = await reads.ReadCandidatesAsync(organizationId, kinds, cancellationToken);

        return new SearchVisibility(
            kinds.Contains(SearchGroupKind.Projects) ? candidates.Projects.Where(set.SeesProject).ToList() : [],
            kinds.Contains(SearchGroupKind.Teams) ? candidates.Teams.Where(set.SeesTeam).ToList() : [],
            kinds.Contains(SearchGroupKind.Departments) ? candidates.Departments.Where(set.SeesArea).ToList() : [],
            kinds.Contains(SearchGroupKind.Areas) ? candidates.Areas.Where(set.SeesArea).ToList() : []);
    }

    private static SearchRowResponse Map(SearchRow row) => row.Kind == SearchGroupKind.Users
        ? new SearchRowResponse
        {
            Id = row.Id,
            DisplayName = row.Name,
            Email = row.Email,
            Inactive = row.Inactive ?? false,
            Similar = row.Tier == MatchTier.Similar,
        }
        : new SearchRowResponse
        {
            Id = row.Id,
            Name = row.Name,
            Key = row.Key,
            DepartmentId = row.DepartmentId,
            DepartmentName = row.DepartmentName,
            ProjectId = row.ProjectId,
            ProjectName = row.ProjectName,
            Color = row.Color,
            Similar = row.Tier == MatchTier.Similar,
        };
}
