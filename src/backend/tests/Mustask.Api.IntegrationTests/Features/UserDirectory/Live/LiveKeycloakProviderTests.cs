using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory.Live;

/// <summary>
/// mod-014 plan § 10, level L: the provider inside the built Keycloak image,
/// pushing to a receiver that checks every signature as MUSTASK does.
/// Opt-in (<see cref="LiveProviderFactAttribute"/>); one Keycloak for the
/// class, so each test works with users of its own.
/// </summary>
[Trait("Category", "KeycloakProvider")]
[Collection(LiveKeycloakCollection.Name)]
public sealed class LiveKeycloakProviderTests(LiveKeycloakFixture keycloak)
{
    private static readonly TimeSpan Fresh = TimeSpan.FromSeconds(15);

    private static JsonObject User(string username, string firstName, string lastName, string email, bool enabled = true) => new()
    {
        ["username"] = username,
        ["firstName"] = firstName,
        ["lastName"] = lastName,
        ["email"] = email,
        ["enabled"] = enabled,
    };

    private static string Text(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;

    // US-2 "A user created in the console becomes findable" — the push carries the user's whole state, id = Keycloak's id.
    [LiveProviderFact]
    public async Task ACreatedUser_IsPushedSignedWithItsFields()
    {
        var (id, _) = await keycloak.CreateUserAsync(User("live.anna", "Анна", "Смирнова", "live.anna@example.test"));

        var entry = await keycloak.Receiver.WaitForEntryAsync(id, _ => true, Fresh);

        Assert.Equal("live.anna", Text(entry, "username"));
        Assert.Equal("Анна", Text(entry, "firstName"));
        Assert.Equal("Смирнова", Text(entry, "lastName"));
        Assert.Equal("live.anna@example.test", Text(entry, "email"));
        Assert.True(entry.GetProperty("enabled").GetBoolean());
        Assert.All(keycloak.Receiver.Received, push => Assert.True(push.SignedRight, "every request is signed with the shared secret"));
    }

    // NFR Freshness ("with both sides up, a change in Keycloak shows in the read model within 10 s") from the moment
    // Keycloak is ready — code review F13: the provider used to hold every change until a first enablement check 20 s
    // after its start, so a change made in the first ~15 s after readiness waited for it. The fixture measures this
    // before any test runs; the receiver stands in for MUSTASK, so its push must arrive inside the bound.
    [LiveProviderFact]
    public void AChangeMadeAsKeycloakBecomesReady_IsPushedWithinTheFreshnessBound()
    {
        Assert.NotNull(keycloak.FirstPushAfterReady);
        Assert.True(
            keycloak.FirstPushAfterReady < TimeSpan.FromSeconds(10),
            $"the push of a user created as Keycloak became ready arrived {keycloak.FirstPushAfterReady!.Value.TotalSeconds:F1} s after the change");
    }

    // US-2 "A change of name / e-mail is followed", "A change of username alone moves the fallback display name" (D-15: this realm allows it).
    [LiveProviderFact]
    public async Task NameEmailAndUsernameChanges_ArePushed()
    {
        var (id, _) = await keycloak.CreateUserAsync(User("live.ivan", "Иван", "Петров", "live.ivan@example.test"));
        await keycloak.Receiver.WaitForEntryAsync(id, _ => true, Fresh);

        await keycloak.AdminAsync(HttpMethod.Put, $"/admin/realms/mustask/users/{id}", new JsonObject
        {
            ["username"] = "live.ivan.renamed",
            ["firstName"] = "Иван",
            ["lastName"] = "Сидоров",
            ["email"] = "live.ivan.new@example.test",
        });

        var entry = await keycloak.Receiver.WaitForEntryAsync(id, candidate => Text(candidate, "lastName") == "Сидоров", Fresh);

        Assert.Equal("live.ivan.renamed", Text(entry, "username"));
        Assert.Equal("live.ivan.new@example.test", Text(entry, "email"));
    }

    // US-2 "A user created already disabled is inactive from the start"; US-3 disabled / enabled again.
    [LiveProviderFact]
    public async Task DisableAndEnable_ArePushedAsTheEnabledFlag()
    {
        var (id, _) = await keycloak.CreateUserAsync(User("live.olga", "Ольга", "Белова", "live.olga@example.test", enabled: false));

        await keycloak.Receiver.WaitForEntryAsync(id, entry => !entry.GetProperty("enabled").GetBoolean(), Fresh);

        await keycloak.AdminAsync(HttpMethod.Put, $"/admin/realms/mustask/users/{id}", new JsonObject { ["enabled"] = true });

        await keycloak.Receiver.WaitForEntryAsync(id, entry => entry.GetProperty("enabled").GetBoolean(), Fresh);
    }

    // US-3 "A deleted account becomes inactive for good and keeps its name" — the deletion carries the last state.
    [LiveProviderFact]
    public async Task ADeletion_IsPushedCarryingTheLastState()
    {
        var (id, _) = await keycloak.CreateUserAsync(User("live.petr", "Пётр", "Волков", "live.petr@example.test"));
        await keycloak.Receiver.WaitForEntryAsync(id, _ => true, Fresh);

        await keycloak.AdminAsync(HttpMethod.Delete, $"/admin/realms/mustask/users/{id}");

        var deletion = await keycloak.Receiver.WaitForEntryAsync(
            id, entry => entry.TryGetProperty("deleted", out var deleted) && deleted.GetBoolean(), Fresh);

        Assert.Equal("Волков", Text(deletion, "lastName"));
        Assert.Equal("live.petr", Text(deletion, "username"));
    }

    // US-2 "A change outside the projected fields leaves the projection as it is" — a role mapping pushes nothing.
    [LiveProviderFact]
    public async Task ARoleMapping_PushesNothing()
    {
        var (id, _) = await keycloak.CreateUserAsync(User("live.elena", "Елена", "Лебедева", "live.elena@example.test"));
        await keycloak.Receiver.WaitForEntryAsync(id, _ => true, Fresh);
        var before = keycloak.Receiver.ChangeEntriesFor(id).Count();

        var (_, role, _) = await keycloak.AdminAsync(HttpMethod.Get, "/admin/realms/mustask/roles/offline_access");
        var (status, _, _) = await keycloak.AdminAsync(HttpMethod.Post, $"/admin/realms/mustask/users/{id}/role-mappings/realm", new JsonArray(JsonNode.Parse(role)));
        Assert.Equal(204, status);
        await Task.Delay(TimeSpan.FromSeconds(3));

        Assert.Equal(before, keycloak.Receiver.ChangeEntriesFor(id).Count());
    }

    // US-2 "A service-account user is never projected" — neither by an event nor by a reconciliation.
    [LiveProviderFact]
    public async Task TheServiceAccountUser_IsNeverPushed()
    {
        var (_, users, _) = await keycloak.AdminAsync(
            HttpMethod.Get, $"/admin/realms/mustask/users?username=service-account-{LiveKeycloakFixture.ServiceAccountClientId}&exact=true");
        var serviceAccountId = JsonNode.Parse(users)!.AsArray().Single()!["id"]!.GetValue<string>();

        await WaitForACompletedSnapshotAsync();

        Assert.Empty(keycloak.Receiver.EntriesFor(serviceAccountId));
    }

    // US-2 "A user of another realm is never projected".
    [LiveProviderFact]
    public async Task AUserOfTheMasterRealm_IsNeverPushed()
    {
        var (id, _) = await keycloak.CreateUserAsync(User("live.master", "Мастер", "Реалм", "live.master@example.test"), realm: "master");

        await Task.Delay(TimeSpan.FromSeconds(3));

        Assert.Empty(keycloak.Receiver.EntriesFor(id));
    }

    // US-4 "Existing users are loaded into an empty projection": begin, pages, completion — a snapshot the receiver can prove whole.
    [LiveProviderFact]
    public async Task TheStartReconciliation_SendsAWholeSnapshot()
    {
        var completion = await WaitForACompletedSnapshotAsync();
        var snapshot = completion.Target[..completion.Target.IndexOf("/completion", StringComparison.Ordinal)];

        var begin = keycloak.Receiver.Received.Single(push => push.Target == snapshot);
        var pages = keycloak.Receiver.Received.Where(push => push.Target.StartsWith(snapshot + "/pages/", StringComparison.Ordinal)).ToList();

        using var counts = JsonDocument.Parse(completion.Body);
        Assert.Equal(pages.Count, counts.RootElement.GetProperty("pageCount").GetInt32());
        Assert.Equal(pages.Sum(page => page.Entries.Count), counts.RootElement.GetProperty("entryCount").GetInt32());
        Assert.True(begin.At <= pages.Select(page => page.At).DefaultIfEmpty(completion.At).Min());
        Assert.All(pages.SelectMany(page => page.Entries), entry => Assert.False(entry.TryGetProperty("deleted", out _)));
    }

    // Security pass S1 (review.md): a user can put an unpaired surrogate in their own name — Keycloak's account REST
    // accepts it (204) — and MUSTASK's reader refuses the escape it becomes in JSON (400; the receiver answers so, as
    // MUSTASK does). The provider makes every projected string well-formed before it queues it, so the push is
    // accepted and the reconciliation after an outage completes with the user on its page.
    [LiveProviderFact]
    public async Task AUsersOwnNameWithAnUnpairedSurrogate_IsPushedWellFormedAndAccepted_AndReconciliationStillCompletes()
    {
        var password = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(12));
        var created = User("live.selfservice", "Первый", "Исходный", "live.selfservice@example.test");
        created["credentials"] = new JsonArray(new JsonObject { ["type"] = "password", ["value"] = password, ["temporary"] = false });
        var (id, _) = await keycloak.CreateUserAsync(created);
        await keycloak.Receiver.WaitForEntryAsync(id, _ => true, Fresh);

        var token = await keycloak.UserTokenAsync("live.selfservice", password);
        var (status, body) = await keycloak.UpdateOwnProfileAsync(
            token,
            """{"username":"live.selfservice","firstName":"Сам\uD800","lastName":"Смирнов","email":"live.selfservice@example.test"}""");
        Assert.True(status == 204, $"Keycloak's account REST is expected to accept an unpaired surrogate (204), it answered {status}: {body}");

        var entry = await keycloak.Receiver.WaitForEntryAsync(id, candidate => Text(candidate, "lastName") == "Смирнов", Fresh);
        Assert.Equal("Сам�", Text(entry, "firstName"));

        try
        {
            // An outage and its recovery: the reconciliation that follows reads this user again, onto a page.
            keycloak.Receiver.Answer = _ => (401, TimeSpan.Zero);
            var (triggerId, _) = await keycloak.CreateUserAsync(User("live.selfservice.trigger", "Повод", "Сбоя", "live.selfservice.trigger@example.test"));
            await keycloak.Receiver.WaitForAsync(push => push.Answered == 401 && push.Entries.Any(item => Text(item, "id") == triggerId), Fresh);
            var recovered = DateTimeOffset.UtcNow;
            keycloak.Receiver.Answer = _ => (200, TimeSpan.Zero);

            var completion = await keycloak.Receiver.WaitForAsync(
                push => push.At > recovered && push.Answered == 200 && push.Target.EndsWith("/completion", StringComparison.Ordinal),
                TimeSpan.FromSeconds(120));
            var snapshot = completion.Target[..completion.Target.IndexOf("/completion", StringComparison.Ordinal)];
            var onPages = keycloak.Receiver.Received
                .Where(push => push.Target.StartsWith(snapshot + "/pages/", StringComparison.Ordinal))
                .SelectMany(push => push.Entries)
                .Where(item => Text(item, "id") == id)
                .ToList();

            Assert.Equal("Сам�", Text(Assert.Single(onPages), "firstName"));
            Assert.DoesNotContain(keycloak.Receiver.Received, push => push.Answered == 400 && push.Body.Contains(id, StringComparison.Ordinal));
        }
        finally
        {
            keycloak.Receiver.Answer = _ => (200, TimeSpan.Zero);
        }
    }

