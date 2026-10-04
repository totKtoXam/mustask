using System.Net;
using System.Text;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.UserDirectory;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// QA re-check 3 (qa.md, RC3-6, RC3-7, RC3-12..RC3-16): what the security pass's S2 fix could have broken, and the
/// edges <c>PushNulCharacterTests</c> does not walk. U+0000 is refused as <c>value_invalid</c> in every text field,
/// of a deletion too, without echoing the value and without recording a refused page; every other character a
/// provider may send — control characters, U+FFFD, a valid surrogate pair — is stored as sent, up to the
/// length limit counted in code points, and the largest batch the provider can build is inside the body bound.
/// (A noncharacter is refused too, since D-5 was fixed: <c>PushNoncharacterTests</c>.)
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class PushNulCharacterQaTests(MustaskApiFactory factory) : IAsyncLifetime
{
    private const string Emoji = "\U0001F600";

    private readonly PushTestClient _push = new(factory);

    public Task InitializeAsync() => ResetAsync(factory);

    public Task DisposeAsync() => ResetAsync(factory);

    public static TheoryData<string> TextFields => new() { "id", "username", "firstName", "lastName", "email" };

    /// <summary>Built from code points, so that no line separator or noncharacter sits in the source text itself.</summary>
    public static TheoryData<string> OtherCharacters
    {
        get
        {
            var data = new TheoryData<string>();

            foreach (var codePoint in new[] { 0x01, 0x08, 0x09, 0x0A, 0x1F, 0x7F, 0x85, 0x2028, 0x2029, 0x200B, 0xFEFF, 0xFFFD, 0x1F600, 0x20000 })
            {
                data.Add(char.ConvertFromUtf32(codePoint));
            }

            data.Add("\r\n");
            data.Add("\u0001" + Emoji + "\u001F");

            return data;
        }
    }

    private static UserPush WithNulIn(string field, bool deleted = false, string? id = null, string marker = "Секрет") => Push(
        id: field == "id" ? $"{id ?? NewId()}\0" : id ?? NewId(),
        firstName: field == "firstName" ? $"{marker}\0Имя" : "Анна",
        lastName: field == "lastName" ? $"{marker}\0Фамилия" : "Смирнова",
        email: field == "email" ? $"{marker}\0@example.test" : "anna@example.test",
        username: field == "username" ? $"{marker}\0user" : "a.smirnova",
        deleted: deleted,
        enabled: !deleted);

    private static async Task<(HttpStatusCode Status, string Body)> SendAsync(PushTestClient push, HttpRequestMessage request)
    {
        using var response = await push.SendAsync(request);

        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    // RC3-12: a deletion entry carries the last state, so its text fields are checked like a state's: refused, and the
    // held user it was meant for is not deleted (the refusal is of the whole request, before any apply).
    [Theory]
    [MemberData(nameof(TextFields))]
    public async Task ADeletion_CarryingU0000_IsRefusedAsInvalid_AndTheHeldUserIsStillActive(string field)
    {
        var id = NewId();
        await SeedAsync(factory, [Push(id, "Анна", "Смирнова", null)]);

        var deletion = WithNulIn(field, deleted: true, id: id);
        var (status, body) = await SendAsync(_push, PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(deletion)));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains($"changes[0].{field}", body, StringComparison.Ordinal);
        Assert.Contains(UserDirectoryErrorCodes.ValueInvalid, body, StringComparison.Ordinal);

        var held = await FindAsync(factory, id);
        Assert.True(held!.IsActive, "the refused deletion deleted nobody");
    }

    // RC3-12: the answer and the log carry the key and the code, never the text that held the character.
    [Theory]
    [MemberData(nameof(TextFields))]
    public async Task TheRefusal_NeverEchoesTheValue_NeitherInTheAnswerNorInTheLog(string field)
    {
        string? answer = null;

        var output = await ConsoleCapture.RunAsync(factory, async push =>
        {
            var (status, body) = await SendAsync(push, PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(WithNulIn(field))));
            Assert.Equal(HttpStatusCode.BadRequest, status);
            answer = body;
        });

        Assert.NotNull(answer);
        Assert.DoesNotContain("Секрет", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u0000", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("Секрет", output, StringComparison.Ordinal);
        Assert.DoesNotContain("\0", output, StringComparison.Ordinal);
    }

    // RC3-13: a refused page is not a page that was applied. The same number sent again, corrected, is applied (and
    // not answered with stored counts), and the snapshot then completes on exactly that page.
    [Fact]
    public async Task ARefusedPage_IsNotRecorded_ACorrectedPageWithTheSameNumberIsApplied_AndTheSnapshotCompletes()
    {
        var snapshotId = Guid.NewGuid();
        using (var begun = await _push.BeginAsync(snapshotId, BaseMoment + 1_000))
        {
            begun.EnsureSuccessStatusCode();
        }

        var good = Push(NewId(), "Анна", "Смирнова", null, moment: BaseMoment + 500);
        var bad = WithNulIn("lastName");

        using (var refused = await _push.PageAsync(snapshotId, 0, good, bad with { Moment = BaseMoment + 500 }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        }

        Assert.Equal(0, await CountAsync(factory));

        using (var corrected = await _push.PageAsync(snapshotId, 0, good))
        {
            Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
            var counts = await PushTestClient.ReadAsync<FeedCountsBody>(corrected);
            Assert.Equal(new FeedCountsBody(1, 1, 0, 0, 0), counts);
        }

        using var completed = await _push.CompleteAsync(snapshotId, 1, 1);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal(1, await CountAsync(factory));
    }

    // RC3-13: the key of an entry among good ones on a page.
    [Fact]
    public async Task APage_WithTheBadEntryThird_NamesEntryTwoOnly()
    {
        var snapshotId = Guid.NewGuid();
        using (var begun = await _push.BeginAsync(snapshotId, BaseMoment + 1_000))
        {
            begun.EnsureSuccessStatusCode();
        }

        using var response = await _push.PageAsync(
            snapshotId, 0, Push(NewId(), "А", "Б", null), Push(NewId(), "В", "Г", null), WithNulIn("email"));

        var errors = (await PushTestClient.ProblemAsync(response)).GetProperty("errors");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(errors.TryGetProperty("entries[2].email", out _));
        Assert.Single(errors.EnumerateObject());
    }

    // RC3-14: everything but U+0000 (and, since D-5, a noncharacter: PushNoncharacterTests) passes, in every text
    // field, and comes back as sent — the controls, the line separators, the replacement character, an emoji, a pair
    // next to a control.
    [Theory]
    [MemberData(nameof(OtherCharacters))]
    public async Task EveryOtherCharacter_IsStoredAsSent_InEveryTextField(string text)
    {
        var id = $"id-{NewId()}-{text}-end";
        var push = Push(id, $"A{text}B", $"C{text}D", $"e{text}@example.test", username: $"u{text}v");
        using var response = await _push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(push)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var held = await FindAsync(factory, id);
        Assert.NotNull(held);
        Assert.Equal($"u{text}v", held.Username);
        Assert.Equal($"A{text}B", held.FirstName);
        Assert.Equal($"C{text}D", held.LastName);
        Assert.Equal($"e{text}@example.test", held.Email);
    }

    // D-5 (qa.md, re-check 3): the noncharacter U+FFFE passes validation in the first name, last name and e-mail and then
    // throws when the projection derives its normalised search columns — a 500, which the provider retries without end
    // (the stall S1 and S2 closed for a lone surrogate and U+0000). Fix-agnostic: stored (200) or refused (400), never a 500.
    [Theory]
    [InlineData("firstName")]
    [InlineData("lastName")]
    [InlineData("email")]
    public async Task TheNoncharacterU_FFFE_IsStoredOrRefused_NeverAServerError(string field)
    {
        var uFffe = char.ConvertFromUtf32(0xFFFE);
        var push = Push(
            NewId(),
            field == "firstName" ? $"A{uFffe}B" : "Анна",
            field == "lastName" ? $"C{uFffe}D" : "Смирнова",
            field == "email" ? $"e{uFffe}@example.test" : "anna@example.test");

        using var response = await _push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(push)));

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest,
            $"U+FFFE in {field} answered {(int)response.StatusCode}");
    }

    // RC3-8: what the provider's cleaning can leave behind — several users whose names are now the same text, one of them
    // containing U+FFFD of its own. The projection is keyed by id and names are not unique (BR-UP-5), so all are held,
    // a search finds all of them (the collation takes U+FFFD) in the order of BR-UP-15 (ties by id, character code by
    // character code), and a lookup answers each by its id.
    [Fact]
    public async Task UsersWhoseCleanedNamesCollide_AreAllHeld_Found_AndOrderedById()
    {
        var ids = new[] { "u-3", "u-1", "u-2", "u-4" };
        var replacement = char.ConvertFromUtf32(0xFFFD);
        using var response = await _push.SendAsync(PushTestClient.Build(
            HttpMethod.Post,
            "changes",
            PushTestClient.ChangesBody([.. ids.Select(id => Push(id, $"Сам{replacement}", "Иванов", null))])));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var found = await SearchAsync(factory, new UserSearch("сам"));
        Assert.Equal(["u-1", "u-2", "u-3", "u-4"], found.Users.Select(user => user.Id));
        Assert.All(found.Users, user => Assert.Equal($"Сам{replacement} Иванов", user.DisplayName));

        var byReplacement = await SearchAsync(factory, new UserSearch($"сам{replacement}"));
        Assert.Equal(4, byReplacement.Users.Count);

        var lookup = await LookupAsync(factory, ids);
        Assert.Equal(4, lookup.Known.Count);
        Assert.Empty(lookup.Unknown);
    }

    // RC3-6: an emoji in a name is a display name like any other — found by the text beside it and by the emoji itself.
    [Fact]
    public async Task AnEmojiInAName_IsFound_ByTheTextBesideIt_AndByTheEmoji()
    {
        var id = NewId();
        using var response = await _push.SendAsync(PushTestClient.Build(
            HttpMethod.Post, "changes", PushTestClient.ChangesBody(Push(id, $"Анна {Emoji}", "Смирнова", null))));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var byText = await SearchAsync(factory, new UserSearch("смирнова"));
        Assert.Equal($"Анна {Emoji} Смирнова", Assert.Single(byText.Users).DisplayName);

        var byEmoji = await SearchAsync(factory, new UserSearch($"а {Emoji}"));
        Assert.Equal(id, Assert.Single(byEmoji.Users).Id);
    }

    // RC3-15: the order the checks run in, and what stays legal.
    [Fact]
    public async Task AUsernameOfOnlyU0000_IsInvalid_NotRequired_AndAnEmptyOneIsStillRequired()
    {
        var onlyNul = Push(NewId(), "А", "Б", null, username: "\0");
        var empty = Push(NewId(), "А", "Б", null, username: string.Empty);

        var (nulStatus, nulBody) = await SendAsync(_push, PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(onlyNul)));
        var (emptyStatus, emptyBody) = await SendAsync(_push, PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(empty)));

        Assert.Equal(HttpStatusCode.BadRequest, nulStatus);
        Assert.Contains(UserDirectoryErrorCodes.ValueInvalid, nulBody, StringComparison.Ordinal);
        Assert.DoesNotContain(UserDirectoryErrorCodes.ValueRequired, nulBody, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, emptyStatus);
        Assert.Contains(UserDirectoryErrorCodes.ValueRequired, emptyBody, StringComparison.Ordinal);
        Assert.Equal(0, await CountAsync(factory));
    }

    [Fact]
    public async Task ATextOver255CodePointsWithU0000_IsTooLongFirst_AndEveryViolationOfAnEntryIsReported()
    {
        var tooLongWithNul = Push(NewId(), new string('я', 255) + "\0", "Б", null);
        var nulAndBadMoment = Push(NewId(), "А\0", "Б", null, moment: 0);

        var (status, body) = await SendAsync(_push, PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(tooLongWithNul)));
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains(UserDirectoryErrorCodes.ValueTooLong, body, StringComparison.Ordinal);
        Assert.DoesNotContain(UserDirectoryErrorCodes.ValueInvalid, body, StringComparison.Ordinal);

        var (status2, body2) = await SendAsync(_push, PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(nulAndBadMoment)));
        Assert.Equal(HttpStatusCode.BadRequest, status2);
        Assert.Contains("changes[0].firstName", body2, StringComparison.Ordinal);
        Assert.Contains("changes[0].moment", body2, StringComparison.Ordinal);
        Assert.Equal(0, await CountAsync(factory));
    }

    [Fact]
    public async Task NullNames_AndANullEmail_StillMeanEmpty_AndPass()
    {
        var id = NewId();
        using var response = await _push.SendAsync(PushTestClient.Build(
            HttpMethod.Post, "changes", PushTestClient.ChangesBody(Push(id, null, null, null, username: "only.username"))));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var held = await FindAsync(factory, id);
        Assert.Equal(string.Empty, held!.FirstName);
        Assert.Equal("only.username", held.DisplayName);
    }

    // RC3-16: the unpaired surrogate MUSTASK's reader cannot take stays a 400 `push_malformed`, never a 500, whichever
    // half it is, and nothing is written; a raw 0x00 byte inside a JSON string is no JSON at all.
    [Theory]
    [InlineData("\\uDC00")]
    [InlineData("\\uDBFF")]
    [InlineData("\\uD800x")]
    [InlineData("x\\uD83D")]
    public async Task AnUnpairedSurrogateEscape_IsMalformed_NotAServerError_AndNothingIsWritten(string escaped)
    {
        var body = $$"""{"changes":[{"id":"{{NewId()}}","moment":{{BaseMoment}},"username":"u","firstName":"{{escaped}}","lastName":null,"email":null,"enabled":true}]}""";

        var (status, answer) = await SendAsync(_push, PushTestClient.Build(HttpMethod.Post, "changes", body));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains(UserDirectoryErrorCodes.PushMalformed, answer, StringComparison.Ordinal);
        Assert.Equal(0, await CountAsync(factory));
    }

    [Fact]
    public async Task ARawNulByteInsideAJsonString_IsMalformed_NotAServerError()
    {
        var body = $$"""{"changes":[{"id":"{{NewId()}}","moment":{{BaseMoment}},"username":"u","firstName":"A{{'\0'}}B","lastName":null,"email":null,"enabled":true}]}""";

        var (status, answer) = await SendAsync(_push, PushTestClient.Build(HttpMethod.Post, "changes", body));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains(UserDirectoryErrorCodes.PushMalformed, answer, StringComparison.Ordinal);
        Assert.Equal(0, await CountAsync(factory));
    }

    // RC3-6, RC3-7: the length limit counts code points, so 255 emoji (510 UTF-16 units) is the longest legal text and
    // 256 is not — in every field. The pairs travel as escaped halves, the way the provider's Jackson writes them.
    [Fact]
    public async Task TheLimit_IsCodePoints_256EmojiAreTooLong_255Pass_InEveryTextField()
    {
        var fits = string.Concat(Enumerable.Repeat(Emoji, 255));
        var tooLong = fits + Emoji;

        using (var refused = await _push.SendAsync(PushTestClient.Build(
            HttpMethod.Post, "changes", RawChanges([RawEntry(tooLong, tooLong, tooLong, tooLong, tooLong)]))))
        {
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            var errors = (await PushTestClient.ProblemAsync(refused)).GetProperty("errors");

            foreach (var field in new[] { "id", "username", "firstName", "lastName", "email" })
            {
                Assert.Equal(
                    UserDirectoryErrorCodes.ValueTooLong,
                    errors.GetProperty($"changes[0].{field}")[0].GetProperty("code").GetString());
            }
        }

        Assert.Equal(0, await CountAsync(factory));

        using (var response = await _push.SendAsync(PushTestClient.Build(
            HttpMethod.Post, "changes", RawChanges([RawEntry(fits, fits, fits, fits, fits)]))))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var id = fits;
        var held = await FindAsync(factory, id);
        Assert.NotNull(held);
        Assert.Equal(fits, held.Username);
        Assert.Equal(fits, held.FirstName);
        Assert.Equal(fits, held.LastName);
        Assert.Equal(fits, held.Email);
        Assert.Equal(fits + " " + fits, held.DisplayName);
    }

    // RC3-7: the largest batch Keycloak's own limits let the provider build — 500 entries, every text field at the user
    // profile's 255 UTF-16 units (127 emoji and one more character), each emoji two 6-byte escapes on the wire — is inside
    // the 4 MiB body bound and is applied whole. (Five fields of 255 emoji each would be 255 code points, which MUSTASK
    // takes but Keycloak's length check, which presumably counts UTF-16 units (not probed), does not: that batch would be about 7.7 MB, over the bound.)
    // The batch below is about 3.85 MB, 92% of the bound.
    [Fact]
    public async Task TheLargestBatchKeycloaksLimitsAllow_FitsTheBodyBound_AndIsApplied()
    {
        var field = string.Concat(Enumerable.Repeat(Emoji, 127)) + "x";
        Assert.Equal(255, field.Length);
        var entries = Enumerable.Range(0, UserDirectoryLimits.EntriesPerPush)
            .Select(index => RawEntry(index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture) + field[4..], field, field, field, field))
            .ToList();
        var body = RawChanges(entries);
        var bytes = Encoding.UTF8.GetByteCount(body);

        Assert.True(bytes < 4 * 1024 * 1024, $"the batch is {bytes} bytes");

        using var response = await _push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(UserDirectoryLimits.EntriesPerPush, await CountAsync(factory));
    }

    /// <summary>
    /// One entry hand-written the way the provider's Jackson writes it: every character raw except a surrogate, which is
    /// escaped (<c>😀</c> for an emoji — a finding of QA re-check 3: Jackson does not write a pair as 4 UTF-8 bytes).
    /// </summary>
    private static string RawEntry(string id, string username, string firstName, string lastName, string email) =>
        $$"""{"id":"{{Wire(id)}}","moment":{{BaseMoment}},"username":"{{Wire(username)}}","firstName":"{{Wire(firstName)}}","lastName":"{{Wire(lastName)}}","email":"{{Wire(email)}}","enabled":true}""";

    private static string Wire(string text)
    {
        var wire = new StringBuilder();

        foreach (var unit in text)
        {
            wire.Append(char.IsSurrogate(unit) ? $"\\u{(int)unit:x4}" : unit.ToString());
        }

        return wire.ToString();
    }

    private static string RawChanges(IEnumerable<string> entries) => "{\"changes\":[" + string.Join(',', entries) + "]}";
}
