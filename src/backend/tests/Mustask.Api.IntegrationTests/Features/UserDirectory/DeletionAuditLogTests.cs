using System.Net;
using Mustask.Domain.UserDirectory;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014 security pass S5 (review.md, "Security pass — backend"): a reported
/// deletion is final (BR-UP-5), so the log says which ids were deleted. NFR
/// Audit leaves the audit to Keycloak's event log, which records deletions made
/// in Keycloak and not one pushed by whoever holds the secret; MUSTASK's own
/// line counts deletions apart from the changes and names the ids applied as
/// deletions at Information — ids only, never the name or e-mail a deletion
/// carries (NFR Logging).
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class DeletionAuditLogTests(MustaskApiFactory factory) : IAsyncLifetime
{
    private const string Surname = "Удалённая";

    private const string Email = "o.deleted@example.test";

    public Task InitializeAsync() => ResetAsync(factory);

    public Task DisposeAsync() => ResetAsync(factory);

    private static UserPush Deletion(string id, long moment) =>
        Push(id, "Ольга", Surname, Email, enabled: false, username: "o.deleted", moment: moment, deleted: true);

    [Fact]
    public async Task ADeletionApplied_IsCountedApart_AndItsIdIsLoggedAtInformation_NeverThePerson()
    {
        var deleted = NewId();
        var kept = NewId();
        var alreadyDeleted = NewId();
        await SeedAsync(factory, [Push(deleted, "Ольга", Surname, Email, username: "o.deleted"), Push(kept, "Иван", "Новиков", null)]);

        var output = await ConsoleCapture.RunAsync(factory, async push =>
        {
            using var first = await push.SendAsync(PushTestClient.Build(
                HttpMethod.Post,
                "changes",
                PushTestClient.ChangesBody(
                    Deletion(deleted, BaseMoment + 10),
                    Push(kept, "Иван", "Новиков", null, enabled: false, moment: BaseMoment + 10),
                    Deletion(alreadyDeleted, BaseMoment + 10))));

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        });

        // Two deletions applied (one held user newly deleted, one id added as deleted) and one plain change.
        Assert.Contains("received 3, added 1, changed 2, unchanged 0, held as deleted 0, deletions applied 2.", output, StringComparison.Ordinal);

        var idsLine = Assert.Single(
            output.Split('\n'),
            line => line.Contains("INF]", StringComparison.Ordinal) && line.Contains("deletions applied for user ids", StringComparison.Ordinal));
        Assert.Contains(deleted, idsLine, StringComparison.Ordinal);
        Assert.Contains(alreadyDeleted, idsLine, StringComparison.Ordinal);
        Assert.DoesNotContain(kept, idsLine, StringComparison.Ordinal);

        Assert.DoesNotContain(Surname, output, StringComparison.Ordinal);
        Assert.DoesNotContain(Email, output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("o.deleted", output, StringComparison.Ordinal);
    }

    // A deletion that changes nothing — the id is already held as deleted — is not "applied": no ids line, a count of 0.
    [Fact]
    public async Task ADeletionRepeated_IsNotApplied_SoItNamesNoId()
    {
        var id = NewId();
        await SeedAsync(factory, [Deletion(id, BaseMoment)]);

        var output = await ConsoleCapture.RunAsync(factory, async push =>
        {
            using var response = await push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(Deletion(id, BaseMoment + 10))));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });

        Assert.Contains("received 1, added 0, changed 0, unchanged 1, held as deleted 0, deletions applied 0.", output, StringComparison.Ordinal);
        Assert.DoesNotContain("deletions applied for user ids", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APushWithoutDeletions_LogsNoIdsLine()
    {
        var output = await ConsoleCapture.RunAsync(factory, async push =>
        {
            using var response = await push.SendAsync(PushTestClient.Build(
                HttpMethod.Post, "changes", PushTestClient.ChangesBody(Push(NewId(), "Иван", "Новиков", null))));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });

        Assert.Contains("deletions applied 0.", output, StringComparison.Ordinal);
        Assert.DoesNotContain("deletions applied for user ids", output, StringComparison.Ordinal);
    }
}
