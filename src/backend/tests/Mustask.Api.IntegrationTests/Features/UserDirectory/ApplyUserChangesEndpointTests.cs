using System.Net;
using System.Text;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.UserDirectory;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014 B-1 <c>POST /api/user-directory/feed/changes</c> end to end — a
/// signed push reaches the read model (US-2, US-3), and duplicates, reordering
/// and replays are harmless (US-5). The provider's own capture of Keycloak
/// events is tested in its Java build and live (plan § 10).
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class ApplyUserChangesEndpointTests(MustaskApiFactory factory) : IAsyncLifetime
{
    private readonly PushTestClient _push = new(factory);

    public Task InitializeAsync() => ResetAsync(factory);

    public Task DisposeAsync() => Task.CompletedTask;

    private static UserPush Anna(string id, long moment, string lastName = "Смирнова", string email = "anna.smirnova@example.test", bool enabled = true) =>
        Push(id, "Анна", lastName, email, enabled, username: "a.smirnova", moment: moment);

    // US-2 "A user created in the console becomes findable" — the push half; the id is the one pushed.
    [Fact]
    public async Task Push_NewUser_IsFindableActiveUnderThePushedId()
    {
        var id = NewId();

        var counts = await _push.PostChangesAsync(Anna(id, BaseMoment));

        Assert.Equal(new FeedCountsBody(1, 1, 0, 0, 0), counts);
        var result = await SearchAsync(factory, new UserSearch("смирнова"));
        Assert.Equal(new DirectoryUser(id, "Анна Смирнова", "anna.smirnova@example.test", true), Assert.Single(result.Users));
    }

    // US-2 "A change of name is followed".
    [Fact]
    public async Task Push_NameChange_IsFollowed()
    {
        var id = NewId();
        await _push.PostChangesAsync(Anna(id, BaseMoment));

        var counts = await _push.PostChangesAsync(Anna(id, BaseMoment + 1, lastName: "Кузнецова"));

        Assert.Equal(new FeedCountsBody(1, 0, 1, 0, 0), counts);
        Assert.Equal("Анна Кузнецова", (await LookupAsync(factory, id)).Known[id].DisplayName);
        Assert.Empty((await SearchAsync(factory, new UserSearch("смирнова"))).Users);
    }

    // US-2 "A change of e-mail is followed".
    [Fact]
    public async Task Push_EmailChange_IsFollowed()
    {
        var id = NewId();
        await _push.PostChangesAsync(Anna(id, BaseMoment));

        await _push.PostChangesAsync(Anna(id, BaseMoment + 1, email: "a.kuznetsova@example.test"));

        var found = Assert.Single((await SearchAsync(factory, new UserSearch("kuznetsova@"))).Users);
        Assert.Equal("a.kuznetsova@example.test", found.Email);
        Assert.Empty((await SearchAsync(factory, new UserSearch("anna.smirnova@"))).Users);
    }

    // US-2 "A change of username alone moves the fallback display name".
    [Fact]
    public async Task Push_UsernameRename_MovesTheFallbackDisplayName()
    {
        var id = NewId();
        await _push.PostChangesAsync(Push(id, null, null, null, username: "qa.fixture"));

        await _push.PostChangesAsync(Push(id, null, null, null, username: "qa.robot", moment: BaseMoment + 1));

        Assert.Equal("qa.robot", (await LookupAsync(factory, id)).Known[id].DisplayName);
    }

    // US-2 "A user created already disabled is inactive from the start".
    [Fact]
    public async Task Push_CreatedDisabled_IsInactiveAndNotADefaultCandidate()
    {
        var id = NewId();

        await _push.PostChangesAsync(Push(id, "Борис", "Орлов", null, enabled: false, username: "b.orlov"));

        Assert.False((await LookupAsync(factory, id)).Known[id].IsActive);
        Assert.Empty((await SearchAsync(factory, new UserSearch("орлов"))).Users);
    }

    // US-3 "A disabled account becomes inactive and stays nameable" and "An account enabled again becomes active under the same id".
    [Fact]
    public async Task Push_DisableThenEnable_FollowsTheStatusUnderTheSameId()
    {
        var id = NewId();
        await _push.PostChangesAsync(Push(id, "Иван", "Петров", "i.petrov@example.test"));

        await _push.PostChangesAsync(Push(id, "Иван", "Петров", "i.petrov@example.test", enabled: false, moment: BaseMoment + 1));

        Assert.Equal(new DirectoryUser(id, "Иван Петров", "i.petrov@example.test", false), (await LookupAsync(factory, id)).Known[id]);
        Assert.Empty((await SearchAsync(factory, new UserSearch("петров"))).Users);
        Assert.False(Assert.Single((await SearchAsync(factory, new UserSearch("петров", IncludeInactive: true))).Users).IsActive);

        await _push.PostChangesAsync(Push(id, "Иван", "Петров", "i.petrov@example.test", moment: BaseMoment + 2));

        Assert.True((await LookupAsync(factory, id)).Known[id].IsActive);
        Assert.Equal(id, Assert.Single((await SearchAsync(factory, new UserSearch("петров"))).Users).Id);
    }

    // US-3 "A deleted account becomes inactive for good and keeps its name".
    [Fact]
    public async Task Push_Deletion_KeepsTheRecordInactiveWithItsNameAndEmail()
    {
        var id = NewId();
        await _push.PostChangesAsync(Push(id, "Ольга", "Белова", "o.belova@example.test", username: "o.belova"));

        var counts = await _push.PostChangesAsync(Push(id, "Ольга", "Белова", "o.belova@example.test", username: "o.belova", moment: BaseMoment + 1, deleted: true));

        Assert.Equal(new FeedCountsBody(1, 0, 1, 0, 0), counts);
        Assert.Equal(new DirectoryUser(id, "Ольга Белова", "o.belova@example.test", false), (await LookupAsync(factory, id)).Known[id]);
        Assert.Equal(ProjectedUserLifecycle.Deleted, (await FindAsync(factory, id))!.Lifecycle);
    }

    // US-3 "A reported deletion is final" — the push half (the reconciliation half is in the snapshot tests).
    [Fact]
    public async Task Push_EnabledAfterDeletion_IsHeldAsDeleted()
    {
        var id = NewId();
        await _push.PostChangesAsync(Push(id, "Ольга", "Белова", null, moment: BaseMoment, deleted: true));

        var counts = await _push.PostChangesAsync(Push(id, "Ольга", "Белова", null, moment: BaseMoment + 10));

        Assert.Equal(new FeedCountsBody(1, 0, 0, 0, 1), counts);
        Assert.False((await LookupAsync(factory, id)).Known[id].IsActive);
    }

    // US-3 "A new user with a deleted user's username and e-mail is another user".
    [Fact]
    public async Task Push_NewUserWithADeletedUsersUsernameAndEmail_IsAnotherUser()
    {
        var old = NewId();
        await _push.PostChangesAsync(Push(old, "Ольга", "Белова", "o.belova@example.test", username: "o.belova", deleted: true));
        var replacement = NewId();

        await _push.PostChangesAsync(Push(replacement, "Ольга", "Белова", "o.belova@example.test", username: "o.belova", moment: BaseMoment + 1));

        var found = Assert.Single((await SearchAsync(factory, new UserSearch("o.belova@"))).Users);
        Assert.Equal(replacement, found.Id);
        Assert.True(found.IsActive);
        Assert.False((await LookupAsync(factory, old)).Known[old].IsActive);
    }

    // US-5 "The same push applied twice changes nothing".
    [Fact]
    public async Task Push_SameTwice_SecondIsUnchangedAndOneRecordIsHeld()
    {
        var id = NewId();
        var push = Anna(id, BaseMoment, lastName: "Кузнецова");

        await _push.PostChangesAsync(push);
        var second = await _push.PostChangesAsync(push);

        Assert.Equal(new FeedCountsBody(1, 0, 0, 1, 0), second);
        Assert.Equal(1, await CountAsync(factory));
        Assert.Equal(new DirectoryUser(id, "Анна Кузнецова", "anna.smirnova@example.test", true), (await LookupAsync(factory, id)).Known[id]);
    }

    // US-5 "An older push arriving after a newer one is ignored".
    [Fact]
    public async Task Push_OlderAfterNewer_IsIgnored()
    {
        var id = NewId();

        await _push.PostChangesAsync(Anna(id, BaseMoment + 2, lastName: "Кузнецова"));
        var older = await _push.PostChangesAsync(Anna(id, BaseMoment + 1, lastName: "Смирнова"));

        Assert.Equal(new FeedCountsBody(1, 0, 0, 1, 0), older);
        Assert.Equal("Анна Кузнецова", (await LookupAsync(factory, id)).Known[id].DisplayName);
    }

    // US-5 "An older 'enabled' after a newer 'disabled' leaves the user inactive".
    [Fact]
    public async Task Push_OlderEnabledAfterNewerDisabled_StaysInactive()
    {
        var id = NewId();

        await _push.PostChangesAsync(Anna(id, BaseMoment + 2, enabled: false));
        await _push.PostChangesAsync(Anna(id, BaseMoment + 1, enabled: true));

        Assert.False((await LookupAsync(factory, id)).Known[id].IsActive);
    }

    // US-5 "A replayed push from the past cannot undo a later change" — the same signed request sent again inside the window.
    [Fact]
    public async Task Push_ReplayedCapturedRequest_CannotUndoALaterChange()
    {
        var id = NewId();
        var captured = PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(Push(id, "Иван", "Петров", null, moment: BaseMoment)));
        var replay = await PushTestClient.CloneAsync(captured);

        using (var first = await _push.SendAsync(captured))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        await _push.PostChangesAsync(Push(id, "Иван", "Петров", null, enabled: false, moment: BaseMoment + 1));

        using var replayed = await _push.SendAsync(replay);

        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        Assert.False((await LookupAsync(factory, id)).Known[id].IsActive);
    }

    [Fact]
    public async Task Push_ManyEntries_CountsEachOnce()
    {
        var a = NewId();
        var b = NewId();

        var counts = await _push.PostChangesAsync(
            Push(a, "Анна", "А", null, moment: BaseMoment + 2),
            Push(a, "Анна", "Б", null, moment: BaseMoment + 1),
            Push(b, "Борис", "В", null, deleted: true));

        Assert.Equal(new FeedCountsBody(3, 2, 1, 0, 0), counts);
        Assert.Equal("Анна А", (await LookupAsync(factory, a)).Known[a].DisplayName);
    }

    [Fact]
    public async Task Push_FiveHundredEntries_IsAccepted()
    {
        var pushes = Enumerable.Range(0, UserDirectoryLimits.EntriesPerPush).Select(i => Push(NewId(), "Анна", $"Ф{i}", null)).ToArray();

        var counts = await _push.PostChangesAsync(pushes);

        Assert.Equal(500, counts.Added);
    }

    // api-contract.md B-1, Part C: field keys are JSON paths into the request.
    [Fact]
    public async Task Push_InvalidEntries_AreRefusedWithFieldPaths_AndNothingChanges()
    {
        var tooLong = new string('x', UserDirectoryLimits.FieldMaxLength + 1);
        var farFuture = UserPushValidation.MicrosecondsSinceEpoch(DateTimeOffset.UtcNow.AddHours(2));
        var body = $$"""
            {"changes":[
              {"id":"{{NewId()}}","moment":{{BaseMoment}},"username":"ok","enabled":true},
              {"moment":{{BaseMoment}},"username":"no-id","enabled":true},
              {"id":"x","moment":0,"username":"u","enabled":true},
              {"id":"y","moment":{{farFuture}},"username":"u","enabled":true},
              {"id":"z","moment":{{BaseMoment}},"username":"u","email":"{{tooLong}}","enabled":true},
              {"id":"w","moment":{{BaseMoment}},"username":"u"},
              {"id":"v","moment":{{BaseMoment}},"username":"u","deleted":true}
            ]}
            """;

        using var response = await _push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await PushTestClient.ProblemAsync(response);
        Assert.Equal(UserDirectoryErrorCodes.ValidationFailed, problem.GetProperty("errorCode").GetString());
        var errors = problem.GetProperty("errors");
        Assert.Equal(UserDirectoryErrorCodes.ValueRequired, errors.GetProperty("changes[1].id")[0].GetProperty("code").GetString());
        Assert.Equal(UserDirectoryErrorCodes.ValueInvalid, errors.GetProperty("changes[2].moment")[0].GetProperty("code").GetString());
        Assert.Equal(UserDirectoryErrorCodes.ValueInvalid, errors.GetProperty("changes[3].moment")[0].GetProperty("code").GetString());
        Assert.Equal(UserDirectoryErrorCodes.ValueTooLong, errors.GetProperty("changes[4].email")[0].GetProperty("code").GetString());
        Assert.Equal(UserDirectoryErrorCodes.ValueRequired, errors.GetProperty("changes[5].enabled")[0].GetProperty("code").GetString());
        Assert.False(errors.TryGetProperty("changes[6].enabled", out _), "A deletion needs no 'enabled'.");
        Assert.Equal(0, await CountAsync(factory));
    }

    [Theory]
    [InlineData("""{"changes":[]}""", "changes", "user_directory.value_required")]
    [InlineData("""{}""", "changes", "user_directory.value_required")]
    public async Task Push_EmptyOrMissingList_IsRefused(string body, string field, string reason)
    {
        using var response = await _push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await PushTestClient.ProblemAsync(response);
        Assert.Equal(reason, problem.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Push_FiveHundredAndOneEntries_IsRefusedAsTooMany()
    {
        var body = PushTestClient.ChangesBody(Enumerable.Range(0, 501).Select(i => Push(NewId(), "А", $"{i}", null)).ToArray());

        using var response = await _push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var reason = (await PushTestClient.ProblemAsync(response)).GetProperty("errors").GetProperty("changes")[0];
        Assert.Equal(UserDirectoryErrorCodes.TooManyEntries, reason.GetProperty("code").GetString());
        Assert.Equal(0, await CountAsync(factory));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"changes":[{"id":"a","moment":"soon","username":"u","enabled":true}]}""")]
    [InlineData("null")]
    public async Task Push_BodyNotOfTheDeclaredShape_IsMalformed(string body)
    {
        using var response = await _push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(UserDirectoryErrorCodes.PushMalformed, await PushTestClient.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Push_BodyOverFourMebibytes_IsMalformed()
    {
        var body = "{\"changes\":[],\"padding\":\"" + new string('x', 4 * 1024 * 1024) + "\"}";

        using var response = await _push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(UserDirectoryErrorCodes.PushMalformed, await PushTestClient.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Push_UnknownPropertiesAreIgnored()
    {
        var id = NewId();
        var body = $$"""{"changes":[{"id":"{{id}}","moment":{{BaseMoment}},"username":"u","enabled":true,"roles":["x"]}],"extra":1}""";

        using var response = await _push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(id, (await LookupAsync(factory, id)).Known.Keys);
    }

    [Fact]
    public async Task Push_ResponseIsTheFeedCountsJson()
    {
        using var response = await _push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(Anna(NewId(), BaseMoment))));

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var json = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("""{"received":1,"added":1,"changed":0,"unchanged":0,"heldAsDeleted":0}""", json);
    }
}
