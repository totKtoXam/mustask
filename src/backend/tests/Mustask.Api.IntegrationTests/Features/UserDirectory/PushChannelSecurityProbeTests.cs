using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Mustask.Api.Features.UserDirectory;
using Mustask.Domain.UserDirectory;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014 L security pass (review.md, "Security pass — backend"): probes of
/// the push channel the developer's and QA's tests left open — bodies the
/// JSON reader rejects below the shape check (a lone surrogate escape as the
/// provider's Jackson writes it, invalid UTF-8, nesting past the reader's
/// depth) are a 400, never a 500; a signature header sent twice is refused;
/// a captured signature does not carry over to another spelling of the same
/// route.
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class PushChannelSecurityProbeTests(MustaskApiFactory factory) : IAsyncLifetime
{
    private readonly PushTestClient _push = new(factory);

    public Task InitializeAsync() => ResetAsync(factory);

    public Task DisposeAsync() => ResetAsync(factory);

    /// <summary>A request signed over exactly <paramref name="body"/>'s bytes, as the provider signs.</summary>
    private static HttpRequestMessage SignedBytes(string target, byte[] body, string path)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var canonical = PushSignatureVerifier.CanonicalString(timestamp, "POST", target, body);

        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        request.Headers.Add(PushSignatureVerifier.TimestampHeader, timestamp);
        request.Headers.Add(PushSignatureVerifier.SignatureHeader, PushSignatureVerifier.Sign(MustaskApiFactory.TestPushSecret, canonical));

        return request;
    }

    [Theory]
    [InlineData("lone surrogate")]
    [InlineData("invalid UTF-8")]
    [InlineData("nesting past the reader's depth")]
    public async Task Signed_BodyTheJsonReaderRejects_IsMalformed_Not500_AndChangesNothing(string kind)
    {
        var id = NewId();
        var prefix = Encoding.UTF8.GetBytes($$"""{"changes":[{"id":"{{id}}","moment":{{BaseMoment}},"username":"u","enabled":true,"firstName":""");
        var suffix = Encoding.UTF8.GetBytes("\"}]}");

        var body = kind switch
        {
            // Exactly what the provider sends for a first name Keycloak's account REST accepted with a lone surrogate.
            "lone surrogate" => [.. prefix, .. Encoding.UTF8.GetBytes("Сам\\uD800"), .. suffix],
            "invalid UTF-8" => [.. prefix, 0xC3, 0x28, 0xFF, .. suffix],
            _ => Encoding.UTF8.GetBytes(
                $$"""{"changes":[{"id":"{{id}}","moment":{{BaseMoment}},"username":"u","enabled":true}],"deep":"""
                + new string('[', 5_000) + new string(']', 5_000) + "}"),
        };

        using var response = await _push.SendAsync(SignedBytes("changes", body, PushTestClient.FeedBase + "changes"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(UserDirectoryErrorCodes.PushMalformed, await PushTestClient.ErrorCodeAsync(response));
        Assert.Equal(0, await CountAsync(factory));
    }

    [Fact]
    public async Task Signed_SignatureHeaderSentTwice_IsRefused()
    {
        var id = NewId();
        var request = PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(Push(id, "Анна", "Дважды", null)));
        var signature = request.Headers.GetValues(PushSignatureVerifier.SignatureHeader).Single();
        request.Headers.TryAddWithoutValidation(PushSignatureVerifier.SignatureHeader, signature);

        using var response = await _push.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty((await LookupAsync(factory, id)).Known);
    }

    // The target in the canonical string is the path exactly as sent: a captured signature does not carry over to
    // another spelling the router would still match (a trailing slash), so a replay there is refused.
    [Fact]
    public async Task CapturedSignature_OnAnotherSpellingOfTheSameRoute_IsRefused()
    {
        var id = NewId();
        var body = Encoding.UTF8.GetBytes(PushTestClient.ChangesBody(Push(id, "Анна", "Слеш", null)));
        var captured = SignedBytes("changes", body, PushTestClient.FeedBase + "changes");

        var moved = new HttpRequestMessage(HttpMethod.Post, PushTestClient.FeedBase + "changes/")
        {
            Content = captured.Content,
        };
        moved.Headers.Add(PushSignatureVerifier.TimestampHeader, captured.Headers.GetValues(PushSignatureVerifier.TimestampHeader));
        moved.Headers.Add(PushSignatureVerifier.SignatureHeader, captured.Headers.GetValues(PushSignatureVerifier.SignatureHeader));

        using var response = await _push.SendAsync(moved);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty((await LookupAsync(factory, id)).Known);
    }
}
