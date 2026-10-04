using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Mustask.Api.Features.UserDirectory;
using Mustask.Domain.UserDirectory;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// Sends push requests the way the Keycloak provider does (api-contract.md
/// B.2): the body signed with <see cref="MustaskApiFactory.TestPushSecret"/>
/// over the canonical string, a timestamp of now. Each knob — no headers,
/// another secret, an old timestamp, a Bearer token — is what a refusal test
/// turns.
/// </summary>
internal sealed class PushTestClient
{
    public const string FeedBase = "/api/user-directory/feed/";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public PushTestClient(MustaskApiFactory factory)
        : this(factory.CreateClient())
    {
    }

    /// <summary>Over another host's client — <see cref="ConsoleCapture"/>'s own host.</summary>
    public PushTestClient(HttpClient http)
    {
        Http = http;
    }

    public HttpClient Http { get; }

    /// <summary>Builds a signed request; <see cref="SendAsync(HttpRequestMessage)"/> sends it (also twice, for a replay).</summary>
    public static HttpRequestMessage Build(
        HttpMethod method,
        string target,
        string body,
        string? secret = MustaskApiFactory.TestPushSecret,
        DateTimeOffset? at = null,
        bool signed = true)
    {
        var request = new HttpRequestMessage(method, FeedBase + target)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        if (signed)
        {
            var timestamp = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
            var canonical = PushSignatureVerifier.CanonicalString(timestamp, method.Method, target, Encoding.UTF8.GetBytes(body));

            request.Headers.Add(PushSignatureVerifier.TimestampHeader, timestamp);
            request.Headers.Add(PushSignatureVerifier.SignatureHeader, PushSignatureVerifier.Sign(secret!, canonical));
        }

        return request;
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request) => Http.SendAsync(request);

    /// <summary>A copy of a request with the same headers and body — a captured request sent again.</summary>
    public static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        var copy = new HttpRequestMessage(request.Method, request.RequestUri);

        foreach (var header in request.Headers)
        {
            copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        var body = await request.Content!.ReadAsStringAsync();
        copy.Content = new StringContent(body, Encoding.UTF8, "application/json");

        return copy;
    }

    public static string ChangesBody(params UserPush[] changes) =>
        JsonSerializer.Serialize(new { changes = changes.Select(Entry) }, Json);

    public static string PageBody(params UserPush[] entries) =>
        JsonSerializer.Serialize(new { entries = entries.Select(Entry) }, Json);

    public async Task<FeedCountsBody> PostChangesAsync(params UserPush[] changes)
    {
        using var response = await SendAsync(Build(HttpMethod.Post, "changes", ChangesBody(changes)));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<FeedCountsBody>(Json))!;
    }

    public Task<HttpResponseMessage> PutAsync(string target, object body) =>
        SendAsync(Build(HttpMethod.Put, target, JsonSerializer.Serialize(body, Json)));

    /// <summary>B-2: begin a snapshot.</summary>
    public Task<HttpResponseMessage> BeginAsync(Guid snapshotId, long enumerationStart) =>
        PutAsync($"snapshots/{snapshotId}", new { enumerationStart });

    /// <summary>B-3: one page of a snapshot.</summary>
    public Task<HttpResponseMessage> PageAsync(Guid snapshotId, int pageNumber, params UserPush[] entries) =>
        SendAsync(Build(HttpMethod.Put, $"snapshots/{snapshotId}/pages/{pageNumber}", PageBody(entries)));

    /// <summary>B-4: complete a snapshot.</summary>
    public Task<HttpResponseMessage> CompleteAsync(Guid snapshotId, int pageCount, int entryCount) =>
        PutAsync($"snapshots/{snapshotId}/completion", new { pageCount, entryCount });

    /// <summary>
    /// A whole reconciliation as the provider runs it (ADR-0025 § 5): begin
    /// with <paramref name="enumerationStart"/>, pages of at most 500 entries,
    /// completion with the counts sent. Each step must succeed.
    /// </summary>
    public async Task<SnapshotCountsBody> ReconcileAsync(long enumerationStart, IReadOnlyList<UserPush> entries)
    {
        var snapshotId = Guid.NewGuid();

        using (var begun = await BeginAsync(snapshotId, enumerationStart))
        {
            begun.EnsureSuccessStatusCode();
        }

        var pages = entries.Chunk(UserDirectoryLimits.EntriesPerPush).ToList();

        for (var page = 0; page < pages.Count; page++)
        {
            using var applied = await PageAsync(snapshotId, page, pages[page]);
            applied.EnsureSuccessStatusCode();
        }

        using var completed = await CompleteAsync(snapshotId, pages.Count, entries.Count);
        completed.EnsureSuccessStatusCode();

        return (await completed.Content.ReadFromJsonAsync<SnapshotCountsBody>(Json))!;
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    private static object Entry(UserPush push) => push.Deleted
        ? new { push.Id, push.Moment, push.Username, push.FirstName, push.LastName, push.Email, push.Enabled, deleted = true }
        : new { push.Id, push.Moment, push.Username, push.FirstName, push.LastName, push.Email, push.Enabled, deleted = false };

    public static async Task<string> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("errorCode").GetString()!;
    }

    public static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.Clone();
    }
}

/// <summary><c>FeedCounts</c> as the client reads it.</summary>
internal sealed record FeedCountsBody(int Received, int Added, int Changed, int Unchanged, int HeldAsDeleted);

/// <summary><c>SnapshotState</c> as the client reads it.</summary>
internal sealed record SnapshotStateBody(Guid SnapshotId, long EnumerationStart, string State);

/// <summary><c>SnapshotCounts</c> as the client reads it.</summary>
internal sealed record SnapshotCountsBody(
    int Received,
    int Added,
    int Changed,
    int Unchanged,
    int HeldAsDeleted,
    int MarkedAbsent,
    int PageCount,
    int EntryCount);
