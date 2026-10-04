using System.Net;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.UserDirectory;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014 QA defect D-5 (qa.md, re-check 3): a Unicode noncharacter — U+FDD0..U+FDEF and the last two code points of
/// every plane — in a text field of a user entry is refused as <c>value_invalid</c> (400, api-contract.md B.3 and
/// Part C) on both apply paths, B-1 and B-3, and <c>value_too_long</c> still comes first. U+FFFE made .NET's
/// normaliser throw while the projection derived its search columns: a 500, which the provider retries without end, so
/// one such user stalled the change feed for everyone. A consumer's search text holding one is read as U+FFFD
/// (api-contract.md A.3), never a 500.
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class PushNoncharacterTests(MustaskApiFactory factory) : IAsyncLifetime
{
    private readonly PushTestClient _push = new(factory);

    public Task InitializeAsync() => ResetAsync(factory);

    public Task DisposeAsync() => ResetAsync(factory);

    public static TheoryData<string> TextFields => new() { "id", "username", "firstName", "lastName", "email" };

    /// <summary>The first and last noncharacter of every kind: the block in the Arabic presentation forms and three planes' last two.</summary>
    public static TheoryData<int> Noncharacters => new()
    {
        0xFDD0, 0xFDEF, 0xFFFE, 0xFFFF, 0x1FFFE, 0x1FFFF, 0x10FFFE, 0x10FFFF,
    };

    /// <summary>Their neighbours, which are ordinary characters (or private use) and stay legal.</summary>
    public static TheoryData<int> NeighboursOfNoncharacters => new()
    {
        0xFDCF, 0xFDF0, 0xFFFD, 0x10000, 0x1FFFD, 0x20000, 0xFFFFD, 0x10FFFD,
    };

    /// <summary>A valid entry but for <paramref name="codePoint"/> in <paramref name="field"/>.</summary>
    private static UserPush WithIn(string field, int codePoint, bool deleted = false, string? id = null)
    {
        var text = char.ConvertFromUtf32(codePoint);

        return Push(
            id: field == "id" ? $"{id ?? NewId()}{text}" : id ?? NewId(),
            firstName: field == "firstName" ? $"Ан{text}на" : "Анна",
            lastName: field == "lastName" ? text : "Смирнова",
            email: field == "email" ? $"anna@example{text}.test" : "anna@example.test",
            username: field == "username" ? $"a.smir{text}nova" : "a.smirnova",
            deleted: deleted,
            enabled: !deleted);
    }

    private static async Task AssertRefusedAsync(HttpResponseMessage response, string path, string code)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await PushTestClient.ProblemAsync(response);
        Assert.Equal(UserDirectoryErrorCodes.ValidationFailed, problem.GetProperty("errorCode").GetString());
        Assert.Equal(code, problem.GetProperty("errors").GetProperty(path)[0].GetProperty("code").GetString());
    }

    // D-5: U+FFFE itself, the value that threw — in each of the five text fields, not only the three QA found reachable.
    [Theory]
    [MemberData(nameof(TextFields))]
    public async Task Changes_WithU_FFFEInATextField_AreRefusedAsInvalid_AndNothingChanges(string field)
    {
        using var response = await _push.SendAsync(
            PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(WithIn(field, 0xFFFE))));

        await AssertRefusedAsync(response, $"changes[0].{field}", UserDirectoryErrorCodes.ValueInvalid);
        Assert.Equal(0, await CountAsync(factory));
    }

    [Theory]
    [MemberData(nameof(TextFields))]
    public async Task SnapshotPage_WithANoncharacterInATextField_IsRefusedAsInvalid_AndNothingChanges(string field)
    {
        var snapshotId = Guid.NewGuid();
        using (var begun = await _push.BeginAsync(snapshotId, BaseMoment + 1_000))
        {
            begun.EnsureSuccessStatusCode();
        }

        using var response = await _push.PageAsync(snapshotId, 0, WithIn(field, 0xFFFF));

        await AssertRefusedAsync(response, $"entries[0].{field}", UserDirectoryErrorCodes.ValueInvalid);
        Assert.Equal(0, await CountAsync(factory));
    }

    // The whole class, by its edges: the first and last of the block, and of three of the planes' pairs.
    [Theory]
    [MemberData(nameof(Noncharacters))]
    public async Task EveryKindOfNoncharacter_IsRefusedInAName(int codePoint)
    {
        using var response = await _push.SendAsync(
            PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(WithIn("firstName", codePoint))));

        await AssertRefusedAsync(response, "changes[0].firstName", UserDirectoryErrorCodes.ValueInvalid);
        Assert.Equal(0, await CountAsync(factory));
    }

    // The rule does not reach past the class: the characters beside a noncharacter are stored as sent.
    [Theory]
    [MemberData(nameof(NeighboursOfNoncharacters))]
    public async Task TheNeighboursOfANoncharacter_AreStoredAsSent(int codePoint)
    {
        var text = char.ConvertFromUtf32(codePoint);
        var id = NewId();

        using var response = await _push.SendAsync(PushTestClient.Build(
            HttpMethod.Post, "changes", PushTestClient.ChangesBody(Push(id, $"A{text}B", $"C{text}D", $"e{text}@example.test", username: $"u{text}v"))));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var held = await FindAsync(factory, id);
        Assert.Equal($"A{text}B", held!.FirstName);
        Assert.Equal($"C{text}D", held.LastName);
        Assert.Equal($"e{text}@example.test", held.Email);
        Assert.Equal($"u{text}v", held.Username);
    }

    // The deletion carries the last state, so its text is checked like a state's (as for U+0000, RC3-12): refused, and
    // the held user it was meant for is not deleted.
    [Fact]
    public async Task ADeletion_CarryingANoncharacter_IsRefused_AndTheHeldUserIsStillActive()
    {
        var id = NewId();
        await SeedAsync(factory, [Push(id, "Анна", "Смирнова", null)]);

        using var response = await _push.SendAsync(
            PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(WithIn("lastName", 0xFFFE, deleted: true, id: id))));

        await AssertRefusedAsync(response, "changes[0].lastName", UserDirectoryErrorCodes.ValueInvalid);
        Assert.True((await FindAsync(factory, id))!.IsActive);
    }

    // B.3 (and security S8): over the limit and holding the character is `value_too_long`, the check that runs first.
    [Fact]
    public async Task ATextOver255CodePointsWithANoncharacter_IsTooLong_NotInvalid()
    {
        var tooLong = Push(NewId(), new string('я', 255) + char.ConvertFromUtf32(0xFFFE), "Б", null);

        using var response = await _push.SendAsync(
            PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(tooLong)));

        await AssertRefusedAsync(response, "changes[0].firstName", UserDirectoryErrorCodes.ValueTooLong);
        Assert.Equal(0, await CountAsync(factory));
    }

    // One bad entry refuses the request and the keys name it; the refusal names the field and the code, never the text.
    [Fact]
    public async Task Changes_WithOneEntryHoldingANoncharacter_NameThatEntryOnly_AndDoNotEchoIt()
    {
        var good = Push(NewId(), "Анна", "Смирнова", null);

        using var response = await _push.SendAsync(PushTestClient.Build(
            HttpMethod.Post, "changes", PushTestClient.ChangesBody(good, WithIn("email", 0xFDD0), good with { Id = NewId() })));

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("changes[1].email", body, StringComparison.Ordinal);
        Assert.DoesNotContain("changes[0]", body, StringComparison.Ordinal);
        Assert.DoesNotContain("changes[2]", body, StringComparison.Ordinal);
        Assert.DoesNotContain("example", body, StringComparison.Ordinal);
        Assert.Equal(0, await CountAsync(factory));
    }

    // A.3: a consumer's search text holding a noncharacter is answered — read as U+FFFD, the character the provider writes
    // for it in a name — never a 500. Nothing the projection holds contains one, so it finds only what holds U+FFFD.
    [Fact]
    public async Task ASearchText_WithU_FFFE_IsAnswered_AndFindsAUserWhoseNameTheProviderCleaned()
    {
        var cleaned = Push(NewId(), "Сам�И", "Смирнов", null);
        var other = Push(NewId(), "Анна", "Смирнова", null);
        await SeedAsync(factory, [cleaned, other]);

        var found = await SearchAsync(factory, new UserSearch("сам￾и"));
        var nobody = await SearchAsync(factory, new UserSearch("ан￾на"));
        var onlyNoncharacters = await SearchAsync(factory, new UserSearch("￾￿"));

        Assert.Equal(cleaned.Id, Assert.Single(found.Users).Id);
        Assert.Empty(nobody.Users);
        Assert.Empty(onlyNoncharacters.Users);
    }
}
