using System.Net.Http.Json;
using System.Text.Json;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// Sends one request to the organizations API and reads the answer as JSON
/// (mod-002 plan-backend § 14.1 step 5). A test names the body as an anonymous
/// object, or leaves it out; it may hold the request at a commit gate.
/// </summary>
public static class OrganizationsApi
{
    public static string Organization(Guid organizationId) => $"/api/organizations/{organizationId}";

    public static string Project(Guid organizationId, Guid projectId) => $"/api/organizations/{organizationId}/projects/{projectId}";

    public static string Areas(Guid organizationId, Guid projectId) => $"{Project(organizationId, projectId)}/areas";

    /// <summary>The project's linked teams (feat-028): the list, the link and the unlink are under it.</summary>
    public static string Teams(Guid organizationId, Guid projectId) => $"{Project(organizationId, projectId)}/teams";

    /// <summary>The organization's teams (feat-028 api-contract § 2.1): the list, the create, and a team at /{teamId}.</summary>
    public static string OrgTeams(Guid organizationId) => $"{Organization(organizationId)}/teams";

    public static async Task<Answer> SendAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        HttpMethod method,
        string path,
        object? body = null,
        string? token = null,
        CommitGate? gate = null)
    {
        using var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        if (token is not null)
        {
            request.WithBearer(token);
        }

        if (gate is not null)
        {
            request.Headers.Add(CommitGates.Header, gate.Name);
        }

        using var response = await factory.CreateClient().SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var json = string.IsNullOrEmpty(text) ? default : JsonDocument.Parse(text).RootElement.Clone();

        return new Answer(response.StatusCode, response.Headers.Location, response.Content.Headers.ContentType?.MediaType, json);
    }
}

/// <summary>An answer, read in full: status, <c>Location</c>, media type and the JSON body (<c>default</c> when empty).</summary>
public sealed record Answer(System.Net.HttpStatusCode Status, Uri? Location, string? MediaType, JsonElement Body)
{
    /// <summary>The problem's <c>errorCode</c>, or <c>null</c> for a body without one.</summary>
    public string? ErrorCode =>
        Body.ValueKind == JsonValueKind.Object && Body.TryGetProperty("errorCode", out var code) ? code.GetString() : null;

    public string? Detail =>
        Body.ValueKind == JsonValueKind.Object && Body.TryGetProperty("detail", out var detail) ? detail.GetString() : null;
}
