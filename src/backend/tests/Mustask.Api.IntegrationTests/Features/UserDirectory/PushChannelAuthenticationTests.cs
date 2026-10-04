using System.Net;
using Mustask.Api.Features.UserDirectory;
using Mustask.Domain.Identity;
using Mustask.Domain.UserDirectory;
using Microsoft.AspNetCore.Hosting;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014 US-6: only a request signed with the shared credential changes the
/// projection (BR-UP-10; api-contract.md B.2). Every refusal is the same
/// 401, changes nothing, and is logged without a name, an e-mail or the secret.
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class PushChannelAuthenticationTests(MustaskApiFactory factory) : IAsyncLifetime
{
    private readonly PushTestClient _push = new(factory);

    private string _annaId = string.Empty;

    public async Task InitializeAsync()
    {
        await ResetAsync(factory);
        _annaId = NewId();
        await _push.PostChangesAsync(DisableAnna(BaseMoment) with { Enabled = true });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private UserPush DisableAnna(long moment) =>
        Push(_annaId, "Анна", "Кузнецова", "anna.k@example.test", enabled: false, username: "a.kuznetsova", moment: moment);

    private string DisablingBody() => PushTestClient.ChangesBody(DisableAnna(BaseMoment + 1));

    private Task AssertRefusedAndUnchangedAsync(HttpRequestMessage request) =>
        AssertRefusedAndUnchangedAsync(_push, request);

    private async Task AssertRefusedAndUnchangedAsync(PushTestClient push, HttpRequestMessage request)
    {
        using var response = await push.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(UserDirectoryErrorCodes.PushUnauthorized, await PushTestClient.ErrorCodeAsync(response));
        Assert.True((await LookupAsync(factory, _annaId)).Known[_annaId].IsActive, "A refused push changed the projection.");
    }

    // US-6 "A push without the credential is refused" (and logged without her name or e-mail).
    [Fact]
    public async Task Push_WithoutCredential_IsRefused_AndTheRefusalLogNamesNoPerson()
    {
        var output = await ConsoleCapture.RunAsync(factory, push =>
            AssertRefusedAndUnchangedAsync(push, PushTestClient.Build(HttpMethod.Post, "changes", DisablingBody(), signed: false)));

        Assert.Contains("User directory push refused (MissingHeaders)", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Кузнецова", output, StringComparison.Ordinal);
        Assert.DoesNotContain("anna.k@example.test", output, StringComparison.Ordinal);
    }

    // US-6 "A push with a wrong credential is refused".
    [Fact]
    public async Task Push_WithAWrongCredential_IsRefused()
    {
        await AssertRefusedAndUnchangedAsync(
            PushTestClient.Build(HttpMethod.Post, "changes", DisablingBody(), secret: "a-wrong-secret-that-is-long-enough-0123456789"));
    }

    // US-6 "A Keycloak access token is not a push credential" — an ordinary user's, and one holding every instance-level code.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Push_WithAKeycloakAccessToken_IsRefused(bool everyCode)
    {
        var token = KeycloakTestRealm.CreateToken(everyCode ? PermissionCatalog.AllCodes : []);
        var request = PushTestClient.Build(HttpMethod.Post, "changes", DisablingBody(), signed: false).WithBearer(token);

        await AssertRefusedAndUnchangedAsync(request);
    }

    [Fact]
    public async Task Push_WithABearerTokenBesideAWrongSignature_IsRefused()
    {
        var token = KeycloakTestRealm.CreateToken(PermissionCatalog.AllCodes);
        var request = PushTestClient.Build(HttpMethod.Post, "changes", DisablingBody(), secret: "another-secret-entirely-0123456789abcdef").WithBearer(token);

        await AssertRefusedAndUnchangedAsync(request);
    }

    [Theory]
    [InlineData(-301)]
    [InlineData(301)]
    public async Task Push_TimestampOutsideTheWindow_IsRefused(int offsetSeconds)
    {
        await AssertRefusedAndUnchangedAsync(
            PushTestClient.Build(HttpMethod.Post, "changes", DisablingBody(), at: DateTimeOffset.UtcNow.AddSeconds(offsetSeconds)));
    }

    [Theory]
    [InlineData(-290)]
    [InlineData(290)]
    public async Task Push_TimestampInsideTheWindow_IsAccepted(int offsetSeconds)
    {
        using var response = await _push.SendAsync(
            PushTestClient.Build(HttpMethod.Post, "changes", DisablingBody(), at: DateTimeOffset.UtcNow.AddSeconds(offsetSeconds)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await LookupAsync(factory, _annaId)).Known[_annaId].IsActive);
    }

    [Fact]
    public async Task Push_SignatureForAnotherOperation_IsRefused()
    {
        var body = DisablingBody();
        var signedForSnapshot = PushTestClient.Build(HttpMethod.Put, $"snapshots/{Guid.NewGuid()}", body);
        var moved = PushTestClient.Build(HttpMethod.Post, "changes", body, signed: false);
        moved.Headers.Add(PushSignatureVerifier.TimestampHeader, signedForSnapshot.Headers.GetValues(PushSignatureVerifier.TimestampHeader));
        moved.Headers.Add(PushSignatureVerifier.SignatureHeader, signedForSnapshot.Headers.GetValues(PushSignatureVerifier.SignatureHeader));

        await AssertRefusedAndUnchangedAsync(moved);
    }

    [Fact]
    public async Task Push_AlteredBody_IsRefused()
    {
        var original = PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(DisableAnna(BaseMoment + 1) with { Enabled = true }));
        var forged = PushTestClient.Build(HttpMethod.Post, "changes", DisablingBody(), signed: false);
        forged.Headers.Add(PushSignatureVerifier.TimestampHeader, original.Headers.GetValues(PushSignatureVerifier.TimestampHeader));
        forged.Headers.Add(PushSignatureVerifier.SignatureHeader, original.Headers.GetValues(PushSignatureVerifier.SignatureHeader));

        await AssertRefusedAndUnchangedAsync(forged);
    }

    [Theory]
    [InlineData("v1=ABCDEF")]
    [InlineData("v2=0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task Push_MalformedSignatureHeader_IsRefused(string signature)
    {
        var request = PushTestClient.Build(HttpMethod.Post, "changes", DisablingBody(), signed: false);
        request.Headers.Add(PushSignatureVerifier.TimestampHeader, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation(PushSignatureVerifier.SignatureHeader, signature);

        await AssertRefusedAndUnchangedAsync(request);
    }

    [Fact]
    public async Task Push_UppercaseHexSignature_IsRefused()
    {
        var request = PushTestClient.Build(HttpMethod.Post, "changes", DisablingBody());
        var signature = request.Headers.GetValues(PushSignatureVerifier.SignatureHeader).Single();
        request.Headers.Remove(PushSignatureVerifier.SignatureHeader);
        request.Headers.TryAddWithoutValidation(PushSignatureVerifier.SignatureHeader, "v1=" + signature[3..].ToUpperInvariant());

        await AssertRefusedAndUnchangedAsync(request);
    }

    /// <summary>ADR-0025 § 3, rotation: the previous secret is accepted too while it is configured.</summary>
    [Fact]
    public async Task Push_SignedWithThePreviousSecret_IsAcceptedWhileItIsConfigured()
    {
        const string previous = "the-previous-test-secret-0123456789abcdef";
        Environment.SetEnvironmentVariable("UserDirectory__Push__PreviousSecret", previous);

        try
        {
            using var rotating = factory.WithWebHostBuilder(_ => { });
            using var client = rotating.CreateClient();

            using var response = await client.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", DisablingBody(), secret: previous));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("UserDirectory__Push__PreviousSecret", null);
        }

        // Without it, the same secret is refused again.
        await AssertRefusedAndUnchangedAfterReEnableAsync(previous);
    }

    private async Task AssertRefusedAndUnchangedAfterReEnableAsync(string previous)
    {
        await _push.PostChangesAsync(DisableAnna(BaseMoment + 2) with { Enabled = true });
        await AssertRefusedAndUnchangedAsync(
            PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(DisableAnna(BaseMoment + 3)), secret: previous));
    }

    /// <summary>The credential is required at startup: a missing secret fails the start naming the key, never a value.</summary>
    [Fact]
    public void MissingPushSecret_HostDoesNotStart_NamesTheKey()
    {
        var previous = Environment.GetEnvironmentVariable("UserDirectory__Push__Secret");
        Environment.SetEnvironmentVariable("UserDirectory__Push__Secret", null);

        try
        {
            using var host = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

            var exception = Assert.Throws<InvalidOperationException>(() => host.CreateClient());
            Assert.StartsWith("Missing configuration value UserDirectory:Push:Secret", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("UserDirectory__Push__Secret", previous);
        }
    }

    [Fact]
    public void ShortPushSecret_HostDoesNotStart_AndTheValueIsNotInTheMessage()
    {
        var previous = Environment.GetEnvironmentVariable("UserDirectory__Push__Secret");
        const string tooShort = "short-secret-value";
        Environment.SetEnvironmentVariable("UserDirectory__Push__Secret", tooShort);

        try
        {
            using var host = factory.WithWebHostBuilder(_ => { });

            var exception = Assert.Throws<InvalidOperationException>(() => host.CreateClient());
            Assert.Contains("UserDirectory:Push:Secret", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(tooShort, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("UserDirectory__Push__Secret", previous);
        }
    }
}
