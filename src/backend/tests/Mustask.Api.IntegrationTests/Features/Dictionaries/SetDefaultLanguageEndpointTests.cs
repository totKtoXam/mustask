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
/// `PUT /api/dictionaries/languages/default` -- the one slice touching two
/// aggregates (the old and the new default language). Every test that needs
/// a language beyond the seeded `ru`/`kk`/`en` mints its own unique code (<see cref="UniqueLanguageCode"/>) and deletes the row directly
/// before returning (<see cref="DeleteLanguageDirectlyAsync"/>); every test
/// that actually changes the default restores it to `ru` before returning
/// (<see cref="RestoreDefaultToRuAsync"/>) -- both so
/// <c>GetLanguagesEndpointTests</c>' exact `["ru","kk","en"]`/`ru`-is-default
/// assertions stay green regardless of run order inside the shared
/// collection (the tests do not reset the database, `MustaskApiCollection`).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SetDefaultLanguageEndpointTests
{
    private readonly MustaskApiFactory _factory;

    public SetDefaultLanguageEndpointTests(MustaskApiFactory factory)
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

    /// <summary>Mirrors `CreateLanguageEndpointTests.UniqueLanguageCode` -- never collides with the seeded codes.</summary>
    private static string UniqueLanguageCode() => $"zz-{Guid.NewGuid():N}"[..7];

    private static HttpRequestMessage PutDefaultRequest(string? accessToken, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/dictionaries/languages/default")
        {
            Content = JsonContent.Create(body),
        };

        if (accessToken is not null)
        {
            request.WithBearer(accessToken);
        }

        return request;
    }

    private async Task CreateInactiveLanguageDirectlyAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        db.Languages.Add(Language.Create(Guid.CreateVersion7(), code, endonym: null, sortOrder: 900, DateTimeOffset.UtcNow, "test"));
        await db.SaveChangesAsync();
    }

    /// <summary>A freshly created language, activated with an endonym -- eligible to become the default.</summary>
    private async Task CreateActiveLanguageDirectlyAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        var now = DateTimeOffset.UtcNow;

        var language = Language.Create(Guid.CreateVersion7(), code, endonym: null, sortOrder: 900, now, "test");
        language.Replace("Test Language", 900, isActive: true, now, "test");
        db.Languages.Add(language);
        await db.SaveChangesAsync();
    }

    /// <summary>Cleanup for the two helpers above -- languages cannot be deleted through the API, only directly, here, by a test that created its own.</summary>
    private async Task DeleteLanguageDirectlyAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        var language = await db.Languages.SingleOrDefaultAsync(l => l.Code == code);
        if (language is not null)
        {
            db.Languages.Remove(language);
            await db.SaveChangesAsync();
        }
    }

    private async Task<bool> IsDefaultDirectlyAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        var language = await db.Languages.SingleAsync(l => l.Code == code);

        return language.IsDefault;
    }

    /// <summary>Restores `ru` as the default through the endpoint itself -- `ru` is always active (seed), so this call never itself fails.</summary>
    private async Task RestoreDefaultToRuAsync(HttpClient client, string token)
    {
        var response = await client.SendAsync(PutDefaultRequest(token, new { code = "ru" }));
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Put_NoToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().SendAsync(
            PutDefaultRequest(null, new { code = "ru" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Put_AuthenticatedWithoutPermission_ReturnsForbidden()
    {
        var (client, token) = await LoginAsync("default-language-noperm");

        var response = await client.SendAsync(PutDefaultRequest(token, new { code = "ru" }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Put_UnknownCode_ReturnsNotFound()
    {
        var (client, token) = await LoginAsync("default-language-unknown", PermissionCatalog.DictionariesLanguagesWrite);

        var response = await client.SendAsync(PutDefaultRequest(token, new { code = "not-a-real-language" }));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("dictionaries.languages.not_found", body.GetProperty("errorCode").GetString());
    }

    /// <summary>A body with no `code` at all resolves the same way an unknown one does -- there is no separate validation error for it.</summary>
    [Fact]
    public async Task Put_MissingCodeInBody_ReturnsNotFound()
    {
        var (client, token) = await LoginAsync("default-language-nocode", PermissionCatalog.DictionariesLanguagesWrite);

        var response = await client.SendAsync(PutDefaultRequest(token, new { }));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("dictionaries.languages.not_found", body.GetProperty("errorCode").GetString());
    }

    /// <summary>Assigning the language that is already the default is a no-op returning 200.</summary>
    [Fact]
    public async Task Put_AlreadyDefault_ReturnsOk_NoOp()
    {
        var (client, token) = await LoginAsync("default-language-noop", PermissionCatalog.DictionariesLanguagesWrite);

        var response = await client.SendAsync(PutDefaultRequest(token, new { code = "ru" }));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ru", body.GetProperty("code").GetString());
        Assert.True(body.GetProperty("isDefault").GetBoolean());
        Assert.True(await IsDefaultDirectlyAsync("ru"));
    }

    /// <summary>Only an active language can become the default.</summary>
    [Fact]
    public async Task Put_TargetInactive_ReturnsConflict_DefaultMustBeActive()
    {
        var code = UniqueLanguageCode();
        await CreateInactiveLanguageDirectlyAsync(code);
        var (client, token) = await LoginAsync("default-language-inactive", PermissionCatalog.DictionariesLanguagesWrite);

        try
        {
            var response = await client.SendAsync(PutDefaultRequest(token, new { code }));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("dictionaries.languages.default_must_be_active", body.GetProperty("errorCode").GetString());
            Assert.Equal(code, body.GetProperty("params").GetProperty("code").GetProperty("value").GetString());
            Assert.True(await IsDefaultDirectlyAsync("ru"));
        }
        finally
        {
            await DeleteLanguageDirectlyAsync(code);
        }
    }

    /// <summary>
    /// A freshly created, active language becomes the default and the
    /// previous default loses the flag in the same call -- nothing else is
    /// required of the new language.
    /// </summary>
    [Fact]
    public async Task Put_NewActiveLanguage_ReturnsOk_MakesNewDefaultAndClearsOld()
    {
        var code = UniqueLanguageCode();
        await CreateActiveLanguageDirectlyAsync(code);
        var (client, token) = await LoginAsync("default-language-new", PermissionCatalog.DictionariesLanguagesWrite);

        try
        {
            var response = await client.SendAsync(PutDefaultRequest(token, new { code }));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(code, body.GetProperty("code").GetString());
            Assert.True(body.GetProperty("isDefault").GetBoolean());
            Assert.True(await IsDefaultDirectlyAsync(code));
            Assert.False(await IsDefaultDirectlyAsync("ru"));
        }
        finally
        {
            await RestoreDefaultToRuAsync(client, token);
            await DeleteLanguageDirectlyAsync(code);
        }
    }

    /// <summary>
    /// Switching to the seeded `kk` succeeds and `ru` loses the flag in the
    /// same call.
    /// </summary>
    [Fact]
    public async Task Put_SeededActiveLanguage_ReturnsOk_MakesNewDefaultAndClearsOld()
    {
        var (client, token) = await LoginAsync("default-language-switch", PermissionCatalog.DictionariesLanguagesWrite);

        try
        {
            var response = await client.SendAsync(PutDefaultRequest(token, new { code = "kk" }));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("kk", body.GetProperty("code").GetString());
            Assert.True(body.GetProperty("isDefault").GetBoolean());
            Assert.True(body.GetProperty("isActive").GetBoolean());
            Assert.True(await IsDefaultDirectlyAsync("kk"));
            Assert.False(await IsDefaultDirectlyAsync("ru"));
        }
        finally
        {
            await RestoreDefaultToRuAsync(client, token);
        }
    }
}
