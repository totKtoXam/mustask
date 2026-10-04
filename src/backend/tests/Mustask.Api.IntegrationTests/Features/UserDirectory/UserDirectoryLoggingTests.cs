using System.Net;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014 US-6 "The credential is never printed" and NFR Logging: over
/// accepted, refused and failed pushes, MUSTASK's log carries the counts and
/// the refusals, names a user by id only, and never prints a name, an e-mail
/// or the push secret.
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class UserDirectoryLoggingTests(MustaskApiFactory factory) : IAsyncLifetime
{
    private const string Name = "Ольга";

    private const string Surname = "Логированная";

    private const string Email = "o.logged@example.test";

    public Task InitializeAsync() => ResetAsync(factory);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AcceptedRefusedAndFailedPushes_LogCountsAndReasons_NeverANameAnEmailOrTheSecret()
    {
        var id = NewId();
        var body = PushTestClient.ChangesBody(Push(id, Name, Surname, Email, username: "o.logged"));

        var output = await ConsoleCapture.RunAsync(factory, async push =>
        {
            using (var accepted = await push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", body)))
            {
                Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            }

            using (var refused = await push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", body, secret: "wrong-secret-0123456789abcdef-0123456789")))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
            }

            var invalid = $$"""{"changes":[{"id":"{{id}}","moment":0,"username":"o.logged","firstName":"{{Name}}","lastName":"{{Surname}}","email":"{{Email}}","enabled":true}]}""";
            using (var failed = await push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", invalid)))
            {
                Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);
            }

            await ExerciseSnapshotAsync(push, id);
        });

        Assert.Contains("User directory: changes applied — received 1, added 1", output, StringComparison.Ordinal);
        Assert.Contains("User directory push refused (WrongSignature)", output, StringComparison.Ordinal);
        Assert.Contains("completed — received 1, added 0, changed 1, marked absent 0, held as deleted 0", output, StringComparison.Ordinal);

        Assert.DoesNotContain(Surname, output, StringComparison.Ordinal);
        Assert.DoesNotContain(Email, output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("o.logged", output, StringComparison.Ordinal);
        Assert.DoesNotContain(MustaskApiFactory.TestPushSecret, output, StringComparison.Ordinal);
    }

    /// <summary>A whole snapshot carrying the same user with another e-mail, and one refused page.</summary>
    private static async Task ExerciseSnapshotAsync(PushTestClient push, string id)
    {
        var entry = Push(id, Name, Surname, "o.logged.renamed@example.test", username: "o.logged", moment: BaseMoment + 10);
        var counts = await push.ReconcileAsync(BaseMoment + 5, [entry]);
        Assert.Equal(1, counts.Changed);

        using var refused = await push.SendAsync(
            PushTestClient.Build(HttpMethod.Put, $"snapshots/{Guid.NewGuid()}/pages/0", PushTestClient.PageBody(entry), secret: "wrong-secret-0123456789abcdef-0123456789"));
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }
}
