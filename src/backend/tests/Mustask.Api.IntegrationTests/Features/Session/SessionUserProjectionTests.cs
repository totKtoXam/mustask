using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.Features.Session;
using Mustask.Api.Features.UserDirectory;
using Mustask.Api.IntegrationTests.Features.UserDirectory;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Features.UserDirectory.RecordSignIn;
using Mustask.Domain.UserDirectory;
using Mustask.Infrastructure;

namespace Mustask.Api.IntegrationTests.Features.Session;

/// <summary>
/// tech-025 (ADR-0034): the user projection is filled from the sign-in. The callback and the renewal upsert the person
/// from the id token's claims; a sign-in the projection cannot record is refused for a person it does not know and let
/// through for one it does. Every test uses a subject of its own, so none depends on the state another one leaves.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SessionUserProjectionTests
{
    private readonly MustaskApiFactory _factory;

    public SessionUserProjectionTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    private static string NewSub() => "tech025-" + Guid.NewGuid().ToString("N");

    private static string Oversized() => new('я', UserDirectoryLimits.FieldMaxLength + 1);

    private static Action<KeycloakTestRealm.TokenOptions> Claims(
        string username = "anna.s", string? first = "Анна", string? last = "Смирнова", string? email = "anna.s@example.test") =>
        t =>
        {
            t.PreferredUsername = username;
            t.GivenName = first;
            t.FamilyName = last;
            t.Email = email;
        };

    private sealed record SignedIn(HttpResponseMessage Response, string Sid)
    {
        public string? SetCookie => SessionTestSupport.SessionCookie(Response);

        public string Cookie => SetCookie!.Split(';')[0];
    }

    private async Task<SignedIn> SignInAsync(HttpClient client, string sub, Action<KeycloakTestRealm.TokenOptions> claims)
    {
        var challenge = await SessionTestSupport.ChallengeAsync(client, "/dashboard");
        var sid = Guid.CreateVersion7().ToString();

        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: KeycloakTestRealm.CreateToken(configure: t => t.Roles = ["user"]),
            idToken: KeycloakTokenEndpointResponder.BuildIdToken(sid, challenge.Nonce, sub, claims),
            refreshToken: "test-refresh-token",
            expiresIn: 300,
            refreshExpiresIn: 1800);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/session/callback")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = "test-code", ["state"] = challenge.State }),
            };
            SessionTestSupport.AttachCookies(request, challenge.Cookies);

            return new SignedIn(await client.SendAsync(request), sid);
        }
        finally
        {
            _factory.TokenResponder.Reset();
        }
    }

    private async Task<bool> IsSearchableAsync(string id)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IUserDirectory>();

        return (await directory.LookupAsync([id], CancellationToken.None)).Known.ContainsKey(id);
    }

    [Fact]
    public async Task Callback_FirstSignIn_CreatesTheProjectionRow_AndOnlyThenTheUserIsFound()
    {
        var client = _factory.CreateNoRedirectClient();
        var sub = NewSub();

        Assert.False(await IsSearchableAsync(sub));

        var signedIn = await SignInAsync(client, sub, Claims());

        Assert.Equal(HttpStatusCode.Redirect, signedIn.Response.StatusCode);
        Assert.NotNull(signedIn.SetCookie);

        var row = await UserDirectoryTestData.FindAsync(_factory, sub);
        Assert.NotNull(row);
        Assert.Equal("anna.s", row.Username);
        Assert.Equal("Анна Смирнова", row.DisplayName);
        Assert.Equal("anna.s@example.test", row.Email);
        Assert.True(row.IsActive);
        Assert.True(await IsSearchableAsync(sub));
    }

    [Fact]
    public async Task Callback_SecondSignInWithChangedNames_UpdatesTheSameRow()
    {
        var client = _factory.CreateNoRedirectClient();
        var sub = NewSub();
        await SignInAsync(client, sub, Claims());

        var signedIn = await SignInAsync(client, sub, Claims(last: "Кузнецова", email: "a.kuznetsova@example.test"));

        Assert.Equal(HttpStatusCode.Redirect, signedIn.Response.StatusCode);

        var row = await UserDirectoryTestData.FindAsync(_factory, sub);
        Assert.Equal("Анна Кузнецова", row!.DisplayName);
        Assert.Equal("a.kuznetsova@example.test", row.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Callback_NewUserWithoutAUsername_FailsTheSignIn_AndStoresNothing(string username)
    {
        var client = _factory.CreateNoRedirectClient();
        var sub = NewSub();

        var signedIn = await SignInAsync(client, sub, Claims(username: username));

        Assert.Equal(HttpStatusCode.Redirect, signedIn.Response.StatusCode);
        Assert.Contains("authError=sign_in_failed", signedIn.Response.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Null(signedIn.SetCookie);
        Assert.Null(await UserDirectoryTestData.FindAsync(_factory, sub));
    }

    [Fact]
    public async Task Callback_NewUserWithAnOversizedClaim_FailsTheSignIn()
    {
        var client = _factory.CreateNoRedirectClient();
        var sub = NewSub();

        var signedIn = await SignInAsync(client, sub, Claims(first: Oversized()));

        Assert.Contains("authError=sign_in_failed", signedIn.Response.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Null(signedIn.SetCookie);
        Assert.Null(await UserDirectoryTestData.FindAsync(_factory, sub));
    }

    [Fact]
    public async Task Callback_KnownUserWhoseUpsertFails_IsLetThrough_AndKeepsTheStoredRow()
    {
        var client = _factory.CreateNoRedirectClient();
        var sub = NewSub();
        await SignInAsync(client, sub, Claims());

        var signedIn = await SignInAsync(client, sub, Claims(first: Oversized()));

        Assert.Equal(HttpStatusCode.Redirect, signedIn.Response.StatusCode);
        Assert.DoesNotContain("authError", signedIn.Response.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.NotNull(signedIn.SetCookie);
        Assert.Equal("Анна Смирнова", (await UserDirectoryTestData.FindAsync(_factory, sub))!.DisplayName);
    }

    [Fact]
    public async Task ConcurrentFirstSignIns_OfOnePerson_LeaveOneRow()
    {
        var sub = NewSub();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(async attempt =>
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<RecordSignInHandler>();

            return (await handler.Handle(
                new RecordSignInCommand(new SignInProfile(sub, "anna.s", "Анна", $"Смирнова{attempt}", null)),
                CancellationToken.None)).Outcome;
        }));

        Assert.Equal(1, outcomes.Count(outcome => outcome == SignInOutcome.Added));
        Assert.NotNull(await UserDirectoryTestData.FindAsync(_factory, sub));
    }

    private Task<HttpResponseMessage> RenewAsync(HttpClient client, SignedIn signedIn, string sub, Action<KeycloakTestRealm.TokenOptions> claims) =>
        RenewAsync(client, signedIn, sub, claims, signedIn.Cookie, signedIn.SetCookie!);

    // `cookie` is the pair to send; `setCookie` is the same session's Set-Cookie header, from which the cache key is derived.
    private async Task<HttpResponseMessage> RenewAsync(
        HttpClient client, SignedIn signedIn, string sub, Action<KeycloakTestRealm.TokenOptions> claims, string cookie, string setCookie)
    {
        // Stale cache: the sign-in cached a fresh access token, and the endpoint renews only when it is not.
        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<SessionAccessTokenCache>()
                .Set(SessionTestSupport.SessionKey(_factory, setCookie), "stale", DateTimeOffset.UtcNow);
        }

        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: KeycloakTestRealm.CreateToken(configure: t => t.Roles = ["user"]),
            idToken: KeycloakTokenEndpointResponder.BuildIdToken(signedIn.Sid, nonce: null, sub, claims),
            refreshToken: "rotated-refresh-token",
            expiresIn: 300,
            refreshExpiresIn: 1800);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/session/token") { Headers = { { "Cookie", cookie } } };

            return await client.SendAsync(request);
        }
        finally
        {
            _factory.TokenResponder.Reset();
        }
    }

    [Fact]
    public async Task Renewal_WithChangedClaims_UpdatesTheRow()
    {
        var client = _factory.CreateNoRedirectClient();
        var sub = NewSub();
        var signedIn = await SignInAsync(client, sub, Claims());

        var response = await RenewAsync(client, signedIn, sub, Claims(last: "Кузнецова"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("accessToken").GetString()));
        Assert.Equal("Анна Кузнецова", (await UserDirectoryTestData.FindAsync(_factory, sub))!.DisplayName);
    }

    // The ticket remembers the fingerprint of the claims it stored: unchanged claims do not touch the database.
    // Proof: the row is deleted behind the session's back, and a renewal with the same claims does not bring it back.
    [Fact]
    public async Task Renewal_WithUnchangedClaims_WritesNothing()
    {
        var client = _factory.CreateNoRedirectClient();
        var sub = NewSub();
        var signedIn = await SignInAsync(client, sub, Claims());

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
            await context.DirectoryUsers.Where(user => user.Id == sub).ExecuteDeleteAsync();
        }

        var response = await RenewAsync(client, signedIn, sub, Claims());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await UserDirectoryTestData.FindAsync(_factory, sub));
    }

    // A renewal that cannot record the claims never ends the session; the next renewal tries again.
    [Fact]
    public async Task Renewal_WhoseUpsertFails_KeepsTheSession()
    {
        var client = _factory.CreateNoRedirectClient();
        var sub = NewSub();
        var signedIn = await SignInAsync(client, sub, Claims());

        var response = await RenewAsync(client, signedIn, sub, Claims(first: Oversized()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Анна Смирнова", (await UserDirectoryTestData.FindAsync(_factory, sub))!.DisplayName);
    }

    // QA (tech-025): ADR-0034 keeps no unique key on the claims; two people may share a username or an e-mail.
    [Fact]
    public async Task Callback_TwoDifferentPeopleWithTheSameUsernameAndEmail_GetTwoRows()
    {
        var client = _factory.CreateNoRedirectClient();
        var first = NewSub();
        var second = NewSub();

        var one = await SignInAsync(client, first, Claims());
        var two = await SignInAsync(client, second, Claims());

        Assert.NotNull(one.SetCookie);
        Assert.NotNull(two.SetCookie);
        Assert.NotNull(await UserDirectoryTestData.FindAsync(_factory, first));
        Assert.NotNull(await UserDirectoryTestData.FindAsync(_factory, second));
    }

    // QA (tech-025): a token with the username only (no names, no e-mail) is a valid sign-in; the name shown is the username.
    [Fact]
    public async Task Callback_NewUserWithOnlyAUsername_IsStored_AndShownByTheUsername()
    {
        var client = _factory.CreateNoRedirectClient();
        var sub = NewSub();
        var username = "only.name." + sub[^8..];

        var signedIn = await SignInAsync(client, sub, Claims(username: username, first: null, last: null, email: null));

        Assert.NotNull(signedIn.SetCookie);

        var row = await UserDirectoryTestData.FindAsync(_factory, sub);
        Assert.NotNull(row);
        Assert.Equal(username, row.DisplayName);
        Assert.True(string.IsNullOrEmpty(row.Email));
    }

    // QA (tech-025): the member search (IUserDirectory.SearchAsync) finds a person only after the sign-in.
    [Fact]
    public async Task Search_FindsThePerson_OnlyAfterTheSignIn()
    {
        var client = _factory.CreateNoRedirectClient();
        var sub = NewSub();
        var surname = "Поиск" + sub[^12..];

        async Task<bool> FoundAsync()
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var directory = scope.ServiceProvider.GetRequiredService<IUserDirectory>();

            return (await directory.SearchAsync(new UserSearch(surname), CancellationToken.None)).Users.Any(user => user.Id == sub);
        }

        Assert.False(await FoundAsync());

        await SignInAsync(client, sub, Claims(last: surname));

        Assert.True(await FoundAsync());
    }

    // QA (tech-025): a renewal that records changed claims also stores their fingerprint, so the next renewal is silent.
    [Fact]
    public async Task Renewal_AfterAChangedRenewal_WithTheSameNewClaims_WritesNothing()
    {
        var client = _factory.CreateNoRedirectClient();
        var sub = NewSub();
        var signedIn = await SignInAsync(client, sub, Claims());

        var first = await RenewAsync(client, signedIn, sub, Claims(last: "Кузнецова"));
        var renewedCookie = SessionTestSupport.SessionCookie(first);
        Assert.NotNull(renewedCookie);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
            await context.DirectoryUsers.Where(user => user.Id == sub).ExecuteDeleteAsync();
        }

        var second = await RenewAsync(client, signedIn, sub, Claims(last: "Кузнецова"), renewedCookie.Split(';')[0], renewedCookie);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Null(await UserDirectoryTestData.FindAsync(_factory, sub));
    }

    // QA (tech-025): a failed upsert stores no fingerprint, so the next renewal tries again.
    [Fact]
    public async Task Renewal_AfterAFailedUpsert_TriesAgain()
    {
        var client = _factory.CreateNoRedirectClient();
        var sub = NewSub();
        var signedIn = await SignInAsync(client, sub, Claims());

        var failed = await RenewAsync(client, signedIn, sub, Claims(first: Oversized()));
        var renewedCookie = SessionTestSupport.SessionCookie(failed);
        Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        Assert.NotNull(renewedCookie);
        Assert.Equal("Анна Смирнова", (await UserDirectoryTestData.FindAsync(_factory, sub))!.DisplayName);

        var retried = await RenewAsync(client, signedIn, sub, Claims(last: "Иванова"), renewedCookie.Split(';')[0], renewedCookie);

        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal("Анна Иванова", (await UserDirectoryTestData.FindAsync(_factory, sub))!.DisplayName);
    }

    // QA (tech-025): a renewed token that lost its username neither ends the session nor blanks the stored row.
    [Fact]
    public async Task Renewal_WithoutAUsername_KeepsTheSession_AndTheStoredRow()
    {
        var client = _factory.CreateNoRedirectClient();
        var sub = NewSub();
        var signedIn = await SignInAsync(client, sub, Claims());

        var response = await RenewAsync(client, signedIn, sub, Claims(username: ""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var row = await UserDirectoryTestData.FindAsync(_factory, sub);
        Assert.Equal("anna.s", row!.Username);
        Assert.Equal("Анна Смирнова", row.DisplayName);
    }

    // QA (tech-025): a person disabled in the push era comes back at the next sign-in (D-5), through the real callback.
    [Fact]
    public async Task Callback_LegacyDisabledRow_IsReactivatedByTheSignIn()
    {
        var client = _factory.CreateNoRedirectClient();
        var sub = NewSub();
        await UserDirectoryTestData.SeedAsync(
            _factory, [UserDirectoryTestData.Person(sub, "Анна", "Смирнова", "anna.s@example.test", enabled: false, username: "anna.s")]);
        Assert.False((await UserDirectoryTestData.FindAsync(_factory, sub))!.IsActive);

        var signedIn = await SignInAsync(client, sub, Claims());

        Assert.NotNull(signedIn.SetCookie);
        Assert.True((await UserDirectoryTestData.FindAsync(_factory, sub))!.IsActive);
    }
}
