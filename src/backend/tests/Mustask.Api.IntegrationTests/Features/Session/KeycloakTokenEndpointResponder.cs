using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Mustask.Api.Features.Session;

namespace Mustask.Api.IntegrationTests.Features.Session;

/// <summary>
/// Stand-in for Keycloak's token endpoint — by the same discipline as
/// <see cref="MustaskApiFactory.RealmMetadataFake"/> (<c>FakeRealmMetadata</c>): shared
/// on <see cref="MustaskApiCollection"/>, every test that switches
/// <see cref="CurrentMode"/> or <see cref="OkResponseBodyFactory"/> is
/// obliged to reset it (<see cref="Reset"/>) in <c>finally</c>.
/// </summary>
/// <remarks>
/// Two different callers hit this same double with very different needs:
/// <see cref="RefreshGrantClient"/> (the renewal path,
/// <c>POST /api/session/token</c>) treats the id token as an opaque string —
/// nothing here validates it. The real ASP.NET Core
/// <c>OpenIdConnectHandler</c> (the login path,
/// <c>GET|POST /api/session/callback</c>) does not: it validates the id token's
/// issuer, audience, signature and <c>nonce</c> for real, so
/// <see cref="OkResponseBodyFactory"/> has to be overridden per test there
/// with a token whose <c>nonce</c> matches whatever the login challenge
/// handed out.
/// </remarks>
public sealed class KeycloakTokenEndpointResponder
{
    public enum Mode
    {
        Ok,
        InvalidGrant,
        Unavailable,
        /// <summary>A client-secret mismatch — 401 <c>invalid_client</c>, not <c>invalid_grant</c>.</summary>
        InvalidClient,
    }

    public Mode CurrentMode { get; set; } = Mode.Ok;

    /// <summary>
    /// Builds the JSON body for a successful token response. Defaults to a
    /// renewal-shaped body — good enough for <see cref="RefreshGrantClient"/>,
    /// which never inspects the id token's claims. Overridden by
    /// <c>SessionCallbackEndpointTests</c> to embed a specific
    /// <c>sid</c>/<c>nonce</c> pair.
    /// </summary>
    public Func<string> OkResponseBodyFactory { get; set; } = DefaultOkBody;

    private int _tokenRequestCount;

    /// <summary>Number of requests actually reaching the token endpoint since the last <see cref="Reset"/> — the concurrency assertion in <c>SessionTokenEndpointTests</c> reads this.</summary>
    public int TokenRequestCount => _tokenRequestCount;

    public void Reset()
    {
        CurrentMode = Mode.Ok;
        OkResponseBodyFactory = DefaultOkBody;
        _tokenRequestCount = 0;
    }

    public Task<HttpResponseMessage> RespondAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _tokenRequestCount);

        return CurrentMode switch
        {
            Mode.Ok => Task.FromResult(JsonResponse(HttpStatusCode.OK, OkResponseBodyFactory())),
            Mode.InvalidGrant => Task.FromResult(JsonResponse(
                HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"Session not active"}""")),
            Mode.Unavailable => Task.FromException<HttpResponseMessage>(
                new HttpRequestException("Keycloak token endpoint is unavailable (KeycloakTokenEndpointResponder.Mode.Unavailable).")),
            Mode.InvalidClient => Task.FromResult(JsonResponse(
                HttpStatusCode.Unauthorized, """{"error":"invalid_client","error_description":"Invalid client secret"}""")),
            _ => throw new InvalidOperationException($"Unknown {nameof(Mode)}: {CurrentMode}."),
        };
    }

    /// <summary>
    /// The same fixed default
    /// <paramref name="sub"/> every call site that doesn't override it gets,
    /// so a sign-in id token and a later renewal id token naturally agree on
    /// "sub" unless a test deliberately picks a different one — before this
    /// default existed, each call inherited <see cref="KeycloakTestRealm.TokenOptions.Subject"/>'s
    /// own default, a fresh random GUID every time, which would have made
    /// every renewal test fail the new sub check by accident.
    /// </summary>
    public const string DefaultSubject = "session-test-subject";

    /// <summary>Builds a real id token — <see cref="KeycloakTestRealm.CreateToken"/> with the panel's client id, no resource_access.</summary>
    public static string BuildIdToken(string sid, string? nonce, string sub = DefaultSubject) =>
        KeycloakTestRealm.CreateToken(configure: t =>
        {
            t.Audience = SessionOidcOptions.ClientId;
            t.Permissions = null;
            t.Sid = sid;
            t.Nonce = nonce;
            t.Subject = sub;
        });

    public static string DefaultOkBody() => BuildBody(
        accessToken: KeycloakTestRealm.CreateToken(),
        idToken: BuildIdToken(sid: Guid.CreateVersion7().ToString(), nonce: null),
        refreshToken: "test-refresh-token",
        expiresIn: 300,
        refreshExpiresIn: 1800);

    public static string BuildBody(
        string accessToken, string idToken, string refreshToken, int expiresIn, int? refreshExpiresIn)
    {
        var payload = new Dictionary<string, object?>
        {
            ["access_token"] = accessToken,
            ["id_token"] = idToken,
            ["refresh_token"] = refreshToken,
            ["token_type"] = "Bearer",
            ["expires_in"] = expiresIn,
        };

        if (refreshExpiresIn is { } value)
        {
            payload["refresh_expires_in"] = value;
        }

        return JsonSerializer.Serialize(payload);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
        {
            Headers = { ContentType = new MediaTypeHeaderValue("application/json") },
        },
    };
}

/// <summary>
/// Routes both consumers of the token endpoint — <see cref="RefreshGrantClient"/>'s
/// named <see cref="HttpClient"/> and the OIDC scheme's own
/// <c>Backchannel</c> — to the same <see cref="KeycloakTokenEndpointResponder"/>
/// (wired up in <see cref="MustaskApiFactory"/>).
/// </summary>
public sealed class KeycloakTokenEndpointStubHandler(KeycloakTokenEndpointResponder responder) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        responder.RespondAsync(request, cancellationToken);
}
