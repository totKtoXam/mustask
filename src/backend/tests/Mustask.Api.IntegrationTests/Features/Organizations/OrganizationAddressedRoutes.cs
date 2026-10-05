namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// Every organization-addressed route that exists (mod-002 api-contract § 5,
/// D-g): the 23 of the organizations module and mod-001's process read. A
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
    ];

    /// <summary>The route's address for <paramref name="organizationId"/>, with fresh ids below it.</summary>
    public static string Path(string template, string organizationId) =>
        template
            .Replace("{organizationId}", Uri.EscapeDataString(organizationId), StringComparison.Ordinal)
            .Replace("{projectId}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal)
            .Replace("{teamId}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal)
            .Replace("{areaId}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal);

    /// <summary>A body every write route binds: each field present and well formed, so only the address can be wrong.</summary>
    public static object? Body(string method) =>
        method is "POST" or "PUT"
            ? new
            {
                name = UniqueBodyName(),
                key = "PRM",
                areaId = Guid.CreateVersion7().ToString(),
                parentId = Guid.CreateVersion7().ToString(),
                ownerTeamId = (string?)null,
            }
            : null;

    private static string UniqueBodyName() => OrganizationsTestData.UniqueName("Имя");
}
