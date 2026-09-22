using System.Net;
using Mustask.Api.Features.Session;

namespace Mustask.Api.IntegrationTests.Features.Session;

/// <summary>
/// Found live with a real Keycloak: the OIDC handler's
/// default <c>ResponseMode.FormPost</c> makes Keycloak deliver the
/// authorization code by <b>POST</b>, form-encoded, not by <b>GET</b> with a
/// query string. The callback stub in <c>SessionEndpoints</c> was mapped for
/// GET only, so routing answered <b>405</b> before the OpenIdConnect handler
/// (which intercepts <c>CallbackPath</c> inside <c>UseAuthentication()</c>,
/// upstream of routing) ever saw the request — reproduced on the stand with
/// <c>curl</c>.
///
/// This test does not run the rest of the handshake
/// (<see cref="SessionCallbackEndpointTests"/> does that): no login challenge,
/// no correlation cookies, an intentionally invalid <c>state</c>. That is the
/// point — it isolates the one thing this fix changes, the route's allowed
/// methods, from everything the OIDC handler does once it is reached. An
/// invalid <c>state</c> still proves the request got past routing: the
/// handler rejects it and redirects with <c>authError=sign_in_failed</c>
/// (<see cref="SessionAuthenticationSetup.ClassifyFailure"/> — anything that
/// is not <c>access_denied</c> or a network/discovery failure falls here),
/// rather than answering 405.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SessionCallbackMethodRegressionTests
{
    private readonly MustaskApiFactory _factory;

    public SessionCallbackMethodRegressionTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Callback_PostFormEncoded_WithInvalidState_ReachesTheHandler_NotMethodNotAllowed()
    {
        var client = _factory.CreateNoRedirectClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/session/callback")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = "test-code",
                ["state"] = "not-a-real-state",
            }),
        };

        var response = await client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "http://panel.invalid/?authError=sign_in_failed",
            response.Headers.Location!.ToString());
    }
}
