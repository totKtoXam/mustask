using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>One organization-addressed route and the organization code it declares (mod-016 api-contract § 4).</summary>
public sealed record OrganizationRoute(string Method, string Template, string Code)
{
    public string Key => $"{Method} {Template}";

    /// <summary>Only the organization's restore accepts a deactivated organization (api-contract § 4.1 row 7).</summary>
    public bool AcceptsDeactivated => Key == OrganizationAddressedRoutes.Restore;

    public bool IsWrite => Method is "POST" or "PUT" or "PATCH" or "DELETE";

    /// <summary>The route without its code, for the tests that predate the codes.</summary>
    public void Deconstruct(out string method, out string template)
    {
        method = Method;
        template = Template;
    }
}

/// <summary>
/// Every organization-addressed route that exists, with the organization code each declares (mod-016 api-contract § 4.1–§ 4.2 and
/// § 1–§ 2): the 23 of mod-002, mod-001's process read, the seven of mod-003 and feat-001 (their api-contract § 5 and § 6, rows 28–34),
/// mod-006's three (rows 35–37), feat-039's three project routes beside them and the ten of mod-016's own access and memberships;
/// feat-028 (api-contract § 4) moved the team routes to the organization's address and, at gate-3, added the creation form's member search
/// (§ 2.3a), mod-005 (api-contract § 4) added the delete, restore and purge of a work item, and feat-040 (api-contract § 6) moved
/// mod-006's three from the project's address to the team's: 59 routes in all. A later module that
/// adds one adds it here with its code, and the inventory test (<c>OrganizationAccessInventoryTests</c>) makes sure it cannot forget: a route under
/// <c>{organizationId}</c> that is not in this list, or is in it with another code than the one it declares, turns the build red.
/// </summary>
public static class OrganizationAddressedRoutes
{
    public const string Restore = "POST /api/organizations/{organizationId}/restore";

    private const string Org = "/api/organizations/{organizationId}";
    private const string Project = Org + "/projects/{projectId}";
    private const string Team = Project + "/teams/{teamId}";
    private const string OrgTeam = Org + "/teams/{teamId}";
    private const string Area = Project + "/areas/{areaId}";
    private const string Item = Project + "/workitems/{identifier}";

