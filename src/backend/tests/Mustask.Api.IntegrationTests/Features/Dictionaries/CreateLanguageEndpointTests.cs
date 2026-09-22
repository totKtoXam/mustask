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
/// "Добавление языка", "код языка уникален", "формат кода языка". Every test
/// mints its own language code (<see cref="UniqueLanguageCode"/>) so it never
/// collides with the seeded `ru`/`kk`/`en` or with languages other test
/// classes create. The languages
/// table is shared with the whole collection (`MustaskApiCollection`), so every
/// test that actually creates a row deletes it again through
/// <see cref="MustaskDbContext"/> in a `finally` block (mirrors
/// `SetDefaultLanguageEndpointTests.DeleteLanguageDirectlyAsync`) — otherwise
/// `GetLanguagesEndpointTests` and later runs of this file would keep
/// accumulating rows.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class CreateLanguageEndpointTests
{
    private readonly MustaskApiFactory _factory;

    public CreateLanguageEndpointTests(MustaskApiFactory factory)
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
    /// A fresh 2-letter primary subtag plus a 4-hex-digit secondary subtag —
    /// well inside BCP-47 (2–3 letters, optional `-` groups) and the 8-char
    /// ceiling, with enough entropy that two calls never collide within one
    /// run of this suite.
    /// </summary>
    private static string UniqueLanguageCode() => $"zz-{Guid.NewGuid():N}"[..7];

    /// <summary>Deletes a language row this file created, so later tests/files see a clean table (mirrors `SetDefaultLanguageEndpointTests`).</summary>
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

    private static HttpRequestMessage PostRequest(string? accessToken, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/dictionaries/languages")
        {
            Content = JsonContent.Create(body),
        };

        if (accessToken is not null)
        {
            request.WithBearer(accessToken);
        }

        return request;
    }

    [Fact]
    public async Task Post_NoToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().SendAsync(
            PostRequest(null, new { code = UniqueLanguageCode() }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_AuthenticatedWithoutPermission_ReturnsForbidden()
    {
        var (client, token) = await LoginAsync("create-language-noperm");

        var response = await client.SendAsync(PostRequest(token, new { code = UniqueLanguageCode() }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>"Добавление языка": появляется в списке неактивным, пока не заполнен эндоним.</summary>
    [Fact]
    public async Task Post_ValidCode_ReturnsCreated_InactiveWithLocationHeader()
    {
        var code = UniqueLanguageCode();
        var (client, token) = await LoginAsync("create-language-ok", PermissionCatalog.DictionariesLanguagesWrite);

        try
        {
            var response = await client.SendAsync(PostRequest(token, new { code, endonym = "Test", sortOrder = 40 }));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal($"/api/dictionaries/languages/{code}", response.Headers.Location!.OriginalString);
            Assert.Equal(code, body.GetProperty("code").GetString());
            Assert.Equal("Test", body.GetProperty("endonym").GetString());
            Assert.Equal(40, body.GetProperty("sortOrder").GetInt32());
            Assert.False(body.GetProperty("isActive").GetBoolean());
            Assert.False(body.GetProperty("isDefault").GetBoolean());
            Assert.False(string.IsNullOrEmpty(body.GetProperty("version").GetString()));
        }
        finally
        {
            await DeleteLanguageDirectlyAsync(code);
        }
    }

    /// <summary>`code` is normalized -- stored lower-cased.</summary>
    [Fact]
    public async Task Post_MixedCaseCode_IsStoredLowerCased()
    {
        var code = UniqueLanguageCode();
        var (client, token) = await LoginAsync("create-language-case", PermissionCatalog.DictionariesLanguagesWrite);

        try
        {
            var response = await client.SendAsync(PostRequest(token, new { code = code.ToUpperInvariant() }));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal(code, body.GetProperty("code").GetString());
        }
        finally
        {
            await DeleteLanguageDirectlyAsync(code);
        }
    }

    /// <summary>`sortOrder` is optional, default `0`.</summary>
    [Fact]
    public async Task Post_SortOrderOmitted_DefaultsToZero()
    {
        var code = UniqueLanguageCode();
        var (client, token) = await LoginAsync("create-language-defaultsort", PermissionCatalog.DictionariesLanguagesWrite);

        try
        {
            var response = await client.SendAsync(PostRequest(token, new { code }));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal(0, body.GetProperty("sortOrder").GetInt32());
            Assert.Equal(string.Empty, body.GetProperty("endonym").GetString());
        }
        finally
        {
            await DeleteLanguageDirectlyAsync(code);
        }
    }

    /// <summary>"Код языка уникален": повторное добавление того же кода отклоняется.</summary>
    [Fact]
    public async Task Post_DuplicateCode_ReturnsConflict()
    {
        var code = UniqueLanguageCode();
        var (client, token) = await LoginAsync("create-language-dup", PermissionCatalog.DictionariesLanguagesWrite);

        try
        {
            await client.SendAsync(PostRequest(token, new { code }));

            var response = await client.SendAsync(PostRequest(token, new { code }));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("dictionaries.languages.already_exists", body.GetProperty("errorCode").GetString());
            Assert.Equal(code, body.GetProperty("params").GetProperty("code").GetProperty("value").GetString());
        }
        finally
        {
            await DeleteLanguageDirectlyAsync(code);
        }
    }

    /// <summary>Same as above, reached through the case-insensitive normalization of the code.</summary>
    [Fact]
    public async Task Post_DuplicateCode_DifferentCase_ReturnsConflict()
    {
        var code = UniqueLanguageCode();
        var (client, token) = await LoginAsync("create-language-dup-case", PermissionCatalog.DictionariesLanguagesWrite);

        try
        {
            await client.SendAsync(PostRequest(token, new { code }));

            var response = await client.SendAsync(PostRequest(token, new { code = code.ToUpperInvariant() }));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("dictionaries.languages.already_exists", body.GetProperty("errorCode").GetString());
        }
        finally
        {
            await DeleteLanguageDirectlyAsync(code);
        }
    }

    /// <summary>"Формат кода языка": длина вне 2–8 символов или не BCP-47.</summary>
    [Theory]
    [InlineData("r")]
    [InlineData("ru_RU!")]
    [InlineData("toolongcode123")]
    public async Task Post_InvalidCodeFormat_ReturnsBadRequest(string invalidCode)
    {
        var (client, token) = await LoginAsync("create-language-badcode", PermissionCatalog.DictionariesLanguagesWrite);

        var response = await client.SendAsync(PostRequest(token, new { code = invalidCode }));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("dictionaries.validation_failed", body.GetProperty("errorCode").GetString());
        Assert.Equal(
            "dictionaries.languages.invalid_code",
            body.GetProperty("errors").GetProperty("code")[0].GetProperty("code").GetString());
    }

    /// <summary>`endonym` ≤ 200.</summary>
    [Fact]
    public async Task Post_EndonymOverLengthLimit_ReturnsBadRequest_ValueTooLong()
    {
        var code = UniqueLanguageCode();
        var tooLongEndonym = new string('a', DictionaryLimits.EndonymMaxLength + 1);
        var (client, token) = await LoginAsync("create-language-toolong", PermissionCatalog.DictionariesLanguagesWrite);

        var response = await client.SendAsync(PostRequest(token, new { code, endonym = tooLongEndonym }));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var reason = body.GetProperty("errors").GetProperty("endonym")[0];
        Assert.Equal("dictionaries.value_too_long", reason.GetProperty("code").GetString());
        Assert.Equal(DictionaryLimits.EndonymMaxLength, reason.GetProperty("params").GetProperty("limit").GetProperty("value").GetInt32());
    }

    /// <summary>"Добавление языка": появляется в общем списке (`GET /languages`) неактивным сразу после создания.</summary>
    [Fact]
    public async Task Post_ValidCode_AppearsInLanguageList()
    {
        var code = UniqueLanguageCode();
        var (client, token) = await LoginAsync(
            "create-language-list", PermissionCatalog.DictionariesLanguagesWrite, PermissionCatalog.DictionariesLanguagesRead);

        try
        {
            await client.SendAsync(PostRequest(token, new { code }));

            // pageSize=100 -- other tests in this class also create languages
            // with the default sortOrder 0; the default page size (10) is not
            // enough to guarantee this one is still on the first page.
            var listResponse = await client.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, "/api/dictionaries/languages?pageSize=100").WithBearer(token));
            var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>();

            var created = listBody.GetProperty("data").EnumerateArray().Single(x => x.GetProperty("code").GetString() == code);
            Assert.False(created.GetProperty("isActive").GetBoolean());
        }
        finally
        {
            await DeleteLanguageDirectlyAsync(code);
        }
    }
}
