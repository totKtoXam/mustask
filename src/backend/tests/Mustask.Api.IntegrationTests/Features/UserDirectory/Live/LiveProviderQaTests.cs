using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Mustask.Api.Features.UserDirectory;
using Mustask.Domain.UserDirectory;
using ChangesRequest = Mustask.Api.Features.UserDirectory.UserDirectoryEndpoints.ChangesRequest;
using PageRequest = Mustask.Api.Features.UserDirectory.UserDirectoryEndpoints.PageRequest;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory.Live;

/// <summary>
/// QA re-check 3 (qa.md, RC3-4..RC3-6, RC3-10): the security pass's S1 fix in the built Keycloak image, against what the
/// developer's live test does not do — an emoji (a valid surrogate pair) in a name, a lone half in more than the first
/// name, the removal path, and above all MUSTASK's own request types and validation applied to every body the provider
/// sent (the receiver in the fixture is a stand-in for MUSTASK's reader; here the real DTOs and the real
/// <see cref="UserPushValidation"/> read the bytes). Opt-in, like the other live tests.
/// </summary>
[Trait("Category", "KeycloakProvider")]
[Collection(LiveKeycloakCollection.Name)]
public sealed class LiveProviderQaTests(LiveKeycloakFixture keycloak)
{
    private static readonly TimeSpan Fresh = TimeSpan.FromSeconds(30);

    private static string Text(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;

    private async Task<(int Status, string Body, string? Id)> SendRawAsync(HttpMethod method, string path, string? rawJson)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await keycloak.AdminTokenAsync());

        if (rawJson is not null)
        {
            request.Content = new StringContent(rawJson, Encoding.UTF8, "application/json");
        }

