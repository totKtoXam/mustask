using System.Net;
using System.Text;
using Mustask.Api.Features.Session;
using Xunit.Abstractions;

namespace Mustask.Api.IntegrationTests.Features.Session;

/// <summary>
/// ADR-0022 Decision 1: stripping the
/// access token was not enough for a single chunk on real tokens — the OIDC
/// handler's own default <c>ClaimActions</c> copy every other id-token claim
/// (name, preferred_username, email, given_name/family_name, groups, ...)
/// onto the ticket's principal unfiltered, duplicating what the raw
/// <c>id_token</c> string already carries. Against tokens calibrated to a
/// real Keycloak profile (profile/email/groups scopes), the pre-fix code
/// reproduced the live defect: three <c>Set-Cookie</c> headers
/// (<c>mustask.session</c>, <c>mustask.sessionC1</c>, <c>mustask.sessionC2</c>)
/// totalling 5838 bytes (8 + 3945 + 1885) — the same shape and order of
/// magnitude as the stand measurement (8 + 3945 + 1949 =
/// 5902 bytes; the 3945-byte cap on the first chunk is
/// <c>ChunkingCookieManager</c>'s own chunk size, identical on both). This
/// test guards the fix: <see cref="SessionAuthenticationSetup"/> now keeps
/// only <c>sub</c>/<c>sid</c> on the principal and only
/// <c>refresh_token</c>/<c>id_token</c> in
/// <see cref="Microsoft.AspNetCore.Authentication.AuthenticationProperties"/>,
/// which is what gets the same calibrated tokens back under one chunk.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SessionCookieContractTests
{
    private readonly MustaskApiFactory _factory;
    private readonly ITestOutputHelper _testOutput;

    public SessionCookieContractTests(MustaskApiFactory factory, ITestOutputHelper testOutput)
    {
        _factory = factory;
        _testOutput = testOutput;
    }

    [Fact]
    public async Task Callback_RealisticSizedTokens_ProducesExactlyOneSetCookieHeader_NotAChunkedSet()
    {
        var client = _factory.CreateNoRedirectClient();
        var challenge = await SessionTestSupport.ChallengeAsync(client);
        var sid = Guid.CreateVersion7().ToString();

        // A close-to-real refresh token: Keycloak's own refresh tokens are
        // JWTs too, comparable in size to an access token carrying a modest
        // permission set.
        var realisticRefreshToken = KeycloakTestRealm.CreateToken(
            ["projects.boards.read", "projects.boards.write", "projects.members.write", "tasks.items.write",
             "reports.dashboards.read", "tasks.items.read", "dictionaries.languages.read", "dictionaries.languages.write"]);

        // The live measurement was against a real sign-in
        // for user "mustask" — profile+email+groups claims a synthetic
        // sub/preferred_username id token never carried. The default
        // client scopes of mustask-panel (deploy/keycloak/realms/mustask-realm.json)
        // are basic/acr/profile/email/roles/web-origins, plus the
        // full.path=true groups mapper — an id token this shape is what
        // reproduced the defect live, not the ticket's own extra
        // AuthenticationProperties bookkeeping.
        var realisticIdToken = KeycloakTestRealm.CreateToken(configure: t =>
        {
            t.Audience = SessionOidcOptions.ClientId;
            t.Permissions = null;
            t.Sid = sid;
            t.Nonce = challenge.Nonce;
            t.PreferredUsername = "mustask";
            t.Name = "Иванов Иван Иванович";
            t.GivenName = "Иван";
            t.FamilyName = "Иванов";
            t.Email = "ivan.ivanov@workspace.mustask.example";
            t.EmailVerified = true;
            t.Groups =
            [
                "/Администраторы",
                "/Сотрудники/Участники",
                "/Администраторы справочников",
            ];
        });

        _testOutput.WriteLine($"calibrated refresh token: {realisticRefreshToken.Length} chars.");
        _testOutput.WriteLine($"calibrated id token: {realisticIdToken.Length} chars.");

        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: KeycloakTestRealm.CreateToken(),
            idToken: realisticIdToken,
            refreshToken: realisticRefreshToken,
            expiresIn: 300,
            refreshExpiresIn: 1800);

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"/api/session/callback?code=test-code&state={Uri.EscapeDataString(challenge.State)}");
            SessionTestSupport.AttachCookies(request, challenge.Cookies);
            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var sessionCookies = response.Headers.GetValues("Set-Cookie")
                .Where(header => header.StartsWith($"{SessionAuthenticationSetup.CookieName}", StringComparison.Ordinal))
                .ToList();

            // Calibration evidence: every Set-Cookie header this
            // response actually carried, and each one's byte size — one
            // "mustask.session" line here is the fix; three lines
            // ("mustask.session"/"mustask.sessionC1"/"mustask.sessionC2") would be the
            // defect reproduced.
            foreach (var header in sessionCookies)
            {
                var nameValue = header.Split(';')[0];
                var name = nameValue.Split('=')[0];
                var value = nameValue[(name.Length + 1)..];
                _testOutput.WriteLine($"{name}: {Encoding.UTF8.GetByteCount(value)} bytes.");
            }

            var singleCookie = Assert.Single(sessionCookies);
            Assert.StartsWith($"{SessionAuthenticationSetup.CookieName}=", singleCookie, StringComparison.Ordinal);

            var cookieValue = singleCookie.Split(';')[0];
            var sizeBytes = Encoding.UTF8.GetByteCount(cookieValue);

            // "Размер cookie измеряется на стенде на реальном токене" — this is the CI-side lower bound on that manual check,
            // not a replacement for it (real Keycloak claims/JWKS differ).
            _testOutput.WriteLine($"mustask.session cookie size: {sizeBytes} bytes (name=value only, no attributes).");
            Assert.True(sizeBytes < 4096, $"mustask.session is {sizeBytes} bytes — approaching the ~4 KB single-cookie limit.");
        }
        finally
        {
            _factory.TokenResponder.Reset();
        }
    }
}
