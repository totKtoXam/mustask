using System.Text.Json.Serialization;

namespace Mustask.Application.Features.Search.SearchStructure;

/// <summary>
/// The answer (feat-071 api-contract): only non-empty groups. There is no total, count or "hidden" field anywhere (BR-GS-5); a test
/// pins that by reflection over these types.
/// </summary>
public sealed record SearchStructureResponse(IReadOnlyList<SearchGroupResponse> Groups);

/// <param name="Kind">One of <c>projects</c>, <c>teams</c>, <c>departments</c>, <c>areas</c>, <c>users</c>.</param>
/// <param name="HasMore">More visible matches than the group's limit exist (never on an expansion).</param>
/// <param name="Rows">The rows in rank order.</param>
public sealed record SearchGroupResponse(string Kind, bool HasMore, IReadOnlyList<SearchRowResponse> Rows);

/// <summary>One flat row; a field the kind does not have is omitted.</summary>
public sealed record SearchRowResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    [JsonPropertyName("displayName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; init; }

    [JsonPropertyName("key"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Key { get; init; }

    [JsonPropertyName("departmentId"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? DepartmentId { get; init; }

    [JsonPropertyName("departmentName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DepartmentName { get; init; }

    [JsonPropertyName("projectId"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ProjectId { get; init; }

    [JsonPropertyName("projectName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProjectName { get; init; }

    [JsonPropertyName("color"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Color { get; init; }

    [JsonPropertyName("email"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; init; }

    [JsonPropertyName("inactive"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Inactive { get; init; }

    [JsonPropertyName("similar")]
    public required bool Similar { get; init; }
}
