using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mustask.Domain.Dictionaries;
using Mustask.Domain.Identity;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Dictionaries;

/// <summary>
/// `PUT /api/dictionaries/languages/{code}`. Every test mints its own
/// language code (<see cref="UniqueLanguageCode"/>) and creates it directly
/// through <see cref="MustaskDbContext"/> so this file never depends on the
/// `CreateLanguage` slice's own endpoint wiring and never collides with the
/// seeded `ru`/`kk`/`en` that `GetLanguagesEndpointTests` reads through a
/// fixed `code=ru|code=kk|code=en` filter rather than an exact count over the
/// whole table. The one seeded language this file does touch is `ru` (the
/// default) -- only in a scenario that is refused before any write happens
/// (the default language cannot be deactivated), so its state is never
/// actually changed.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class UpdateLanguageEndpointTests
{
    private readonly MustaskApiFactory _factory;

    public UpdateLanguageEndpointTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    private Task<(HttpClient Client, string AccessToken)> LoginAsync(string prefix, params string[] permissionCodes)
    {
        // Keycloak-токен: права едут в токене, строк в базе не нужно.
        var client = _factory.CreateClient();
        var accessToken = KeycloakTestRealm.CreateToken(permissionCodes);

        return Task.FromResult((client, accessToken));
    }

    /// <summary>
    /// A fresh 2-letter primary subtag plus a 4-hex-digit secondary subtag --
    /// well inside BCP-47 (2-3 letters, optional `-` groups) and the 8-char
    /// ceiling, with enough entropy that two calls never collide within one
    /// run of this suite (mirrors `CreateLanguageEndpointTests`).
    /// </summary>
    private static string UniqueLanguageCode() => $"zz-{Guid.NewGuid():N}"[..7];

    /// <summary>Creates a language row directly, bypassing `POST /languages` (covered by `CreateLanguageEndpointTests`), and returns its `version` for the first `If-Match`.</summary>
    private async Task<string> CreateLanguageDirectlyAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        var language = Language.Create(Guid.CreateVersion7(), code, endonym: null, sortOrder: 900, DateTimeOffset.UtcNow, "test");
        db.Languages.Add(language);
        await db.SaveChangesAsync();

        return ((uint)db.Entry(language).Property("xmin").CurrentValue!).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Every test that creates its own `zz-*` row must undo it, or the shared
    /// `MustaskApiCollection` database accumulates rows other classes'
    /// assertions have to keep working around (`GetLanguagesEndpointTests`'s
    /// comment).
    /// </summary>
    private async Task RemoveLanguageDirectlyAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        var language = await db.Languages.SingleOrDefaultAsync(l => l.Code == code);
        if (language is null)
        {
            return;
        }

        db.Languages.Remove(language);
        await db.SaveChangesAsync();
    }

    private async Task<string> GetLanguageVersionAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        var language = await db.Languages.SingleAsync(l => l.Code == code);

        return ((uint)db.Entry(language).Property("xmin").CurrentValue!).ToString(CultureInfo.InvariantCulture);
    }

    private static HttpRequestMessage PutRequest(
        string? accessToken, string code, string? endonym, int? sortOrder, bool? isActive, string? ifMatch = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/dictionaries/languages/{code}")
        {
            Content = JsonContent.Create(new { endonym, sortOrder, isActive }),
        };

        if (accessToken is not null)
        {
            request.WithBearer(accessToken);
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{ifMatch}\"");
        }

        return request;
    }

    [Fact]
    public async Task Put_NoToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().SendAsync(
            PutRequest(null, "ru", "Русский", 10, true));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Put_AuthenticatedWithoutPermission_ReturnsForbidden()
    {
        var (client, token) = await LoginAsync("update-language-noperm");

        var response = await client.SendAsync(PutRequest(token, "ru", "Русский", 10, true));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Put_UnknownCode_ReturnsNotFound()
    {
        var (client, token) = await LoginAsync("update-language-unknown", PermissionCatalog.DictionariesLanguagesWrite);

        var response = await client.SendAsync(PutRequest(token, "not-a-real-language", "X", 10, true, "1"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("dictionaries.languages.not_found", body.GetProperty("errorCode").GetString());
    }

    /// <summary>The target already exists -- a missing `If-Match` is 400, never "creates it".</summary>
    [Fact]
    public async Task Put_NoIfMatch_ReturnsBadRequest_VersionRequired()
    {
        var code = UniqueLanguageCode();
        await CreateLanguageDirectlyAsync(code);

        try
        {
            var (client, token) = await LoginAsync("update-language-vr", PermissionCatalog.DictionariesLanguagesWrite);

            var response = await client.SendAsync(PutRequest(token, code, "Test", 40, false));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(
                "dictionaries.version_required",
                body.GetProperty("errors").GetProperty("ifMatch")[0].GetProperty("code").GetString());
        }
        finally
        {
            await RemoveLanguageDirectlyAsync(code);
        }
    }

    [Fact]
    public async Task Put_MatchingIfMatch_ReturnsOk_ReplacesEndonymSortOrderAndActivity()
    {
        var code = UniqueLanguageCode();
        var version = await CreateLanguageDirectlyAsync(code);

        try
        {
            var (client, token) = await LoginAsync("update-language-ok", PermissionCatalog.DictionariesLanguagesWrite);

            var response = await client.SendAsync(PutRequest(token, code, "Test Language", 40, true, version));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(code, body.GetProperty("code").GetString());
            Assert.Equal("Test Language", body.GetProperty("endonym").GetString());
            Assert.Equal(40, body.GetProperty("sortOrder").GetInt32());
            Assert.True(body.GetProperty("isActive").GetBoolean());
            Assert.NotEqual(version, body.GetProperty("version").GetString());
        }
        finally
        {
            await RemoveLanguageDirectlyAsync(code);
        }
    }

    /// <summary>`PUT /languages/{code}` normalizes the route parameter to lower case before lookup -- `RU` behaves exactly like `ru`.</summary>
    [Fact]
    public async Task Put_CodeInUpperCase_BehavesLikeLowerCase()
    {
        var code = UniqueLanguageCode();
        var version = await CreateLanguageDirectlyAsync(code);

        try
        {
            var (client, token) = await LoginAsync("update-language-uppercase", PermissionCatalog.DictionariesLanguagesWrite);

            var response = await client.SendAsync(PutRequest(token, code.ToUpperInvariant(), "Test Language", 40, true, version));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(code, body.GetProperty("code").GetString());
        }
        finally
        {
            await RemoveLanguageDirectlyAsync(code);
        }
    }

    [Fact]
    public async Task Put_WrongIfMatch_ReturnsConflict()
    {
        var code = UniqueLanguageCode();
        await CreateLanguageDirectlyAsync(code);

        try
        {
            var (client, token) = await LoginAsync("update-language-conflict", PermissionCatalog.DictionariesLanguagesWrite);

            var response = await client.SendAsync(PutRequest(token, code, "Test", 40, false, "999999999"));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("dictionaries.version_conflict", body.GetProperty("errorCode").GetString());
        }
        finally
        {
            await RemoveLanguageDirectlyAsync(code);
        }
    }

    /// <summary>An absent `isActive` must not silently deactivate the language -- 400 `value_required` on that field.</summary>
    [Fact]
    public async Task Put_MissingIsActive_ReturnsBadRequest_ValueRequired()
    {
        var code = UniqueLanguageCode();
        var version = await CreateLanguageDirectlyAsync(code);

        try
        {
            var (client, token) = await LoginAsync("update-language-missing-active", PermissionCatalog.DictionariesLanguagesWrite);

            var response = await client.SendAsync(PutRequest(token, code, "Test", 40, null, version));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(
                "dictionaries.value_required",
                body.GetProperty("errors").GetProperty("isActive")[0].GetProperty("code").GetString());
        }
        finally
        {
            await RemoveLanguageDirectlyAsync(code);
        }
    }

    /// <summary>An absent `sortOrder` must not silently reset it to 0 -- 400 `value_required` on that field.</summary>
    [Fact]
    public async Task Put_MissingSortOrder_ReturnsBadRequest_ValueRequired()
    {
        var code = UniqueLanguageCode();
        var version = await CreateLanguageDirectlyAsync(code);

        try
        {
            var (client, token) = await LoginAsync("update-language-missing-sort", PermissionCatalog.DictionariesLanguagesWrite);

            var response = await client.SendAsync(PutRequest(token, code, "Test", null, true, version));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(
                "dictionaries.value_required",
                body.GetProperty("errors").GetProperty("sortOrder")[0].GetProperty("code").GetString());
        }
        finally
        {
            await RemoveLanguageDirectlyAsync(code);
        }
    }

    /// <summary>"Активация языка без эндонима".</summary>
    [Fact]
    public async Task Put_ActivateWithoutEndonym_ReturnsBadRequest_EndonymRequired()
    {
        var code = UniqueLanguageCode();
        var version = await CreateLanguageDirectlyAsync(code);

        try
        {
            var (client, token) = await LoginAsync("update-language-noendonym", PermissionCatalog.DictionariesLanguagesWrite);

            var response = await client.SendAsync(PutRequest(token, code, null, 40, true, version));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(
                "dictionaries.languages.endonym_required",
                body.GetProperty("errors").GetProperty("endonym")[0].GetProperty("code").GetString());
        }
        finally
        {
            await RemoveLanguageDirectlyAsync(code);
        }
    }

    /// <summary>"Добавление языка" continued / "повторная активация": a non-empty endonym lets activation succeed.</summary>
    [Fact]
    public async Task Put_ActivateWithEndonym_ReturnsOk_IsActiveTrue()
    {
        var code = UniqueLanguageCode();
        var version = await CreateLanguageDirectlyAsync(code);

        try
        {
            var (client, token) = await LoginAsync("update-language-activate", PermissionCatalog.DictionariesLanguagesWrite);

            var response = await client.SendAsync(PutRequest(token, code, "Test", 40, true, version));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(body.GetProperty("isActive").GetBoolean());
        }
        finally
        {
            await RemoveLanguageDirectlyAsync(code);
        }
    }

    /// <summary>"Язык по умолчанию нельзя выключить" -- refused before any write, `ru`'s own state stays untouched.</summary>
    [Fact]
    public async Task Put_DeactivateDefaultLanguage_ReturnsConflict_DefaultCannotBeDeactivated()
    {
        var version = await GetLanguageVersionAsync("ru");
        var (client, token) = await LoginAsync("update-language-default", PermissionCatalog.DictionariesLanguagesWrite);

        var response = await client.SendAsync(PutRequest(token, "ru", "Русский", 10, false, version));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("dictionaries.languages.default_cannot_be_deactivated", body.GetProperty("errorCode").GetString());
    }

    /// <summary>The endonym-required rule only binds an *active* language -- clearing it is allowed once the same call deactivates it.</summary>
    [Fact]
    public async Task Put_ClearEndonymWhileDeactivating_ReturnsOk_EmptyEndonym()
    {
        var code = UniqueLanguageCode();
        var version = await CreateLanguageDirectlyAsync(code);

        try
        {
            var (client, token) = await LoginAsync("update-language-clear", PermissionCatalog.DictionariesLanguagesWrite);
            var activateResponse = await client.SendAsync(PutRequest(token, code, "Test", 40, true, version));
            var activatedVersion = (await activateResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetString();

            var response = await client.SendAsync(PutRequest(token, code, null, 40, false, activatedVersion));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(string.Empty, body.GetProperty("endonym").GetString());
            Assert.False(body.GetProperty("isActive").GetBoolean());
        }
        finally
        {
            await RemoveLanguageDirectlyAsync(code);
        }
    }

    /// <summary>A non-default language can be switched off; the row stays, only its flag changes.</summary>
    [Fact]
    public async Task Put_DeactivateNonDefaultLanguage_ReturnsOk_IsActiveFalse()
    {
        var code = UniqueLanguageCode();
        var version = await CreateLanguageDirectlyAsync(code);

        try
        {
            var (client, token) = await LoginAsync("update-language-deactivate", PermissionCatalog.DictionariesLanguagesWrite);
            var activateResponse = await client.SendAsync(PutRequest(token, code, "Test", 40, true, version));
            var activatedVersion = (await activateResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetString();

            var response = await client.SendAsync(PutRequest(token, code, "Test", 40, false, activatedVersion));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(body.GetProperty("isActive").GetBoolean());
            Assert.Equal("Test", body.GetProperty("endonym").GetString());
        }
        finally
        {
            await RemoveLanguageDirectlyAsync(code);
        }
    }

    /// <summary>
    /// Авторство пишется из claim "name" токена, а не демо-заглушка ("demo" —
    /// единственное значение, которое туда попадало раньше, потому что
    /// прежний собственный access-токен claim'а "name" не нёс вовсе).
    /// Проверяется и в теле ответа, и в самой строке таблицы — единственный
    /// сценарий, где видно, что авторство из токена дошло до данных.
    /// </summary>
    [Fact]
    public async Task Put_MatchingIfMatch_RecordsAuthorNameFromToken_NotDemo()
    {
        var code = UniqueLanguageCode();
        var version = await CreateLanguageDirectlyAsync(code);

        try
        {
            var client = _factory.CreateClient();
            var token = KeycloakTestRealm.CreateToken(
                [PermissionCatalog.DictionariesLanguagesWrite],
                configure: t => t.Name = "Инспектор Иванов");

            var response = await client.SendAsync(PutRequest(token, code, "Test Language", 40, true, version));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Инспектор Иванов", body.GetProperty("updatedBy").GetString());
            Assert.NotEqual("demo", body.GetProperty("updatedBy").GetString());

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
            var language = await db.Languages.SingleAsync(l => l.Code == code);
            Assert.Equal("Инспектор Иванов", language.UpdatedBy);
        }
        finally
        {
            await RemoveLanguageDirectlyAsync(code);
        }
    }
}
