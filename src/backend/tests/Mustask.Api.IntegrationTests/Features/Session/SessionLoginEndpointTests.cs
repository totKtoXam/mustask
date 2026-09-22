using System.Net;
using System.Net.Http.Json;
using Mustask.Api.Features.Session;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Mustask.Api.IntegrationTests.Features.Session;

/// <summary>
/// <c>GET /api/session/login</c>.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SessionLoginEndpointTests
{
    private readonly MustaskApiFactory _factory;

    public SessionLoginEndpointTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_NoReturnUrl_ChallengesToKeycloakAuthorizationEndpoint()
    {
        var client = _factory.CreateNoRedirectClient();

        var challenge = await SessionTestSupport.ChallengeAsync(client, returnUrl: "/");

        Assert.StartsWith(KeycloakTestRealm.Configuration.AuthorizationEndpoint, challenge.AuthorizationLocation.ToString(), StringComparison.Ordinal);
        Assert.Contains($"client_id={SessionOidcOptions.ClientId}", challenge.AuthorizationLocation.Query, StringComparison.Ordinal);
        Assert.Contains("response_type=code", challenge.AuthorizationLocation.Query, StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", challenge.AuthorizationLocation.Query, StringComparison.Ordinal);
        Assert.NotEmpty(challenge.State);
    }

    [Fact]
    public async Task Login_LocalReturnUrl_Challenges()
    {
        var client = _factory.CreateNoRedirectClient();

        var challenge = await SessionTestSupport.ChallengeAsync(client, "/dashboard");

        Assert.StartsWith(KeycloakTestRealm.Configuration.AuthorizationEndpoint, challenge.AuthorizationLocation.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://evil.example.com/steal")]
    [InlineData("//evil.example.com/steal")]
    [InlineData("/\\evil.example.com/steal")]
    // A control character or anything outside
    // ASCII must not reach the OIDC handler's own RedirectUri and, from
    // there, the "Location" header a later redirect would carry — Kestrel
    // rejects a non-ASCII header value with a 500, not our own 400.
    [InlineData("/foo\nbar")]
    [InlineData("/wünderbar")]
    public async Task Login_NonLocalReturnUrl_Returns400InvalidReturnUrl(string returnUrl)
    {
        var client = _factory.CreateNoRedirectClient();

        var response = await client.GetAsync($"/api/session/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(SessionErrorCodes.InvalidReturnUrl, body.GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// Readiness-гейт не блокирует этот
    /// маршрут (он публичный), но сам обработчик спрашивает
    /// <c>RealmReadiness</c> перед выпуском challenge — недоступный Keycloak
    /// не должен выпускать challenge, на который ответить всё равно будет
    /// нечем.
    /// </summary>
    [Fact]
    public async Task Login_RealmNotReady_RedirectsToPanelWithIdpUnavailable_WithoutIssuingChallenge()
    {
        var fake = new KeycloakTestRealm.FakeRealmMetadata { CurrentMode = KeycloakTestRealm.FakeRealmMetadata.Mode.Unavailable };
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(fake)));

        var client = factory.CreateNoRedirectClient();

        var response = await client.GetAsync("/api/session/login?returnUrl=/dashboard");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "http://panel.invalid/dashboard?authError=idp_unavailable",
            response.Headers.Location!.ToString());
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    /// <summary>
    /// Plain string interpolation put a second "?" in
    /// front of authError whenever returnUrl already carried a query string
    /// — "…/tasks?status=active?authError=idp_unavailable" is not
    /// parseable the way the panel needs, and sent it into a sign-in loop.
    /// </summary>
    [Fact]
    public async Task Login_RealmNotReady_ReturnUrlWithQueryString_AppendsAuthErrorWithAmpersand()
    {
        var fake = new KeycloakTestRealm.FakeRealmMetadata { CurrentMode = KeycloakTestRealm.FakeRealmMetadata.Mode.Unavailable };
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(fake)));

        var client = factory.CreateNoRedirectClient();

        var response = await client.GetAsync(
            $"/api/session/login?returnUrl={Uri.EscapeDataString("/tasks?status=active")}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "http://panel.invalid/tasks?status=active&authError=idp_unavailable",
            response.Headers.Location!.ToString());
    }
}
