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
        // The addressed routes of other modules — mod-001's process read, mod-003's work items, mod-006's backlogs and
        // boards — are not this module's. The rest of the list is mod-002's and, last, mod-016's access and memberships
        // (api-contract § 1–§ 2).
        .. OrganizationAddressedRoutes.All
            .Where(route => !route.Template.EndsWith("/process", StringComparison.Ordinal)
                && !route.Template.Contains("/workitems", StringComparison.Ordinal)
                && !route.Template.Contains("/backlogs/", StringComparison.Ordinal)
                && !route.Template.Contains("/boards/", StringComparison.Ordinal))
            .Select(route => route.Key),
    ];

    [Fact]
    public async Task OpenApiDocument_OrganizationOperations_AreExactlyTheContracts()
    {
        var operations = await OperationsAsync();

        var ofTheModule = operations
            .Where(operation => Tagged(operation.Operation, "Organizations"))
            .Select(operation => $"{operation.Method} {operation.Path}")
            .Order(StringComparer.Ordinal);

        // feat-028 api-contract § 4: eight project-nested team routes out and sixteen organization-level ones in, 36 -> 44 of this module;
        // at gate-3 the creation form's member search (§ 2.3a) makes 45. mod-018 adds fifteen net (8 area + 12 link routes in, 5 tree routes out): 60.
        Assert.Equal(60, ContractOperations.Length);
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

    // US-1 · "No screen grants the right": nothing under /api/organizations names a right, a role or a group. mod-016 adds exactly one
    // route that says "role": the change of an organization MEMBER's role (api-contract § 1.5) — an organization role of ADR-0027, a
    // column of our own membership table, not a Keycloak role, right or group; nothing here writes the realm (ADR-0023).
    private const string MembershipRoleRoute = "/api/organizations/{organizationId}/members/{userId}/role";

    [Fact]
    public async Task OpenApiDocument_NoOrganizationOperationWritesRightsRolesOrGroups_ExceptTheMemberRoleOfTheOrganization()
    {
        var operations = await OperationsAsync();

        Assert.DoesNotContain(
            operations,
            operation => operation.Path.StartsWith("/api/organizations", StringComparison.Ordinal)
                && operation.Path != MembershipRoleRoute
                && new[] { "role", "group", "permission", "right" }.Any(word => operation.Path.Contains(word, StringComparison.OrdinalIgnoreCase)));
        Assert.Single(operations, operation => operation.Path == MembershipRoleRoute);
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
