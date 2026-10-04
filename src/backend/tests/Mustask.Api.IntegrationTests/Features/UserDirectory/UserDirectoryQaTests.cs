using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using Mustask.Api.Features.UserDirectory;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Identity;
using Mustask.Domain.UserDirectory;
using Mustask.Shared.Exceptions;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014, tests added at QA (qa.md, Backend, "Additional scenarios"): the
/// boundaries and concurrency cases the developer's tests left open — the
/// 20/21 edge of a search answer, repeats counting toward the 500 of a
/// restriction, timestamps that are not decimal integers, a valid signature
/// beside a Bearer token, a page and a change for one user at the same time,
/// two snapshots begun at the same time, and a complete snapshot that marks
/// the whole projection absent at the NFR volume. Re-check 1 adds the entry
/// fields at and beyond their limits (A-19).
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class UserDirectoryQaTests(MustaskApiFactory factory) : IAsyncLifetime
{
    private const int Repetitions = 20;

    private readonly PushTestClient _push = new(factory);

    public Task InitializeAsync() => ResetAsync(factory);

    public Task DisposeAsync() => ResetAsync(factory);

    // qa.md A-10: exactly 20 matches is not "more matched"; 21 is.
    [Theory]
    [InlineData(20, false)]
    [InlineData(21, true)]
    public async Task Search_AtTheEdgeOfTheAnswerSize_SaysMoreMatchedOnlyAboveTwenty(int matches, bool moreMatched)
    {
        await SeedAsync(factory, Enumerable.Range(0, matches).Select(i => Push(NewId(), "Край", $"Ответа{i:D2}", null)));

        var result = await SearchAsync(factory, new UserSearch("край"));

        Assert.Equal(UserDirectoryLimits.SearchAnswerSize, result.Users.Count);
        Assert.Equal(moreMatched, result.MoreMatched);
    }

    // qa.md A-6, BR-UP-14: a repeated id counts toward the 500 — 501 entries made of repeats are refused,
    // 500 entries with repeats are answered like their distinct ids.
    [Fact]
    public async Task Search_RestrictionOfRepeatedIds_CountsEveryEntryTowardTheLimit()
    {
        var anna = NewId();
        await SeedAsync(factory, [Push(anna, "Анна", "Повторова", null)]);
        var distinct = Enumerable.Range(0, 249).Select(_ => NewId()).Append(anna).ToList();

        var fiveHundredAndOne = distinct.Concat(distinct).Append(anna).ToList();
        var ex = await Assert.ThrowsAsync<ValidationException>(() => SearchAsync(factory, new UserSearch("анна", AmongIds: fiveHundredAndOne)));
        Assert.Equal(UserDirectoryErrorCodes.TooManyIds, Assert.Single(ex.Errors["amongIds"]).Code);

        var fiveHundred = distinct.Concat(distinct).ToList();
        var answered = await SearchAsync(factory, new UserSearch("анна", AmongIds: fiveHundred));
        Assert.Equal(anna, Assert.Single(answered.Users).Id);
    }

    // qa.md A-15, api-contract.md B.2: the timestamp is a decimal integer; anything else is refused
    // even when the signature is computed over exactly that header value.
    [Theory]
    [InlineData("{0}.5")]
    [InlineData("+{0}")]
    [InlineData(" {0}")]
    [InlineData("{0}e0")]
    [InlineData("")]
    public async Task Push_TimestampNotADecimalInteger_IsRefused(string format)
    {
        var timestamp = string.Format(CultureInfo.InvariantCulture, format, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var body = PushTestClient.ChangesBody(Push(NewId(), "Анна", "Метка", null));
        var request = PushTestClient.Build(HttpMethod.Post, "changes", body, signed: false);
        request.Headers.TryAddWithoutValidation(PushSignatureVerifier.TimestampHeader, timestamp);
        request.Headers.TryAddWithoutValidation(
            PushSignatureVerifier.SignatureHeader,
            PushSignatureVerifier.Sign(
                MustaskApiFactory.TestPushSecret,
                PushSignatureVerifier.CanonicalString(timestamp, "POST", "changes", Encoding.UTF8.GetBytes(body))));

        using var response = await _push.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(UserDirectoryErrorCodes.PushUnauthorized, await PushTestClient.ErrorCodeAsync(response));
        Assert.Equal(0, await CountAsync(factory));
    }

    // qa.md A-29: a Bearer token beside a valid signature neither helps nor hurts — the signature decides.
    [Fact]
    public async Task Push_ValidSignatureBesideABearerToken_IsAcceptedOnTheSignature()
    {
        var id = NewId();
        var request = PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(Push(id, "Анна", "Подписанная", null)))
            .WithBearer(KeycloakTestRealm.CreateToken(PermissionCatalog.AllCodes));

        using var response = await _push.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await LookupAsync(factory, id)).Known[id].IsActive);
    }

    // qa.md A-31, BR-UP-6: a snapshot page and a change for one user applied at the same time end with
    // the newest moment, whichever request carries it.
    [Fact]
    public async Task PageAndChangeForOneUser_AtTheSameTime_TheNewestWins()
    {
        for (var i = 0; i < Repetitions; i++)
        {
            var id = NewId();
            var snapshotId = Guid.NewGuid();
            var start = BaseMoment + ((i + 1) * 1_000L);

            using (var begun = await _push.BeginAsync(snapshotId, start))
            {
                begun.EnsureSuccessStatusCode();
            }

            var pageIsNewer = i % 2 == 0;
            var pageEntry = Push(id, "Анна", pageIsNewer ? "Страничная" : "Устаревшая", null, moment: start + (pageIsNewer ? 2 : 1));
            var change = Push(id, "Анна", pageIsNewer ? "Устаревшая" : "Изменённая", null, moment: start + (pageIsNewer ? 1 : 2));

            var page = PushTestClient.Build(HttpMethod.Put, $"snapshots/{snapshotId}/pages/0", PushTestClient.PageBody(pageEntry));
            var changes = PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(change));

            var statuses = await SendTogetherAsync(i % 4 < 2 ? [page, changes] : [changes, page]);

            Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
            Assert.Equal(pageIsNewer ? "Анна Страничная" : "Анна Изменённая", (await LookupAsync(factory, id)).Known[id].DisplayName);
        }
    }

    // qa.md A-31, api-contract.md B-2: two snapshots begun at the same time never both stay open and
    // never fail with a server error — the newer one is open, the older superseded or refused.
    [Fact]
    public async Task TwoSnapshotsBegunAtTheSameTime_LeaveOnlyTheNewerOpen()
    {
        for (var i = 0; i < Repetitions; i++)
        {
            var older = Guid.NewGuid();
            var newer = Guid.NewGuid();
            var start = BaseMoment + ((i + 1) * 1_000L);
            var beginOlder = PushTestClient.Build(HttpMethod.Put, $"snapshots/{older}", $$"""{"enumerationStart":{{start}}}""");
            var beginNewer = PushTestClient.Build(HttpMethod.Put, $"snapshots/{newer}", $$"""{"enumerationStart":{{start + 1}}}""");

            var statuses = await SendTogetherAsync(i % 2 == 0 ? [beginOlder, beginNewer] : [beginNewer, beginOlder]);

            Assert.All(statuses, status => Assert.True(status is HttpStatusCode.OK or HttpStatusCode.Conflict, $"B-2 answered {status}."));

            using var again = await _push.BeginAsync(newer, start + 1);
            Assert.Equal("open", (await PushTestClient.ReadAsync<SnapshotStateBody>(again)).State);

            using var olderAgain = await _push.BeginAsync(older, start);
            if (olderAgain.StatusCode == HttpStatusCode.OK)
            {
                Assert.Equal("superseded", (await PushTestClient.ReadAsync<SnapshotStateBody>(olderAgain)).State);
            }
            else
            {
                Assert.Equal(HttpStatusCode.Conflict, olderAgain.StatusCode);
            }
        }
    }

    // qa.md A-24 at the NFR volume (BR-UP-8, NFR Volumes): a complete snapshot that carries none of
    // 10 000 held users — a Keycloak whose store was reset alone, spec NFR Stand — marks every one of
    // them absent, and the reconciliation finishes within 5 minutes.
    [Fact]
    public async Task CompleteSnapshotWithoutAnyHeldUser_AtTenThousand_MarksEveryoneAbsent()
    {
        var ids = await UserDirectoryVolumeTests.SeedVolumeAsync(factory);
        var stopwatch = Stopwatch.StartNew();

        var counts = await _push.ReconcileAsync(BaseMoment + 1_000_000, []);

        stopwatch.Stop();
        Assert.Equal(UserDirectoryVolumeTests.TargetVolume, counts.MarkedAbsent);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMinutes(5), $"The reconciliation took {stopwatch.Elapsed}.");
        Assert.DoesNotContain((await LookupAsync(factory, ids.Take(500).ToArray())).Known.Values, user => user.IsActive);
    }

    // qa.md A-19 (re-check 1), api-contract.md Part B's entry table: id and username are 1–255 characters,
    // the names at most 255, the moment above 0. Each violation is refused under its own JSON-path key, the
    // valid entry beside them gets no key, and nothing of the request is applied.
    [Fact]
    public async Task Push_EntryFieldsOutsideTheirLimits_AreRefusedEachUnderItsOwnPath()
    {
        var over = new string('x', UserDirectoryLimits.FieldMaxLength + 1);
        var body = $$"""
            {"changes":[
              {"id":"{{over}}","moment":{{BaseMoment}},"username":"u","enabled":true},
              {"id":"","moment":{{BaseMoment}},"username":"u","enabled":true},
              {"id":"{{NewId()}}","moment":{{BaseMoment}},"username":"{{over}}","enabled":true},
              {"id":"{{NewId()}}","moment":{{BaseMoment}},"enabled":true},
              {"id":"{{NewId()}}","moment":{{BaseMoment}},"username":"","enabled":true},
              {"id":"{{NewId()}}","moment":{{BaseMoment}},"username":"u","firstName":"{{over}}","enabled":true},
              {"id":"{{NewId()}}","moment":{{BaseMoment}},"username":"u","lastName":"{{over}}","enabled":true},
              {"id":"{{NewId()}}","moment":-1,"username":"u","enabled":true},
              {"id":"{{NewId()}}","moment":{{BaseMoment}},"username":"u","enabled":true}
            ]}
            """;

        using var response = await _push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await PushTestClient.ProblemAsync(response);
        Assert.Equal(UserDirectoryErrorCodes.ValidationFailed, problem.GetProperty("errorCode").GetString());
        var errors = problem.GetProperty("errors");

        string CodeOf(string path) => errors.GetProperty(path)[0].GetProperty("code").GetString()!;

        Assert.Equal(UserDirectoryErrorCodes.ValueTooLong, CodeOf("changes[0].id"));
        Assert.Equal(UserDirectoryErrorCodes.ValueRequired, CodeOf("changes[1].id"));
        Assert.Equal(UserDirectoryErrorCodes.ValueTooLong, CodeOf("changes[2].username"));
        Assert.Equal(UserDirectoryErrorCodes.ValueRequired, CodeOf("changes[3].username"));
        Assert.Equal(UserDirectoryErrorCodes.ValueRequired, CodeOf("changes[4].username"));
        Assert.Equal(UserDirectoryErrorCodes.ValueTooLong, CodeOf("changes[5].firstName"));
        Assert.Equal(UserDirectoryErrorCodes.ValueTooLong, CodeOf("changes[6].lastName"));
        Assert.Equal(UserDirectoryErrorCodes.ValueInvalid, CodeOf("changes[7].moment"));
        Assert.DoesNotContain(errors.EnumerateObject(), error => error.Name.StartsWith("changes[8]", StringComparison.Ordinal));
        Assert.Equal(0, await CountAsync(factory));
    }

    // qa.md A-19 (re-check 1): exactly 255 characters in every text field is accepted and held as sent —
    // a display name of 511 characters (both names and the space) included.
    [Fact]
    public async Task Push_EntryFieldsAtTheirLimits_AreAcceptedAndHeldAsSent()
    {
        var limit = UserDirectoryLimits.FieldMaxLength;
        var id = new string('i', limit);
        var username = new string('u', limit);
        var firstName = new string('Ф', limit);
        var lastName = new string('L', limit);
        var email = new string('e', limit - "@example.test".Length) + "@example.test";
        var body = $$"""
            {"changes":[
              {"id":"{{id}}","moment":{{BaseMoment}},"username":"{{username}}","firstName":"{{firstName}}","lastName":"{{lastName}}","email":"{{email}}","enabled":true}
            ]}
            """;

        using var response = await _push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var held = await FindAsync(factory, id);
        Assert.NotNull(held);
        Assert.Equal(username, held.Username);
        Assert.Equal(firstName, held.FirstName);
        Assert.Equal(lastName, held.LastName);
        Assert.Equal(email, held.Email);
        Assert.Equal(UserDirectoryLimits.DisplayNameMaxLength, held.DisplayName.Length);
        Assert.True((await LookupAsync(factory, id)).Known[id].IsActive);
    }

    private async Task<HttpStatusCode[]> SendTogetherAsync(HttpRequestMessage[] requests)
    {
        using var gate = new SemaphoreSlim(0);

        async Task<HttpStatusCode> SendAsync(HttpRequestMessage request)
        {
            await gate.WaitAsync();
            using var response = await _push.SendAsync(request);
            return response.StatusCode;
        }

        var sending = requests.Select(request => Task.Run(() => SendAsync(request))).ToArray();
        gate.Release(requests.Length);

        return await Task.WhenAll(sending);
    }
}