    // US-5 "Keycloak does not wait for a slow MUSTASK"; US-6 "A provider with the wrong credential fails loudly and recovers".
    [LiveProviderFact]
    public async Task ASlowOrRefusingMustask_NeitherDelaysKeycloakNorLosesTheChange()
    {
        try
        {
            await RefusedThenSlowAsync();
        }
        finally
        {
            keycloak.Receiver.Answer = _ => (200, TimeSpan.Zero);
        }
    }

    private async Task RefusedThenSlowAsync()
    {
        keycloak.Receiver.Answer = _ => (401, TimeSpan.Zero);

        var (refusedId, refusedElapsed) = await keycloak.CreateUserAsync(User("live.refused", "Отказ", "Ключа", "live.refused@example.test"));
        await keycloak.Receiver.WaitForAsync(push => push.Answered == 401 && push.Entries.Any(entry => Text(entry, "id") == refusedId), Fresh);
        Assert.True(refusedElapsed < TimeSpan.FromSeconds(2), $"creating a user took {refusedElapsed.TotalSeconds:F1} s while MUSTASK refused");
        Assert.Contains("push-secret", await keycloak.LogsAsync(), StringComparison.Ordinal);

        keycloak.Receiver.Answer = _ => (200, TimeSpan.FromSeconds(12));
        var (_, slowElapsed) = await keycloak.CreateUserAsync(User("live.slow", "Медленный", "Ответ", "live.slow@example.test"));
        Assert.True(slowElapsed < TimeSpan.FromSeconds(2), $"creating a user took {slowElapsed.TotalSeconds:F1} s while MUSTASK was slow");

        keycloak.Receiver.Answer = _ => (200, TimeSpan.Zero);
        await keycloak.Receiver.WaitForEntryAsync(refusedId, _ => true, TimeSpan.FromSeconds(90));
    }

    private async Task<ReceivedPush> WaitForACompletedSnapshotAsync() =>
        await keycloak.Receiver.WaitForAsync(
            push => push.Answered == 200 && push.Target.EndsWith("/completion", StringComparison.Ordinal),
            TimeSpan.FromSeconds(120));
}

/// <summary>One live Keycloak for the live tests.</summary>
[CollectionDefinition(Name)]
public sealed class LiveKeycloakCollection : ICollectionFixture<LiveKeycloakFixture>
{
    public const string Name = "Live Keycloak provider";
}
