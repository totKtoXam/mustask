using System.Net;
using Mustask.Domain.UserDirectory;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014 security pass S2 (review.md, "Security pass — backend"): U+0000 in
/// a text field of a user entry — PostgreSQL text cannot hold it — is refused
/// as <c>value_invalid</c> (400, api-contract.md Part C) on both apply paths,
/// B-1 and B-3. It was a 500 at the save, which the provider retries without
/// end, so one such entry stalled the whole change feed.
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class PushNulCharacterTests(MustaskApiFactory factory) : IAsyncLifetime
{
    private readonly PushTestClient _push = new(factory);

    public Task InitializeAsync() => ResetAsync(factory);

    public Task DisposeAsync() => ResetAsync(factory);

    public static TheoryData<string> TextFields => new() { "id", "username", "firstName", "lastName", "email" };

    /// <summary>A valid entry but for U+0000 in <paramref name="field"/>.</summary>
    private static UserPush WithNulIn(string field) => Push(
        id: field == "id" ? $"{NewId()}\0" : NewId(),
        firstName: field == "firstName" ? "Ан\0на" : "Анна",
        lastName: field == "lastName" ? "\0" : "Смирнова",
        email: field == "email" ? "anna@example\0.test" : "anna@example.test",
        username: field == "username" ? "a.smir\0nova" : "a.smirnova");

    private static async Task AssertRefusedAsInvalidAsync(HttpResponseMessage response, string path)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await PushTestClient.ProblemAsync(response);
        Assert.Equal(UserDirectoryErrorCodes.ValidationFailed, problem.GetProperty("errorCode").GetString());
        Assert.Equal(UserDirectoryErrorCodes.ValueInvalid, problem.GetProperty("errors").GetProperty(path)[0].GetProperty("code").GetString());
    }

    [Theory]
    [MemberData(nameof(TextFields))]
    public async Task Changes_WithU0000InATextField_AreRefusedAsInvalid_AndNothingChanges(string field)
    {
        using var response = await _push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(WithNulIn(field))));

        await AssertRefusedAsInvalidAsync(response, $"changes[0].{field}");
        Assert.Equal(0, await CountAsync(factory));
    }

    [Theory]
    [MemberData(nameof(TextFields))]
    public async Task SnapshotPage_WithU0000InATextField_IsRefusedAsInvalid_AndNothingChanges(string field)
    {
        var snapshotId = Guid.NewGuid();
        using (var begun = await _push.BeginAsync(snapshotId, BaseMoment + 1_000))
        {
            begun.EnsureSuccessStatusCode();
        }

        using var response = await _push.PageAsync(snapshotId, 0, WithNulIn(field));

        await AssertRefusedAsInvalidAsync(response, $"entries[0].{field}");
        Assert.Equal(0, await CountAsync(factory));
    }

    // One bad entry refuses the request, and the field keys name it: the entries around it are not the reason.
    [Fact]
    public async Task Changes_WithOneEntryHoldingU0000_NameThatEntryOnly()
    {
        var good = Push(NewId(), "Анна", "Смирнова", null);

        using var response = await _push.SendAsync(
            PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(good, WithNulIn("firstName"), good with { Id = NewId() })));

        await AssertRefusedAsInvalidAsync(response, "changes[1].firstName");
        var errors = (await PushTestClient.ProblemAsync(response)).GetProperty("errors");
        Assert.False(errors.TryGetProperty("changes[0].firstName", out _) || errors.TryGetProperty("changes[2].firstName", out _));
        Assert.Equal(0, await CountAsync(factory));
    }
}
