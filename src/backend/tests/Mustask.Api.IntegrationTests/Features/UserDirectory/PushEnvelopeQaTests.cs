using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.Features.UserDirectory;
using Mustask.Domain.UserDirectory;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// QA re-check 3 (qa.md, RC3-18..RC3-23): the security pass's S3 fix — the push binder checks the envelope (both
/// headers, well-formed, the timestamp in its window) before it reads the body — walked past the three shapes
/// <c>PushEnvelopeBeforeBodyTests</c> uses. Every way an envelope can fail, on every route, with a body over the
/// limit: 401, the same answer as any other refusal, and the server's side of the request stream read 0 bytes. A
/// well-formed envelope with a wrong signature is still refused 401 after the body is read; the body limit is still
/// exactly 4 MiB; and a client that sends a large unsigned body to a real Kestrel gets the 401.
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class PushEnvelopeQaTests(MustaskApiFactory factory) : IAsyncLifetime
{
    private const int MaxBody = 4 * 1024 * 1024;

    private const string Snapshot = "snapshots/5b3bdb90-5d0a-4a89-8a24-0d1f2a4c6e11";

    private static readonly Lazy<string> OverTheLimit = new(() => PaddedTo(MaxBody + 1));

    private readonly BodyBytesRead _counter = new();

    private WebApplicationFactory<Program> _host = null!;

    private PushTestClient _push = null!;

    public async Task InitializeAsync()
    {
        await ResetAsync(factory);

        _host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter>(new CountingBodyStartupFilter(_counter))));
        _push = new PushTestClient(_host.CreateClient());
    }

    public Task DisposeAsync()
    {
        _host.Dispose();

        return Task.CompletedTask;
    }

    /// <summary>A JSON body of exactly <paramref name="bytes"/> bytes: one valid entry for B-1, then padding.</summary>
    private static string PaddedTo(int bytes)
    {
        var start = "{\"changes\":[],\"padding\":\"";
        const string End = "\"}";

        return start + new string('x', bytes - start.Length - End.Length) + End;
    }

    private static string Now(long offsetSeconds = 0) =>
        (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + offsetSeconds).ToString(CultureInfo.InvariantCulture);

    private static string Hex64 => "v1=" + new string('a', 64);

    public static TheoryData<string> BadEnvelopes => new()
    {
        "no-headers",
        "timestamp-only",
        "signature-only",
        "ts:abc",
        "ts:empty",
        "ts:fraction",
        "ts:negative",
        "ts:plus-sign",
        "ts:overflow",
        "ts:hex",
        "ts:scientific",
        "ts:fullwidth-digits",
        "sig:no-prefix",
        "sig:upper-case-hex",
        "sig:upper-case-prefix",
        "sig:v2",
        "sig:63-digits",
        "sig:65-digits",
        "sig:non-hex",
        "sig:empty",
        "stale-past",
        "stale-future",
        "timestamp-twice",
        "signature-twice",
        "bearer-only",
        "bearer-and-malformed-signature",
    };

    public static TheoryData<string, string> Routes => new()
    {
        { "POST", "changes" },
        { "PUT", Snapshot },
        { "PUT", Snapshot + "/pages/0" },
        { "PUT", Snapshot + "/completion" },
    };

    /// <summary>Builds the request of one named way the envelope fails; the body is whatever the caller gives.</summary>
    private static HttpRequestMessage Broken(string shape, string method, string target, string body)
    {
        var request = PushTestClient.Build(new HttpMethod(method), target, body, signed: false);
        var headers = request.Headers;

        void Add(string name, string value) => headers.TryAddWithoutValidation(name, value);

        string Signed(long offset)
        {
            var timestamp = Now(offset);

            Add(PushSignatureVerifier.TimestampHeader, timestamp);

            return PushSignatureVerifier.Sign(
                MustaskApiFactory.TestPushSecret,
                PushSignatureVerifier.CanonicalString(timestamp, method, target, Encoding.UTF8.GetBytes(body)));
        }

        switch (shape)
        {
            case "no-headers":
                break;
            case "timestamp-only":
                Add(PushSignatureVerifier.TimestampHeader, Now());
                break;
            case "signature-only":
                Add(PushSignatureVerifier.SignatureHeader, Hex64);
                break;
            case var _ when shape.StartsWith("ts:", StringComparison.Ordinal):
                Add(PushSignatureVerifier.TimestampHeader, shape switch
                {
                    "ts:abc" => "abc",
                    "ts:empty" => string.Empty,
                    "ts:fraction" => Now() + ".5",
                    "ts:negative" => "-1",
                    "ts:plus-sign" => "+" + Now(),
                    "ts:overflow" => "99999999999999999999",
                    "ts:hex" => "0x" + long.Parse(Now(), CultureInfo.InvariantCulture).ToString("x", CultureInfo.InvariantCulture),
                    "ts:scientific" => "1.79e9",
                    _ => "１７９００００００００",
                });
                Add(PushSignatureVerifier.SignatureHeader, Hex64);
                break;
            case var _ when shape.StartsWith("sig:", StringComparison.Ordinal):
                Add(PushSignatureVerifier.TimestampHeader, Now());
                Add(PushSignatureVerifier.SignatureHeader, shape switch
                {
                    "sig:no-prefix" => new string('a', 64),
                    "sig:upper-case-hex" => "v1=" + new string('A', 64),
                    "sig:upper-case-prefix" => "V1=" + new string('a', 64),
                    "sig:v2" => "v2=" + new string('a', 64),
                    "sig:63-digits" => "v1=" + new string('a', 63),
                    "sig:65-digits" => "v1=" + new string('a', 65),
                    "sig:non-hex" => "v1=" + new string('g', 64),
                    _ => string.Empty,
                });
                break;
            case "stale-past":
                Add(PushSignatureVerifier.SignatureHeader, Signed(-400));
                break;
            case "stale-future":
                Add(PushSignatureVerifier.SignatureHeader, Signed(400));
                break;
            case "timestamp-twice":
                Add(PushSignatureVerifier.TimestampHeader, Now());
                Add(PushSignatureVerifier.TimestampHeader, Now());
                Add(PushSignatureVerifier.SignatureHeader, Hex64);
                break;
            case "signature-twice":
                Add(PushSignatureVerifier.TimestampHeader, Now());
                Add(PushSignatureVerifier.SignatureHeader, Hex64);
                Add(PushSignatureVerifier.SignatureHeader, Hex64);
                break;
            case "bearer-only":
                headers.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.token");
                break;
            case "bearer-and-malformed-signature":
                headers.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.token");
                Add(PushSignatureVerifier.TimestampHeader, Now());
                Add(PushSignatureVerifier.SignatureHeader, "v1=zz");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(shape), shape, null);
        }

        return request;
    }

    private async Task AssertUnauthorizedAndUnreadAsync(HttpRequestMessage request, string what)
    {
        _counter.Bytes = -1;
        using var response = await _push.SendAsync(request);

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{what}: answered {(int)response.StatusCode}");
        Assert.Equal(UserDirectoryErrorCodes.PushUnauthorized, await PushTestClient.ErrorCodeAsync(response));
        Assert.True(_counter.Bytes == 0, $"{what}: the server read {_counter.Bytes} bytes of the body");
    }

    // RC3-18: every shape of a bad envelope, B-1, a body one byte over the limit: 401, and not a byte of it read.
    [Theory]
    [MemberData(nameof(BadEnvelopes))]
    public Task ABadEnvelope_WithABodyOverTheLimit_IsUnauthorized_AndTheBodyIsNotRead(string shape) =>
        AssertUnauthorizedAndUnreadAsync(Broken(shape, "POST", "changes", OverTheLimit.Value), shape);

    // RC3-18: the same on all four routes — the early check is in the binder every route shares.
    [Theory]
    [MemberData(nameof(Routes))]
    public async Task EveryRoute_RefusesAnUnsignedStaleOrMalformedRequest_BeforeItsBody(string method, string target)
    {
        foreach (var shape in new[] { "no-headers", "ts:abc", "sig:63-digits", "stale-past", "bearer-only" })
        {
            await AssertUnauthorizedAndUnreadAsync(Broken(shape, method, target, OverTheLimit.Value), $"{method} {target} {shape}");
        }
    }

    // RC3-18: no Content-Length to go by — a chunked body is not read either.
    [Fact]
    public async Task AChunkedUnsignedBody_IsUnauthorized_AndNotRead()
    {
        var request = PushTestClient.Build(HttpMethod.Post, "changes", string.Empty, signed: false);
        request.Content = new StreamContent(new PatternStream(8L * 1024 * 1024));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.TransferEncodingChunked = true;

        await AssertUnauthorizedAndUnreadAsync(request, "chunked 8 MiB, unsigned");
    }

    // RC3-19: a well-formed envelope with the wrong HMAC — the secret, the body, the route — is read to the end and
    // refused 401; nothing is written. (The contract: the body is "read and verified before it is parsed".)
    [Fact]
    public async Task AWellFormedEnvelope_WithAWrongSignature_IsReadAndThenRefused_401()
    {
        var body = PushTestClient.ChangesBody(Push(NewId(), "Анна", "Смирнова", null));
        var length = Encoding.UTF8.GetByteCount(body);

        var wrongSecret = PushTestClient.Build(HttpMethod.Post, "changes", body, secret: "another-secret-another-secret-0123456789");
        var alteredBody = PushTestClient.Build(HttpMethod.Post, "changes", body);
        alteredBody.Content = new StringContent(body + " ", Encoding.UTF8, "application/json");
        var otherRoute = PushTestClient.Build(HttpMethod.Post, "changes", body);
        var signatureOfAnotherRoute = PushSignatureVerifier.Sign(
            MustaskApiFactory.TestPushSecret,
            PushSignatureVerifier.CanonicalString(Now(), "POST", "snapshots/x", Encoding.UTF8.GetBytes(body)));
        otherRoute.Headers.Remove(PushSignatureVerifier.SignatureHeader);
        otherRoute.Headers.Remove(PushSignatureVerifier.TimestampHeader);
        otherRoute.Headers.TryAddWithoutValidation(PushSignatureVerifier.TimestampHeader, Now());
        otherRoute.Headers.TryAddWithoutValidation(PushSignatureVerifier.SignatureHeader, signatureOfAnotherRoute);

        foreach (var (request, what, expected) in new[] { (wrongSecret, "wrong secret", length), (alteredBody, "altered body", -1), (otherRoute, "another route's signature", length) })
        {
            _counter.Bytes = -1;
            using var response = await _push.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(UserDirectoryErrorCodes.PushUnauthorized, await PushTestClient.ErrorCodeAsync(response));
            Assert.True(_counter.Bytes > 0 && (expected < 0 || _counter.Bytes == expected), $"{what}: the server read {_counter.Bytes} bytes, the body is {length}");
        }

        Assert.Equal(0, await CountAsync(factory));
    }

    // RC3-19: over the limit WITH a well-formed envelope — the contract's 400 `push_malformed`, the signature not yet
    // checked (so a wrong secret and a right one are told apart by nothing here).
    [Fact]
    public async Task AWellFormedEnvelope_WithABodyOverTheLimit_IsMalformed_WhateverTheSignature()
    {
        foreach (var secret in new[] { MustaskApiFactory.TestPushSecret, "another-secret-another-secret-0123456789" })
        {
            using var response = await _push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", OverTheLimit.Value, secret: secret));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(UserDirectoryErrorCodes.PushMalformed, await PushTestClient.ErrorCodeAsync(response));
        }
    }

    // RC3-19: the bound is exactly 4 MiB — a correctly signed body of 4 194 304 bytes is read and applied, one more is
    // malformed (the read loop was not changed by the fix, but the early step sits right above it).
    [Fact]
    public async Task TheBodyBound_IsExactly4MiB_ForASignedRequest()
    {
        var id = NewId();
        var entry = PushTestClient.ChangesBody(Push(id, "Анна", "Смирнова", null));

        // A valid JSON of exactly MaxBody bytes: the entry, then whitespace as padding.
        var json = entry + new string(' ', MaxBody - Encoding.UTF8.GetByteCount(entry));
        Assert.Equal(MaxBody, Encoding.UTF8.GetByteCount(json));

        using (var accepted = await _push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", json)))
        {
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        Assert.NotNull(await FindAsync(factory, id));

        using var over = await _push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", json + " "));
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
    }

    // RC3-20: a request that was valid before is valid now, near both edges of the window and on all four routes in a row
    // (+300 s is the future edge, −290 leaves a margin for the clock's second to tick between signing and checking;
    // the exact edges are `PushTimestampWindowTests`' on a fixed clock).
    [Fact]
    public async Task TheFourOperations_AreAcceptedAtTheEdgesOfTheWindow()
    {
        var snapshot = Guid.NewGuid();
        var entry = Push(NewId(), "Анна", "Смирнова", null, moment: BaseMoment + 1);

        using (var begun = await _push.SendAsync(PushTestClient.Build(
            HttpMethod.Put, $"snapshots/{snapshot}", JsonSerializer.Serialize(new { enumerationStart = BaseMoment + 1_000 }), at: DateTimeOffset.UtcNow.AddSeconds(300))))
        {
            Assert.Equal(HttpStatusCode.OK, begun.StatusCode);
        }

        using (var page = await _push.SendAsync(PushTestClient.Build(
            HttpMethod.Put, $"snapshots/{snapshot}/pages/0", PushTestClient.PageBody(entry), at: DateTimeOffset.UtcNow.AddSeconds(-290))))
        {
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        }

        using (var completed = await _push.SendAsync(PushTestClient.Build(
            HttpMethod.Put, $"snapshots/{snapshot}/completion", JsonSerializer.Serialize(new { pageCount = 1, entryCount = 1 }), at: DateTimeOffset.UtcNow.AddSeconds(300))))
        {
            Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        }

        using var changes = await _push.SendAsync(PushTestClient.Build(
            HttpMethod.Post, "changes", PushTestClient.ChangesBody(Push(NewId(), "Иван", "Новиков", null)), at: DateTimeOffset.UtcNow.AddSeconds(-290)));
        Assert.Equal(HttpStatusCode.OK, changes.StatusCode);
    }

    // RC3-21: whichever way a request is refused, the answer is the same: status, content type, error code, title and
    // detail, and the set of headers — the answer does not say which check failed. The bodies are compared with the
    // trace id taken out.
    [Fact]
    public async Task EveryRefusal_LooksTheSame_ToTheCaller()
    {
        var body = PushTestClient.ChangesBody(Push(NewId(), "Анна", "Смирнова", null));
        var requests = new (string What, HttpRequestMessage Request)[]
        {
            ("no headers", Broken("no-headers", "POST", "changes", body)),
            ("malformed timestamp", Broken("ts:abc", "POST", "changes", body)),
            ("malformed signature", Broken("sig:63-digits", "POST", "changes", body)),
            ("stale timestamp", Broken("stale-past", "POST", "changes", body)),
            ("a bearer token", Broken("bearer-only", "POST", "changes", body)),
            ("wrong secret", PushTestClient.Build(HttpMethod.Post, "changes", body, secret: "another-secret-another-secret-0123456789")),
            ("a body over the limit, unsigned", Broken("no-headers", "POST", "changes", OverTheLimit.Value)),
        };

        var seen = new List<(string What, string Shape)>();

        foreach (var (what, request) in requests)
        {
            using var response = await _push.SendAsync(request);
            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            var properties = json.EnumerateObject()
                .Where(property => property.Name is not ("traceId" or "instance"))
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => $"{property.Name}={property.Value}");
            var headers = response.Headers.Concat(response.Content.Headers)
                .Where(header => header.Key is not ("Date" or "Server" or "Content-Length" or "traceparent"))
                .OrderBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
                .Select(header => $"{header.Key}:{string.Join(',', header.Value)}");

            seen.Add((what, $"{(int)response.StatusCode} | {string.Join("; ", properties)} | {string.Join("; ", headers)}"));
        }

        var distinct = seen.Select(item => item.Shape).Distinct(StringComparer.Ordinal).ToList();
        Assert.True(distinct.Count == 1, "the refusals differ:\n" + string.Join("\n", seen.Select(item => $"  {item.What}: {item.Shape}")));
    }

    // RC3-22: each refusal is logged with its reason and the route, without the body, a header's value or a secret, and
    // without a third line beside the two the review described (the verifier's and the business handler's).
    [Fact]
    public async Task ARefusedEnvelope_IsLoggedOnce_WithItsReasonAndRoute_NoValues()
    {
        var timestamp = Now();
        var signature = "v1=" + new string('b', 64);

        var output = await ConsoleCapture.RunAsync(factory, async push =>
        {
            for (var i = 0; i < 3; i++)
            {
                var request = PushTestClient.Build(HttpMethod.Post, "changes", new string('q', 1_000_000), signed: false);
                request.Headers.TryAddWithoutValidation(PushSignatureVerifier.TimestampHeader, timestamp);
                request.Headers.TryAddWithoutValidation(PushSignatureVerifier.SignatureHeader, signature.Replace("b", "c", StringComparison.Ordinal) + "!");
                using var response = await push.SendAsync(request);
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }
        });

        var warnings = output.Split('\n').Where(line => line.Contains("WRN]", StringComparison.Ordinal)).ToList();

        Assert.Equal(3, warnings.Count(line => line.Contains("push refused (MalformedHeader)", StringComparison.Ordinal)));
        Assert.All(warnings.Where(line => line.Contains("push refused", StringComparison.Ordinal)), line => Assert.Contains("/api/user-directory/feed/changes", line, StringComparison.Ordinal));
        Assert.True(warnings.Count <= 6, $"{warnings.Count} warnings for three refusals:\n{string.Join("\n", warnings)}");
        Assert.DoesNotContain(new string('c', 64), output, StringComparison.Ordinal);
        Assert.DoesNotContain("qqqqqqqq", output, StringComparison.Ordinal);
        Assert.DoesNotContain(MustaskApiFactory.TestPushSecret, output, StringComparison.Ordinal);
    }

    // RC3-23: over a real socket. The in-process host answers without a socket in the way; a real Kestrel has to answer
    // while the client is still sending, and a client must see the 401 and not a connection reset.
    [Fact]
    public async Task ARealKestrel_AnswersALargeUnsignedBody_WithTheUnauthorized_NotAResetConnection()
    {
        using var kestrel = factory.WithWebHostBuilder(_ => { });
        kestrel.UseKestrel(0);
        kestrel.StartServer();
        using var client = kestrel.CreateClient();

        // A real socket: Kestrel's own address, not the in-process host's `http://localhost/`.
        Assert.Equal("http", client.BaseAddress!.Scheme);
        Assert.NotEqual(80, client.BaseAddress.Port);

        for (var round = 0; round < 10; round++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, client.BaseAddress + "api/user-directory/feed/changes")
            {
                Content = new StringContent(OverTheLimit.Value, Encoding.UTF8, "application/json"),
            };
            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(UserDirectoryErrorCodes.PushUnauthorized, await PushTestClient.ErrorCodeAsync(response));
        }
    }

    /// <summary>What the server read of the last request's body.</summary>
    private sealed class BodyBytesRead
    {
        public long Bytes { get; set; }
    }

    private sealed class CountingBodyStartupFilter(BodyBytesRead counter) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, pipeline) =>
            {
                var counting = new CountingReadStream(context.Request.Body);
                context.Request.Body = counting;

                try
                {
                    await pipeline(context);
                }
                finally
                {
                    counter.Bytes = counting.BytesRead;
                }
            });

            next(app);
        };
    }

    private sealed class CountingReadStream(Stream inner) : Stream
    {
        public long BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Count(await inner.ReadAsync(buffer, cancellationToken));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private int Count(int read)
        {
            BytesRead += read;

            return read;
        }
    }

    /// <summary>A non-seekable stream of <paramref name="length"/> filler bytes — a body with no known length.</summary>
    private sealed class PatternStream(long length) : Stream
    {
        private long _position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = (int)Math.Min(count, length - _position);
            Array.Fill(buffer, (byte)'x', offset, n);
            _position += n;

            return n;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