        using var response = await keycloak.Http.SendAsync(request);

        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(), response.Headers.Location?.Segments[^1]);
    }

    private async Task<string> CreateAsync(string rawJson)
    {
        var (status, body, id) = await SendRawAsync(HttpMethod.Post, "/admin/realms/mustask/users", rawJson);
        Assert.True(status == 201 && id is not null, $"creating a user answered {status}: {body}");

        return id;
    }

    // An emoji and a character of the supplementary ideographic plane are valid pairs: they come out as sent, in the
    // push body as UTF-8 (not as escapes), and lone halves in the first name, the last name, the e-mail and the username
    // come out as U+FFFD wherever Keycloak accepts them — on an event, on a removal and on a reconciliation page. Every
    // body the provider sent in this fixture's life is then read by MUSTASK's own request types and validation.
    [LiveProviderFact]
    public async Task EmojiSurviveAndLoneHalvesAreCleaned_OnEveryPath_AndMustasksOwnReaderAndValidationTakeEveryBody()
    {
        var started = DateTimeOffset.UtcNow;
        var emoji = await CreateAsync(
            """{"username":"qa.emoji","firstName":"Эмодзи 😀","lastName":"𠀀 Ё","email":"qa.emoji@example.test","enabled":true}""");
        var lone = await CreateAsync(
            """{"username":"qa.lone","firstName":"\uD83D","lastName":"Лон\uDC00ный","email":"qa.lone@example.test","enabled":true}""");

        // Whether Keycloak takes a lone half in the e-mail or the username is Keycloak's: the test records the answer
        // and, where it is 201, asserts the same cleaning.
        var loneEmailAnswer = await SendRawAsync(
            HttpMethod.Post, "/admin/realms/mustask/users", """{"username":"qa.loneemail","firstName":"Е","lastName":"Е","email":"qa\uD800@example.test","enabled":true}""");
        var loneUsernameAnswer = await SendRawAsync(
            HttpMethod.Post, "/admin/realms/mustask/users", """{"username":"qa.lone\uDC00name","firstName":"У","lastName":"У","email":"qa.loneusername@example.test","enabled":true}""");

        var emojiEntry = await keycloak.Receiver.WaitForEntryAsync(emoji, _ => true, Fresh);
        Assert.Equal("Эмодзи \U0001F600", Text(emojiEntry, "firstName"));
        Assert.Equal("\U00020000 Ё", Text(emojiEntry, "lastName"));

        var loneEntry = await keycloak.Receiver.WaitForEntryAsync(lone, _ => true, Fresh);
        Assert.Equal("�", Text(loneEntry, "firstName"));
        Assert.Equal("Лон�ный", Text(loneEntry, "lastName"));

        if (loneEmailAnswer.Status == 201)
        {
            var entry = await keycloak.Receiver.WaitForEntryAsync(loneEmailAnswer.Id!, _ => true, Fresh);
            Assert.Equal("qa�@example.test", Text(entry, "email"));
        }

        if (loneUsernameAnswer.Status == 201)
        {
            var entry = await keycloak.Receiver.WaitForEntryAsync(loneUsernameAnswer.Id!, _ => true, Fresh);
            Assert.Equal("qa.lone�name", Text(entry, "username"));
        }

        // The removal path: the deletion carries the last state, cleaned.
        var (deleteStatus, deleteBody, _) = await SendRawAsync(HttpMethod.Delete, $"/admin/realms/mustask/users/{lone}", null);
        Assert.True(deleteStatus == 204, $"deleting the user answered {deleteStatus}: {deleteBody}");
        var deletion = await keycloak.Receiver.WaitForEntryAsync(lone, entry => entry.TryGetProperty("deleted", out var deleted) && deleted.ValueKind == JsonValueKind.True, Fresh);
        Assert.Equal("�", Text(deletion, "firstName"));
        Assert.Equal("Лон�ный", Text(deletion, "lastName"));

        try
        {
            // The page path: an outage and its recovery — the reconciliation reads the realm again, onto pages.
            keycloak.Receiver.Answer = _ => (401, TimeSpan.Zero);
            var trigger = await CreateAsync("""{"username":"qa.trigger","firstName":"Повод","lastName":"Сбоя","email":"qa.trigger@example.test","enabled":true}""");
            await keycloak.Receiver.WaitForAsync(push => push.Answered == 401 && push.Entries.Any(item => Text(item, "id") == trigger), Fresh);
            var recovered = DateTimeOffset.UtcNow;
            keycloak.Receiver.Answer = _ => (200, TimeSpan.Zero);

            var completion = await keycloak.Receiver.WaitForAsync(
                push => push.At > recovered && push.Answered == 200 && push.Target.EndsWith("/completion", StringComparison.Ordinal),
                TimeSpan.FromSeconds(120));
            var snapshot = completion.Target[..completion.Target.IndexOf("/completion", StringComparison.Ordinal)];
            var onPages = keycloak.Receiver.Received
                .Where(push => push.Target.StartsWith(snapshot + "/pages/", StringComparison.Ordinal))
                .SelectMany(push => push.Entries)
                .ToList();

            var emojiOnPage = Assert.Single(onPages, item => Text(item, "id") == emoji);
            Assert.Equal("Эмодзи \U0001F600", Text(emojiOnPage, "firstName"));
            Assert.Equal("\U00020000 Ё", Text(emojiOnPage, "lastName"));
            Assert.DoesNotContain(onPages, item => Text(item, "id") == lone);
        }
        finally
        {
            keycloak.Receiver.Answer = _ => (200, TimeSpan.Zero);
        }

        // Every body of this test's own window (the earlier tests of the shared fixture have their own checks).
        AssertMustasksReaderTakesEveryBody(push => push.At >= started);
    }

    // D-5's reach: it takes no administrator. A user puts U+FFFE in their own first name through the account REST API,
    // exactly as S1's lone surrogate got in, and Keycloak answers 204 — the other half of D-5's reproduction: it says
    // that the character is reachable by anyone who can sign in (`manage-account` is a default role), and, since the
    // fix, that what the provider pushes for it is U+FFFD in a body MUSTASK takes. If Keycloak ever refuses it, D-5
    // turns into an operator-only case and this test says so by failing.
    [LiveProviderFact]
    public async Task AUser_CanPutTheNoncharacterU_FFFE_InTheirOwnName_AndKeycloakAcceptsIt()
    {
        var password = Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(12));
        var id = await CreateAsync(
            $$"""{"username":"qa.selfservice.fffe","firstName":"Первый","lastName":"Исходный","email":"qa.selfservice.fffe@example.test","enabled":true,"credentials":[{"type":"password","value":"{{password}}","temporary":false}]}""");

        try
        {
            await keycloak.Receiver.WaitForEntryAsync(id, _ => true, Fresh);

            var token = await keycloak.UserTokenAsync("qa.selfservice.fffe", password);
            var (status, body) = await keycloak.UpdateOwnProfileAsync(
                token,
                """{"username":"qa.selfservice.fffe","firstName":"Сам￾И","lastName":"Смирнов","email":"qa.selfservice.fffe@example.test"}""");

            Assert.True(status == 204, $"Keycloak's account REST is expected to accept U+FFFE in a first name (204), it answered {status}: {body}");

            var pushed = await keycloak.Receiver.WaitForEntryAsync(id, candidate => Text(candidate, "lastName") == "Смирнов", Fresh);

            // D-5 fixed: the provider sends U+FFFD where Keycloak holds U+FFFE (MUSTASK refuses a noncharacter, a 400 that
            // would drop the batch), and the body is one MUSTASK takes — validation and derivation included.
            Assert.Equal("Сам�И", Text(pushed, "firstName"));
            AssertMustasksReaderTakesEveryBody(push => push.Body.Contains(id, StringComparison.Ordinal));
        }
        finally
        {
            // Deleted again, and the deletion awaited, so that no later reconciliation page or window of this fixture carries it.
            await SendRawAsync(HttpMethod.Delete, $"/admin/realms/mustask/users/{id}", null);
            await keycloak.Receiver.WaitForEntryAsync(id, entry => entry.TryGetProperty("deleted", out var deleted) && deleted.ValueKind == JsonValueKind.True, Fresh);
        }
    }

    // The same question for a character no earlier test asked about: U+FFFE, a noncharacter. Keycloak's person-name and
    // e-mail checks do not list it; MUSTASK's validation accepts it; and deriving the projection's own columns from it
    // (the normalised search name and e-mail) throws in .NET's normaliser — a 500, which the provider retries without
    // end. Whatever Keycloak accepts, MUSTASK must be able to take: the test records what Keycloak does and, for each
    // field it takes, asks MUSTASK's reader, validation and derivation to take the body the provider sent.
    [LiveProviderFact]
    public async Task ANoncharacterTheValidationAccepts_InAName_IsPushedInAFormMustaskCanDerive()
    {
        var uFffe = "\\uFFFE";
        var created = new Dictionary<string, (int Status, string Body, string? Id)>
        {
            ["firstName"] = await SendRawAsync(
                HttpMethod.Post, "/admin/realms/mustask/users", $$"""{"username":"qa.fffe.first","firstName":"A{{uFffe}}B","lastName":"Л","email":"qa.fffe.first@example.test","enabled":true}"""),
            ["lastName"] = await SendRawAsync(
                HttpMethod.Post, "/admin/realms/mustask/users", $$"""{"username":"qa.fffe.last","firstName":"И","lastName":"C{{uFffe}}D","email":"qa.fffe.last@example.test","enabled":true}"""),
            ["email"] = await SendRawAsync(
                HttpMethod.Post, "/admin/realms/mustask/users", $$"""{"username":"qa.fffe.email","firstName":"И","lastName":"Л","email":"e{{uFffe}}@example.test","enabled":true}"""),
            ["username"] = await SendRawAsync(
                HttpMethod.Post, "/admin/realms/mustask/users", $$"""{"username":"qa.fffe{{uFffe}}name","firstName":"И","lastName":"Л","email":"qa.fffe.username@example.test","enabled":true}"""),
        };

        var answers = string.Join("; ", created.Select(pair => $"{pair.Key} {pair.Value.Status}"));
        var accepted = created.Where(pair => pair.Value.Status == 201).ToList();

        var ids = accepted.Select(pair => pair.Value.Id!).ToList();

        try
        {
            foreach (var id in ids)
            {
                await keycloak.Receiver.WaitForEntryAsync(id, _ => true, Fresh);
            }

            // The question: would MUSTASK take every body that carries one of these users? (Whatever the fix does with
            // the character — refuse it at MUSTASK, or have the provider send something else — a body the provider sent
            // must be one MUSTASK takes.)
            Assert.NotEmpty(ids);
            AssertMustasksReaderTakesEveryBody(push => ids.Any(id => push.Body.Contains(id, StringComparison.Ordinal)), $"Keycloak answered: {answers}");
        }
        finally
        {
            // The users are deleted again so that no later reconciliation page of this fixture carries them.
            foreach (var id in ids)
            {
                await SendRawAsync(HttpMethod.Delete, $"/admin/realms/mustask/users/{id}", null);
                await keycloak.Receiver.WaitForEntryAsync(id, entry => entry.TryGetProperty("deleted", out var deleted) && deleted.ValueKind == JsonValueKind.True, Fresh);
            }
        }
    }

    /// <summary>
    /// The bytes of every request the provider has sent so far (that <paramref name="which"/> selects), through the real
    /// request types, the real validation — the push binder's deserialisation (<c>PushBodyReader.JsonOptions</c>) and
    /// <see cref="UserPushValidation"/> — and the real derivation of the projection's columns
    /// (<see cref="ProjectedUser.Create"/>): no exception, so MUSTASK would have answered 2xx, whatever the receiver
    /// stand-in did.
    /// </summary>
    private void AssertMustasksReaderTakesEveryBody(Func<ReceivedPush, bool> which, string context = "")
    {
        var checkedBodies = 0;

        foreach (var push in keycloak.Receiver.Received.Where(push => push.SignedRight && which(push)))
        {
            var bytes = Encoding.UTF8.GetBytes(push.Body);
            var isChanges = push.Target == "changes";
            var isPage = push.Target.Contains("/pages/", StringComparison.Ordinal);

            if (!isChanges && !isPage)
            {
                continue;
            }

            try
            {
                var entries = isChanges
                    ? JsonSerializer.Deserialize<ChangesRequest>(bytes, PushBodyReader.JsonOptions)?.Changes
                    : JsonSerializer.Deserialize<PageRequest>(bytes, PushBodyReader.JsonOptions)?.Entries;

                var now = DateTimeOffset.UtcNow;
                var pushes = UserPushValidation.Validate(entries, isChanges ? "changes" : "entries", isSnapshotPage: isPage, now);

                foreach (var user in pushes)
                {
                    _ = ProjectedUser.Create(user, now);
                }

                checkedBodies++;
            }
            catch (Exception ex)
            {
                Assert.Fail($"MUSTASK's reader refuses a body the provider sent ({push.Method} {push.Target}, answered {push.Answered}): {ex.GetType().Name} {ex.Message}. {context}");
            }
        }

        Assert.True(checkedBodies > 0, $"no body was checked. {context}");
    }
}
