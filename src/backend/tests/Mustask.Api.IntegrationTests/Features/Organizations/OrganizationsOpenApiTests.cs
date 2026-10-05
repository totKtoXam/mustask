using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// mod-002 api-contract § 5, through the published API description: the
/// module's operations are exactly the contract's. In particular none deletes
/// or purges an organization or a project, restores a team or an area, changes
/// a key, or writes rights, roles or groups (US-1 "No screen grants the
/// right", US-2 "no purge", US-3 "the key never changes", US-5 and US-7 "no
/// action restores it").
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class OrganizationsOpenApiTests(MustaskApiFactory factory)
{
    private static readonly string[] ContractOperations =
    [
        "GET /api/organizations",
        "GET /api/organizations/deactivated",
        "POST /api/organizations",
        // The addressed routes of other modules — mod-001's process read, mod-003's work items — are not this module's.
        .. OrganizationAddressedRoutes.All
            .Where(route => !route.Template.EndsWith("/process", StringComparison.Ordinal)
                && !route.Template.Contains("/workitems", StringComparison.Ordinal))
            .Select(route => $"{route.Method} {route.Template}"),
    ];

    [Fact]
    public async Task OpenApiDocument_OrganizationOperations_AreExactlyTheContracts()
    {
        var operations = await OperationsAsync();

        var ofTheModule = operations
            .Where(operation => Tagged(operation.Operation, "Organizations"))
            .Select(operation => $"{operation.Method} {operation.Path}")
            .Order(StringComparer.Ordinal);

        Assert.Equal(26, ContractOperations.Length);
        Assert.Equal(ContractOperations.Order(StringComparer.Ordinal), ofTheModule);
    }

    // US-3 · "the key never changes": no operation carries a key except the project's creation.
    [Fact]
    public async Task OpenApiDocument_OnlyProjectCreation_TakesAKey()
    {
        var (document, operations) = await DocumentAsync();

        var withKey = operations
            .Where(operation => Tagged(operation.Operation, "Organizations"))
            .Where(operation => RequestProperties(document, operation.Operation).Contains("key"))
            .Select(operation => $"{operation.Method} {operation.Path}");

        Assert.Equal(["POST /api/organizations/{organizationId}/projects"], withKey);
    }

    // US-1 · "No screen grants the right": nothing under /api/organizations names a right, a role or a group.
    [Fact]
    public async Task OpenApiDocument_NoOrganizationOperationWritesRightsRolesOrGroups()
    {
        var operations = await OperationsAsync();

        Assert.DoesNotContain(
            operations,
            operation => operation.Path.StartsWith("/api/organizations", StringComparison.Ordinal)
                && new[] { "role", "group", "permission", "right" }.Any(word => operation.Path.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    private async Task<List<(string Path, string Method, JsonElement Operation)>> OperationsAsync() => (await DocumentAsync()).Operations;

    private async Task<(JsonElement Document, List<(string Path, string Method, JsonElement Operation)> Operations)> DocumentAsync()
    {
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();

        var operations = document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject()
                .Where(operation => operation.Value.ValueKind == JsonValueKind.Object && operation.Value.TryGetProperty("responses", out _))
                .Select(operation => (Path: "/" + path.Name.Trim('/'), Method: operation.Name.ToUpperInvariant(), Operation: operation.Value)))
            .ToList();
        Assert.NotEmpty(operations);

        return (document, operations);
    }

    private static bool Tagged(JsonElement operation, string tag) =>
        operation.TryGetProperty("tags", out var tags) && tags.EnumerateArray().Any(candidate => candidate.GetString() == tag);

    /// <summary>The property names of the operation's JSON request body, following a schema reference.</summary>
    private static HashSet<string> RequestProperties(JsonElement document, JsonElement operation)
    {
        if (!operation.TryGetProperty("requestBody", out var body)
            || !body.GetProperty("content").TryGetProperty("application/json", out var json)
            || !json.TryGetProperty("schema", out var schema))
        {
            return [];
        }

        schema = Resolve(document, schema);

        if (schema.TryGetProperty("anyOf", out var anyOf) || schema.TryGetProperty("oneOf", out anyOf))
        {
            return anyOf.EnumerateArray().SelectMany(variant => Properties(Resolve(document, variant))).ToHashSet(StringComparer.Ordinal);
        }

        return Properties(schema).ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<string> Properties(JsonElement schema) =>
        schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("properties", out var properties)
            ? properties.EnumerateObject().Select(property => property.Name)
            : [];

    private static JsonElement Resolve(JsonElement document, JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out var reference))
        {
            return schema;
        }

        var name = reference.GetString()!.Split('/')[^1];

        return document.GetProperty("components").GetProperty("schemas").GetProperty(name);
    }
}
