using System.Net;

namespace Mustask.Api.IntegrationTests;

// StartPage вызывает /health с origin
// Vite dev-сервера, отличным от origin Api. Без ответа CORS-заголовков
// браузер реджектит fetch. Проверяем ту же конфигурацию (Development +
// Cors:AllowedOrigins из appsettings.Development.json), которой пользуется
// Mustask.Api при обычном запуске/через AppHost.
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class CorsTests
{
    // Совпадает со значением из appsettings.Development.json
    // (fallback-origin для запуска Mustask.Api без AppHost).
    private const string AllowedDevOrigin = "http://localhost:3000";

    private readonly MustaskApiFactory _factory;

    public CorsTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_CrossOriginRequestFromAllowedDevOrigin_ReturnsAccessControlAllowOriginHeader()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", AllowedDevOrigin);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowOriginValues),
            "Response is missing the Access-Control-Allow-Origin header — the browser would reject it.");
        Assert.Equal(AllowedDevOrigin, Assert.Single(allowOriginValues));
    }

    [Fact]
    public async Task OptionsHealth_PreflightFromAllowedDevOrigin_ReturnsCorsHeaders()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", AllowedDevOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        Assert.True(
            response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowOriginValues),
            "Preflight response is missing Access-Control-Allow-Origin — the browser would abort the real request.");
        Assert.Equal(AllowedDevOrigin, Assert.Single(allowOriginValues));
    }

    // ADR-0022: this assertion flipped. The API contract used to promise
    // ".AllowCredentials() снят" (Program.cs comment at the removal site) —
    // true for the old cookie/anti-CSRF contour that was demolished, but
    // false again once the session module added its
    // own cookie: fetch(..., { credentials: 'include' }) on
    // POST /api/session/token/logout needs this header, and the CORS policy
    // is one policy for the whole Development origin, not scoped per route —
    // enabling it for the session endpoints enables it for every response.
    [Fact]
    public async Task GetHealth_CrossOriginRequestFromAllowedDevOrigin_ReturnsAccessControlAllowCredentialsHeader()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", AllowedDevOrigin);

        var response = await client.SendAsync(request);

        Assert.True(
            response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var values) && values.Single() == "true",
            "Access-Control-Allow-Credentials: true is required — the session module's cookie travels via fetch(..., { credentials: 'include' }) (ADR-0022).");
    }

    // The fact that actually matters for the session module — a
    // credentialed cross-origin fetch to the session endpoints
    // specifically, both on preflight and on the real response.
    [Fact]
    public async Task OptionsSessionToken_PreflightFromAllowedDevOrigin_ReturnsAccessControlAllowCredentialsHeader()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/session/token");
        request.Headers.Add("Origin", AllowedDevOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
        Assert.Equal(AllowedDevOrigin, Assert.Single(origins));
        Assert.True(
            response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var credentials) && credentials.Single() == "true");
    }

    [Fact]
    public async Task PostSessionToken_CrossOriginRequestFromAllowedDevOrigin_ReturnsAccessControlAllowCredentialsHeader()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/session/token");
        request.Headers.Add("Origin", AllowedDevOrigin);

        var response = await client.SendAsync(request);

        Assert.True(
            response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var values) && values.Single() == "true");
    }

    // The scenario "браузерный доступ панели не меняется" names a
    // PROTECTED route explicitly ("preflight OPTIONS к защищённому
    // маршруту") — the only existing preflight test used /health, which is
    // both public and unaffected by the readiness gate either way. This
    // proves CORS (registered before UseRealmReadinessGate/UseAuthentication
    // in Program.cs) answers preflight before either of them ever runs.
    [Fact]
    public async Task OptionsProtectedRoute_PreflightFromAllowedDevOrigin_ReturnsCorsHeaders_WithoutToken()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/dictionaries/languages");
        request.Headers.Add("Origin", AllowedDevOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        Assert.True(
            response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowOriginValues),
            "Preflight response is missing Access-Control-Allow-Origin — the browser would abort the real request.");
        Assert.Equal(AllowedDevOrigin, Assert.Single(allowOriginValues));
    }

    [Fact]
    public async Task GetHealth_CrossOriginRequestFromDisallowedOrigin_DoesNotReturnAccessControlAllowOriginHeader()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "http://evil.example.com");

        var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    // The tests above only
    // covered the appsettings.Development.json fallback origin, never the
    // path Aspire's AppHost actually uses at runtime (Cors__AllowedOrigins__0
    // set from EndpointReference.ToString(), which can carry a trailing
    // slash — see AppHost.cs and Program.cs). This boots a second, separate
    // app host (same shared Postgres container, different Cors config) with
    // that exact env var to prove the trailing slash is normalized away and
    // origin matching still works.
    [Fact]
    public async Task GetHealth_AllowedOriginConfiguredWithTrailingSlash_StillMatchesRequestOriginWithoutSlash()
    {
        const string EnvVar = "Cors__AllowedOrigins__0";
        const string ConfiguredOriginWithTrailingSlash = "http://localhost:5174/";
        const string RequestOrigin = "http://localhost:5174";

        Environment.SetEnvironmentVariable(EnvVar, ConfiguredOriginWithTrailingSlash);
        try
        {
            using var factoryWithAspireStyleOrigin = _factory.WithWebHostBuilder(_ => { });
            var client = factoryWithAspireStyleOrigin.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
            request.Headers.Add("Origin", RequestOrigin);

            var response = await client.SendAsync(request);

            Assert.True(
                response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowOriginValues),
                "Response is missing Access-Control-Allow-Origin — a trailing slash in the configured origin broke matching.");
            Assert.Equal(RequestOrigin, Assert.Single(allowOriginValues));
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvVar, null);
        }
    }
}
