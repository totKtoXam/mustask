using System.Net;
using Mustask.Domain.Identity;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014 US-6 "No MUSTASK user can write the projection" and "A sign-in
/// does not fill the projection", US-3 "The status authorizes nothing":
/// MUSTASK users' tokens reach no write path (BR-UP-1, BR-UP-12), a token
/// for an id the provider has not pushed leaves it unknown (BR-UP-17), and an
/// inactive status neither refuses nor admits a request.
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class UserDirectoryWriteSurfaceTests(MustaskApiFactory factory) : IAsyncLifetime
{
    private static readonly Guid ForgedSnapshot = Guid.Parse("0f0f0f0f-0000-4000-8000-000000000001");

    private readonly PushTestClient _push = new(factory);

    public Task InitializeAsync() => ResetAsync(factory);

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Every route this task adds, each with a well-formed body for it.</summary>
    public static TheoryData<string, string, string> Routes() => new()
    {
        { "POST", "changes", PushTestClient.ChangesBody(Push("forged", "Поддельный", "Пользователь", "forged@example.test")) },
        { "PUT", $"snapshots/{ForgedSnapshot}", $$"""{"enumerationStart":{{BaseMoment}}}""" },
        { "PUT", $"snapshots/{ForgedSnapshot}/pages/0", PushTestClient.PageBody(Push("forged", "Поддельный", "Пользователь", "forged@example.test")) },
        { "PUT", $"snapshots/{ForgedSnapshot}/completion", """{"pageCount":0,"entryCount":0}""" },
    };

    // US-6 "No MUSTASK user can write the projection" — every route, with a token of an ordinary user and of every code.
    [Theory]
    [MemberData(nameof(Routes))]
    public async Task EveryRoute_WithAUsersToken_IsRefusedAndChangesNothing(string method, string target, string body)
    {
        foreach (var codes in new[] { Array.Empty<string>(), PermissionCatalog.AllCodes.ToArray() })
        {
            var request = PushTestClient.Build(new HttpMethod(method), target, body, signed: false)
                .WithBearer(KeycloakTestRealm.CreateToken(codes));

            using var response = await _push.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        Assert.Equal(0, await CountAsync(factory));
        Assert.Equal(0, await SnapshotCountAsync(factory));
        Assert.Empty((await LookupAsync(factory, "forged")).Known);
    }

    // US-6 "A sign-in does not fill the projection".
    [Fact]
    public async Task ATokenForAnUnpushedUser_LeavesItUnknown()
    {
        var unpushed = NewId();
        var token = KeycloakTestRealm.CreateToken(PermissionCatalog.AllCodes, options =>
        {
            options.Subject = unpushed;
            options.Email = "new.user@example.test";
            options.GivenName = "Новый";
            options.FamilyName = "Пользователь";
        });
        var client = factory.CreateClient();

        foreach (var path in new[] { "/__test/current-user", "/api/dictionaries/languages" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path).WithBearer(token);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using (var push = await _push.SendAsync(
            PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(Push(unpushed, "Новый", "Пользователь", null)), signed: false)
                .WithBearer(token)))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, push.StatusCode);
        }

        var lookup = await LookupAsync(factory, unpushed);
        Assert.Empty(lookup.Known);
        Assert.Equal([unpushed], lookup.Unknown.ToArray());
    }

    // US-3 "The status authorizes nothing" — the same outcome before and after the projection marks the user inactive.
    [Fact]
    public async Task AnInactiveStatus_NeitherRefusesNorAdmits()
    {
        var id = NewId();
        await _push.PostChangesAsync(Push(id, "Иван", "Петров", null));
        var token = KeycloakTestRealm.CreateToken(configure: options => options.Subject = id);
        var client = factory.CreateClient();

        async Task<HttpStatusCode> CallAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/__test/current-user").WithBearer(token);
            using var response = await client.SendAsync(request);
            return response.StatusCode;
        }

        var before = await CallAsync();
        await _push.PostChangesAsync(Push(id, "Иван", "Петров", null, enabled: false, moment: BaseMoment + 1));
        var after = await CallAsync();

        Assert.False((await LookupAsync(factory, id)).Known[id].IsActive);
        Assert.Equal(HttpStatusCode.OK, before);
        Assert.Equal(before, after);
    }
}
