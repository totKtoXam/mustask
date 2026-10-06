namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// Every organization-addressed route that exists (mod-002 api-contract § 5,
/// D-g): the 23 of the organizations module, mod-001's process read and the five
/// of mod-003's work items (its api-contract § 5). A
/// later module that adds one adds it here, and the inventory test makes sure
/// it cannot forget.
/// </summary>
public static class OrganizationAddressedRoutes
{
    public const string Restore = "POST /api/organizations/{organizationId}/restore";

    public static readonly IReadOnlyList<(string Method, string Template)> All =
    [
        ("GET", "/api/organizations/{organizationId}"),
        ("PUT", "/api/organizations/{organizationId}/name"),
        ("POST", "/api/organizations/{organizationId}/deactivate"),
        ("POST", "/api/organizations/{organizationId}/restore"),
        ("GET", "/api/organizations/{organizationId}/projects"),
        ("GET", "/api/organizations/{organizationId}/projects/deactivated"),
        ("POST", "/api/organizations/{organizationId}/projects"),
        ("GET", "/api/organizations/{organizationId}/projects/{projectId}"),
        ("PUT", "/api/organizations/{organizationId}/projects/{projectId}/name"),
        ("POST", "/api/organizations/{organizationId}/projects/{projectId}/deactivate"),
        ("POST", "/api/organizations/{organizationId}/projects/{projectId}/restore"),
        ("GET", "/api/organizations/{organizationId}/projects/{projectId}/teams"),
        ("GET", "/api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}"),
        ("POST", "/api/organizations/{organizationId}/projects/{projectId}/teams"),
        ("PUT", "/api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/name"),
        ("PUT", "/api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/default-area"),
        ("DELETE", "/api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}"),
        ("GET", "/api/organizations/{organizationId}/projects/{projectId}/areas"),
        ("POST", "/api/organizations/{organizationId}/projects/{projectId}/areas"),
        ("PUT", "/api/organizations/{organizationId}/projects/{projectId}/areas/{areaId}/name"),
        ("PUT", "/api/organizations/{organizationId}/projects/{projectId}/areas/{areaId}/parent"),
        ("PUT", "/api/organizations/{organizationId}/projects/{projectId}/areas/{areaId}/owner"),
        ("DELETE", "/api/organizations/{organizationId}/projects/{projectId}/areas/{areaId}"),
        ("GET", "/api/organizations/{organizationId}/process"),

        // mod-003 api-contract § 5: the work items routes (BR-M3-27).
        ("GET", "/api/organizations/{organizationId}/projects/{projectId}/workitems"),
        ("POST", "/api/organizations/{organizationId}/projects/{projectId}/workitems"),
        ("GET", "/api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}"),
        ("PATCH", "/api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}"),
        ("GET", "/api/organizations/{organizationId}/workitems/assignee-candidates"),

        // mod-006 api-contract § 5: a team's backlog, its reorder and its board (BR-M4-27).
        ("GET", "/api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/backlogs/{level}"),
        ("POST", "/api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/backlogs/{level}/reorder"),
        ("GET", "/api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/boards/{level}"),
    ];

    /// <summary>The route's address for <paramref name="organizationId"/>, with fresh ids below it.</summary>
    public static string Path(string template, string organizationId) =>
        template
            .Replace("{organizationId}", Uri.EscapeDataString(organizationId), StringComparison.Ordinal)
            .Replace("{projectId}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal)
            .Replace("{teamId}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal)
            .Replace("{areaId}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal)
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
                ownerTeamId = (string?)null,
                requestId = Guid.CreateVersion7().ToString(),
                typeId = Guid.CreateVersion7().ToString(),
                title = "Заголовок",
                itemId = Guid.CreateVersion7().ToString(),
                position = "top",
            }
            : null;

    private static string UniqueBodyName() => OrganizationsTestData.UniqueName("Имя");
}
