using Mustask.Api.IntegrationTests.Features.Organizations;

namespace Mustask.Api.IntegrationTests.Kit;

/// <summary>
/// One client for the endpoint tests (tech-035): it carries a token, so a test names the caller once and not on every call. The
/// default caller is <see cref="Mustask.Testing.TestData.Creator"/> — the administrator of every organization the world builder
/// makes. <see cref="As"/> and <see cref="Anonymous"/> return another client; this one is never changed.
/// </summary>
public sealed class ApiClient
{
    private readonly MustaskApiFactory _factory;
    private readonly string? _token;

    public ApiClient(MustaskApiFactory factory)
        : this(factory, OrganizationsTokens.SignedIn())
    {
    }

    private ApiClient(MustaskApiFactory factory, string? token)
    {
        _factory = factory;
        _token = token;
    }

    /// <summary>The same host, another caller: a token from <see cref="OrganizationsTokens"/> or <c>KeycloakTestRealm</c>.</summary>
    public ApiClient As(string token) => new(_factory, token);

    /// <summary>The same host, no <c>Authorization</c> header.</summary>
    public ApiClient Anonymous() => new(_factory, null);

    public Task<Answer> SendAsync(HttpMethod method, string path, object? body = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(_factory, method, path, body, _token, gate);

    public Task<Answer> GetAsync(string path) => SendAsync(HttpMethod.Get, path);

    public Task<Answer> PostAsync(string path, object? body = null, CommitGate? gate = null) => SendAsync(HttpMethod.Post, path, body, gate);

    public Task<Answer> PutAsync(string path, object? body = null, CommitGate? gate = null) => SendAsync(HttpMethod.Put, path, body, gate);

    public Task<Answer> DeleteAsync(string path, CommitGate? gate = null) => SendAsync(HttpMethod.Delete, path, null, gate);
}