    public static readonly IReadOnlyList<OrganizationRoute> All =
    [
        // mod-002 api-contract § 5 (rows 4-26); mod-017 api-contract § 2.8-§ 2.9 swaps the two area-ownership routes (`…/default-area`, `…/areas/{areaId}/owner`) for `PUT …/default-team` and `GET …/owned-items/count`.
        new("GET", Org, PermissionCatalog.OrganizationsOrganizationRead),
        new("PUT", Org + "/name", PermissionCatalog.OrganizationsOrganizationWrite),
        new("POST", Org + "/deactivate", PermissionCatalog.OrganizationsOrganizationDeactivate),
        new("POST", Org + "/restore", PermissionCatalog.OrganizationsOrganizationDeactivate),
        new("GET", Org + "/projects", PermissionCatalog.OrganizationsProjectRead),
        new("GET", Org + "/projects/deactivated", PermissionCatalog.OrganizationsProjectDeactivate),
        new("POST", Org + "/projects", PermissionCatalog.OrganizationsProjectWrite),
        new("GET", Project, PermissionCatalog.OrganizationsProjectRead),
        new("PUT", Project + "/name", PermissionCatalog.OrganizationsProjectWrite),
        new("POST", Project + "/deactivate", PermissionCatalog.OrganizationsProjectDeactivate),
        new("POST", Project + "/restore", PermissionCatalog.OrganizationsProjectDeactivate),
        // feat-028 api-contract § 4: the default team is the project's own setting (project.write) and must be linked; a project
        // describes itself; its page links teams, unlinks one (the unlink and its count carry the project's link code, the team's is
        // asked by the handler) and lists the linked ones.
        new("PUT", Project + "/default-team", PermissionCatalog.OrganizationsProjectWrite),
        new("PUT", Project + "/description", PermissionCatalog.OrganizationsProjectWrite),
        new("GET", Project + "/teams", PermissionCatalog.OrganizationsTeamRead),
        new("POST", Project + "/teams", PermissionCatalog.OrganizationsProjectLink),
        new("POST", Team + "/unlink", PermissionCatalog.OrganizationsProjectLink),
        new("GET", Team + "/unlink-count", PermissionCatalog.OrganizationsProjectLink),

        // feat-028 api-contract § 2.1, § 2.2: the organization's teams, at the organization's address.
        new("GET", Org + "/teams", PermissionCatalog.OrganizationsTeamRead),
        new("POST", Org + "/teams", PermissionCatalog.OrganizationsTeamCreate),
        // § 2.3a (added at gate-3): the creation form's «Участники» searches the organization's active members before the team exists;
        // its handler asks `team.write`, the second code of a creation with members. A literal segment: never read as a `{teamId}`.
        new("GET", Org + "/teams/member-candidates", PermissionCatalog.OrganizationsTeamCreate),
        new("GET", OrgTeam, PermissionCatalog.OrganizationsTeamRead),
        new("PUT", OrgTeam + "/name", PermissionCatalog.OrganizationsTeamWrite),
        new("PUT", OrgTeam + "/description", PermissionCatalog.OrganizationsTeamWrite),
        new("DELETE", OrgTeam, PermissionCatalog.OrganizationsTeamDelete),
        new("GET", OrgTeam + "/deletion-counts", PermissionCatalog.OrganizationsTeamDelete),
        new("GET", OrgTeam + "/projects", PermissionCatalog.OrganizationsTeamRead),
        new("POST", OrgTeam + "/projects", PermissionCatalog.OrganizationsTeamLink),
        new("GET", Project + "/areas", PermissionCatalog.OrganizationsAreaRead),
        new("POST", Project + "/areas", PermissionCatalog.OrganizationsAreaWrite),
        new("PUT", Area + "/name", PermissionCatalog.OrganizationsAreaWrite),
        new("PUT", Area + "/parent", PermissionCatalog.OrganizationsAreaWrite),
        new("DELETE", Area, PermissionCatalog.OrganizationsAreaWrite),

        // mod-001: the process read (row 27).
        new("GET", Org + "/process", PermissionCatalog.ProcessDefinitionRead),

        // mod-003 api-contract § 5: the work items routes (rows 28-31, 34); feat-001 api-contract § 6: the parent write and the
        // «Родитель» picker (rows 32-33).
        new("GET", Project + "/workitems", PermissionCatalog.WorkItemsWorkItemRead),
        new("POST", Project + "/workitems", PermissionCatalog.WorkItemsWorkItemWrite),
        new("GET", Item, PermissionCatalog.WorkItemsWorkItemRead),
        new("PATCH", Item, PermissionCatalog.WorkItemsWorkItemWrite),
        new("PUT", Item + "/parent", PermissionCatalog.WorkItemsLinkWrite),
        new("GET", Item + "/parent-candidates", PermissionCatalog.WorkItemsWorkItemRead),
        new("GET", Org + "/workitems/assignee-candidates", PermissionCatalog.WorkItemsWorkItemWrite),

        // mod-005 api-contract § 2 and § 5: delete, restore and purge a work item. After the item routes, not at the end:
        // `Memberships` takes the last ten rows of this list.
        new("POST", Item + "/delete", PermissionCatalog.WorkItemsWorkItemDelete),
        new("POST", Item + "/restore", PermissionCatalog.WorkItemsWorkItemDelete),
        new("POST", Item + "/purge", PermissionCatalog.WorkItemsWorkItemPurge),

        // mod-006 api-contract § 5: a team's backlog, its reorder and its board (rows 35-37); feat-040 api-contract § 6 moved them
        // from the project's address to the team's, whose address names no project. The three project-nested routes are not served.
        new("GET", OrgTeam + "/backlogs/{level}", PermissionCatalog.WorkItemsWorkItemRead),
        new("POST", OrgTeam + "/backlogs/{level}/reorder", PermissionCatalog.BoardsBacklogReorder),
        new("GET", OrgTeam + "/boards/{level}", PermissionCatalog.WorkItemsWorkItemRead),

        // feat-039 api-contract § 5: the project's own backlog, its reorder and its board (no team in the address).
        new("GET", Project + "/backlogs/{level}", PermissionCatalog.WorkItemsWorkItemRead),
        new("POST", Project + "/backlogs/{level}/reorder", PermissionCatalog.BoardsBacklogReorder),
        new("GET", Project + "/boards/{level}", PermissionCatalog.WorkItemsWorkItemRead),

        // mod-016 api-contract § 1-§ 2: the access and the memberships.
        new("GET", Org + "/access", PermissionCatalog.OrganizationsOrganizationRead),
        new("GET", Org + "/members", PermissionCatalog.OrganizationsMembershipRead),
        new("GET", Org + "/member-candidates", PermissionCatalog.OrganizationsMembershipWrite),
        new("POST", Org + "/members", PermissionCatalog.OrganizationsMembershipWrite),
        new("PUT", Org + "/members/{userId}/role", PermissionCatalog.OrganizationsMembershipWrite),
        new("DELETE", Org + "/members/{userId}", PermissionCatalog.OrganizationsMembershipWrite),
        new("GET", OrgTeam + "/members", PermissionCatalog.OrganizationsTeamRead),
        new("GET", OrgTeam + "/member-candidates", PermissionCatalog.OrganizationsTeamWrite),
        new("POST", OrgTeam + "/members", PermissionCatalog.OrganizationsTeamWrite),
        new("DELETE", OrgTeam + "/members/{userId}", PermissionCatalog.OrganizationsTeamWrite),
    ];

