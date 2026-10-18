using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// mod-003 api-contract § 5 and feat-001 api-contract § 6, through the published
/// API description: the operations tagged <c>WorkItems</c> are exactly the two
/// contracts' — no link route beyond the parent write and the picker (D-2: no
/// other link kind in this cut).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class WorkItemsOpenApiTests(MustaskApiFactory factory)
{
    private static readonly string[] ContractOperations =
    [
        "GET /api/organizations/{organizationId}/projects/{projectId}/workitems",
        "POST /api/organizations/{organizationId}/projects/{projectId}/workitems",
        "GET /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}",
        "PATCH /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}",
        "GET /api/organizations/{organizationId}/workitems/assignee-candidates",
        "GET /api/organizations/{organizationId}/projects/{projectId}/workitems/assignee-options",
        "PUT /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/parent",
        "GET /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/parent-candidates",
        "POST /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/delete",
        "POST /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/restore",
        "POST /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/purge",
    ];

    [Fact]
    public async Task OpenApiDocument_WorkItemsOperations_AreExactlyTheContracts()
    {
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();

        var operations = document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject()
                .Where(operation => operation.Value.ValueKind == JsonValueKind.Object
                    && operation.Value.TryGetProperty("tags", out var tags)
                    && tags.EnumerateArray().Any(tag => tag.GetString() == "WorkItems"))
                .Select(operation => $"{operation.Name.ToUpperInvariant()} /{path.Name.Trim('/')}"))
            .Order(StringComparer.Ordinal);

        Assert.Equal(ContractOperations.Order(StringComparer.Ordinal), operations);
    }
}
