using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory.Live;

/// <summary>
/// mod-014 L security pass (review.md, "Security pass — backend"): Keycloak's
/// own log, provider lines included, over an accepted push, a refused one
/// (401) and a dropped one (400), carries the user's id but never the push
/// secret, the user's name, username or e-mail (US-6 "The credential is never
/// printed"; NFR Logging). The developer's live tests check that the refusal
/// names the option, not that the value and the person stay out.
/// </summary>
[Trait("Category", "KeycloakProvider")]
[Collection(LiveKeycloakCollection.Name)]
public sealed class LiveProviderLogProbeTests(LiveKeycloakFixture keycloak)
{
    private static readonly TimeSpan Fresh = TimeSpan.FromSeconds(30);

    private static string Id(JsonElement entry) =>
        entry.TryGetProperty("id", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;

    [LiveProviderFact]
    public async Task KeycloaksLog_OverAcceptedRefusedAndDroppedPushes_NamesIdsOnly_NeverTheSecretOrAPerson()
    {
        try
        {
            var (accepted, _) = await keycloak.CreateUserAsync(User("probe.accepted", "Принятая", "Пробная", "probe.accepted@example.test"));
            await keycloak.Receiver.WaitForEntryAsync(accepted, _ => true, Fresh);

            keycloak.Receiver.Answer = push => push.Entries.Count > 0 ? (401, TimeSpan.Zero) : (200, TimeSpan.Zero);
            var (refused, _) = await keycloak.CreateUserAsync(User("probe.refused", "Отказная", "Пробная", "probe.refused@example.test"));
            await keycloak.Receiver.WaitForAsync(push => push.Answered == 401 && push.Entries.Any(entry => Id(entry) == refused), Fresh);

            keycloak.Receiver.Answer = push => push.Target == "changes" ? (400, TimeSpan.Zero) : (200, TimeSpan.Zero);
            var (dropped, _) = await keycloak.CreateUserAsync(User("probe.dropped", "Сброшенная", "Пробная", "probe.dropped@example.test"));
            await keycloak.Receiver.WaitForAsync(push => push.Answered == 400 && push.Entries.Any(entry => Id(entry) == dropped), Fresh);

            // Give the provider a moment to write its lines for the last answer.
            await Task.Delay(TimeSpan.FromSeconds(2));

            var log = await keycloak.LogsAsync();

            Assert.Contains(refused, log, StringComparison.Ordinal);
            Assert.Contains(dropped, log, StringComparison.Ordinal);
            Assert.DoesNotContain(keycloak.PushSecret, log, StringComparison.Ordinal);

            foreach (var personal in new[]
            {
                "Принятая", "Отказная", "Сброшенная", "Пробная",
                "probe.accepted", "probe.refused", "probe.dropped",
            })
            {
                Assert.DoesNotContain(personal, log, StringComparison.Ordinal);
            }
        }
        finally
        {
            keycloak.Receiver.Answer = _ => (200, TimeSpan.Zero);
        }
    }

    private static JsonObject User(string username, string firstName, string lastName, string email) => new()
    {
        ["username"] = username,
        ["firstName"] = firstName,
        ["lastName"] = lastName,
        ["email"] = email,
        ["enabled"] = true,
    };
}