    /// <summary>The ten routes of mod-016's own access and memberships (api-contract § 1-§ 2), the last of <see cref="All"/>.</summary>
    public static IEnumerable<OrganizationRoute> Memberships => All.Skip(All.Count - 10);

    /// <summary>The route's address for <paramref name="organizationId"/>, with fresh ids below it.</summary>
    public static string Path(string template, string organizationId) =>
        template
            .Replace("{organizationId}", Uri.EscapeDataString(organizationId), StringComparison.Ordinal)
            .Replace("{projectId}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal)
            .Replace("{teamId}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal)
            .Replace("{areaId}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal)
            .Replace("{userId}", "u-" + Guid.CreateVersion7().ToString("N"), StringComparison.Ordinal)
            .Replace("{identifier}", "PRM-BUG-1", StringComparison.Ordinal)
            .Replace("{level}", "stories", StringComparison.Ordinal);

    /// <summary>A body every write route binds: each field present and well formed, so only the address can be wrong.</summary>
    public static object? Body(string method) =>
        method is "POST" or "PUT" or "PATCH"
            ? new
            {
                name = UniqueBodyName(),
                key = "PRM",
                areaId = Guid.CreateVersion7().ToString(),
                parentId = Guid.CreateVersion7().ToString(),
                teamId = (string?)null,
                requestId = Guid.CreateVersion7().ToString(),
                typeId = Guid.CreateVersion7().ToString(),
                title = "Заголовок",
                itemId = Guid.CreateVersion7().ToString(),
                position = "top",
                userId = "u-" + Guid.CreateVersion7().ToString("N"),
                role = "viewer",
                description = "Описание",
                teamIds = Array.Empty<string>(),
                projectIds = Array.Empty<string>(),
                memberIds = Array.Empty<string>(),
                destination = new { teamId = (string?)null },
            }
            : null;

    private static string UniqueBodyName() => OrganizationsTestData.UniqueName("Имя");
}
