using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Mustask.Api.Features.UserDirectory;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory.Live;

/// <summary>
/// A stand-in for MUSTASK's push channel on a local port (Kestrel): records
/// every request the live provider sends, checks its signature exactly as
/// MUSTASK's verifier does, and answers what the test says — counts, a
/// refusal, a delay.
/// </summary>
internal sealed class SignatureCheckingReceiver : IAsyncDisposable
{
    private const string FeedPrefix = "/api/user-directory/feed/";

    private readonly ConcurrentQueue<ReceivedPush> _received = new();

    private readonly string _secret;

    private WebApplication? _app;

    public SignatureCheckingReceiver(string secret)
    {
        _secret = secret;
    }

    /// <summary>What the receiver answers next; 200 with zero counts by default.</summary>
    public Func<ReceivedPush, (int Status, TimeSpan Delay)> Answer { get; set; } = _ => (200, TimeSpan.Zero);

    public int Port { get; private set; }

    public IReadOnlyList<ReceivedPush> Received => _received.ToArray();

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        _app = builder.Build();
        _app.Map(FeedPrefix + "{**target}", HandleAsync);

        await _app.StartAsync();

        Port = new Uri(_app.Urls.First()).Port;
    }

    /// <summary>The user entries of every accepted request so far — changes and snapshot pages alike.</summary>
    public IEnumerable<JsonElement> EntriesFor(string userId) =>
        Received
            .Where(push => push.SignedRight && push.Answered == 200)
            .SelectMany(push => push.Entries)
            .Where(entry => entry.GetProperty("id").GetString() == userId);

    /// <summary>The user entries of accepted changes requests only — events, not reconciliation pages.</summary>
    public IEnumerable<JsonElement> ChangeEntriesFor(string userId) =>
        Received
            .Where(push => push.SignedRight && push.Answered == 200 && push.Target == "changes")
            .SelectMany(push => push.Entries)
            .Where(entry => entry.GetProperty("id").GetString() == userId);

    public async Task<JsonElement> WaitForEntryAsync(string userId, Func<JsonElement, bool> matches, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            foreach (var entry in EntriesFor(userId))
            {
                if (matches(entry))
                {
                    return entry;
                }
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"No matching push for user {userId} within {timeout.TotalSeconds:F0} s; received {Received.Count} request(s).");
    }

    public async Task<ReceivedPush> WaitForAsync(Func<ReceivedPush, bool> matches, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (Received.FirstOrDefault(matches) is { } push)
            {
                return push;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"No matching request within {timeout.TotalSeconds:F0} s; received {Received.Count} request(s).");
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private async Task HandleAsync(HttpContext context, string target)
    {
        using var buffer = new MemoryStream();
        await context.Request.Body.CopyToAsync(buffer);
        var body = buffer.ToArray();

        var timestamp = context.Request.Headers[PushSignatureVerifier.TimestampHeader].ToString();
        var signature = context.Request.Headers[PushSignatureVerifier.SignatureHeader].ToString();
        var signedRight = timestamp.Length > 0
            && signature == PushSignatureVerifier.Sign(_secret, PushSignatureVerifier.CanonicalString(timestamp, context.Request.Method, target, body));

        var push = new ReceivedPush(context.Request.Method, target, Encoding.UTF8.GetString(body), signedRight, DateTimeOffset.UtcNow);

        // As MUSTASK's binder does (SignedPushBody): a signed body System.Text.Json cannot read — an unpaired surrogate
        // escape among the cases — is a 400, whatever the test would have answered.
        var (status, delay) = !signedRight ? (401, TimeSpan.Zero)
            : !IsReadableJson(body) ? (400, TimeSpan.Zero)
            : Answer(push);

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay);
        }

        push.Answered = status;
        _received.Enqueue(push);

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(status switch
        {
            200 => CountsFor(push),
            400 => """{"errorCode":"user_directory.push_malformed"}""",
            _ => """{"errorCode":"user_directory.push_unauthorized"}""",
        });
    }

    /// <summary>
    /// Whether System.Text.Json can read the body into strings, as MUSTASK's binder does when it deserialises. Parsing
    /// alone is not enough: <see cref="JsonDocument"/> takes an unpaired surrogate escape and fails only when the
    /// string is read, so every name and value is read here.
    /// </summary>
    private static bool IsReadableJson(byte[] body)
    {
        try
        {
            var reader = new Utf8JsonReader(body);

            while (reader.Read())
            {
                if (reader.TokenType is JsonTokenType.PropertyName or JsonTokenType.String)
                {
                    _ = reader.GetString();
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    private static string CountsFor(ReceivedPush push)
    {
        var received = push.Entries.Count;

        return target(push) switch
        {
            "completion" => string.Create(CultureInfo.InvariantCulture,
                $$"""{"received":0,"added":0,"changed":0,"unchanged":0,"heldAsDeleted":0,"markedAbsent":0,"pageCount":0,"entryCount":0}"""),
            _ => string.Create(CultureInfo.InvariantCulture,
                $$"""{"received":{{received}},"added":0,"changed":0,"unchanged":{{received}},"heldAsDeleted":0}"""),
        };

        static string target(ReceivedPush push) => push.Target.EndsWith("/completion", StringComparison.Ordinal) ? "completion" : "other";
    }
}

/// <summary>One request as the receiver saw it.</summary>
internal sealed record ReceivedPush(string Method, string Target, string Body, bool SignedRight, DateTimeOffset At)
{
    public int Answered { get; set; }

    /// <summary>The user entries of a changes request or a snapshot page; empty otherwise.</summary>
    public IReadOnlyList<JsonElement> Entries
    {
        get
        {
            if (string.IsNullOrEmpty(Body))
            {
                return [];
            }

            JsonDocument document;

            try
            {
                document = JsonDocument.Parse(Body);
            }
            catch (JsonException)
            {
                // A body MUSTASK refuses as malformed carries no entries to speak of.
                return [];
            }

            using (document)
            {
                return Read(document.RootElement);
            }
        }
    }

    private static IReadOnlyList<JsonElement> Read(JsonElement root) =>
        root.TryGetProperty("changes", out var changes) ? changes.EnumerateArray().Select(entry => entry.Clone()).ToList()
            : root.TryGetProperty("entries", out var entries) ? entries.EnumerateArray().Select(entry => entry.Clone()).ToList()
            : [];
}
