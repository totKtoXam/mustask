using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mustask.Domain.Identity;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Dictionaries;

/// <summary>
/// The dictionary API rules that belong to no single slice and are therefore
/// covered by none of the slice test files — the operations that
/// deliberately do not exist, the `orderBy` half of the list whitelist, the
/// `pageSize` ceiling from below, a page past the end, and the promise that
/// a deactivated entry is still listed.
/// </summary>
/// <remarks>
/// This class shares its database with the whole <see cref="MustaskApiCollection"/>,
/// and other classes create helper languages that outlive their own test
/// run, so every assertion here is relative to the three seeded rows
/// (`ru`, `kk`, `en`), never an exact count of the whole table.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DictionaryContractRulesEndpointTests
{
    private const string DeactivatableCode = "en";
    private const int SeededLanguageCount = 3;

    private readonly MustaskApiFactory _factory;

    public DictionaryContractRulesEndpointTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    private (HttpClient Client, string AccessToken) Login(params string[] permissionCodes)
    {
        // Keycloak-токен: права едут в токене, строк в базе не нужно.
        var client = _factory.CreateClient();
        var accessToken = KeycloakTestRealm.CreateToken(permissionCodes);

        return (client, accessToken);
    }

    private static HttpRequestMessage Get(string accessToken, string path) =>
        new HttpRequestMessage(HttpMethod.Get, path).WithBearer(accessToken);

    private async Task SetActivityDirectlyAsync(bool isActive)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        var language = await db.Languages.SingleAsync(l => l.Code == DeactivatableCode);

        language.Replace(language.Endonym, language.SortOrder, isActive, DateTimeOffset.UtcNow, "test");
        await db.SaveChangesAsync();
    }

    // --- Operations that deliberately do not exist ------------------------------

    /// <summary>Entries are deactivated, never deleted — there is no delete route for an entry.</summary>
    [Fact]
    public async Task Delete_DictionaryEntry_ReturnsMethodNotAllowed()
    {
        var (client, token) = Login(PermissionCatalog.DictionariesLanguagesWrite);

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, "/api/dictionaries/languages/ru").WithBearer(token));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("http.method_not_allowed", body.GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// Authentication and authorization do not run for an unmatched method
    /// (UnmatchedRouteMiddleware), so the answer is the same with and without
    /// a token — an unauthenticated caller gets 405, not 401.
    /// </summary>
    [Fact]
    public async Task Delete_DictionaryEntry_WithoutToken_IsStill405NotUnauthorized()
    {
        var response = await _factory.CreateClient().DeleteAsync("/api/dictionaries/languages/ru");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    // --- The whitelist and the paging boundaries -------------------------------

    /// <summary>An unmapped field in `orderBy` is a 400 with `dictionaries.unknown_query_field` as the top-level `errorCode`, exactly like one in `filter` -- not a field reason under `validation_failed`.</summary>
    [Fact]
    public async Task Get_UnknownOrderByField_ReturnsBadRequestNamingTheField()
    {
        var (client, token) = Login(PermissionCatalog.DictionariesLanguagesRead);

        var response = await client.SendAsync(Get(token, "/api/dictionaries/languages?orderBy=notAField"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("dictionaries.unknown_query_field", body.GetProperty("errorCode").GetString());
        Assert.Equal("notAField", body.GetProperty("params").GetProperty("field").GetProperty("value").GetString());
    }

    /// <summary>The ceiling itself is allowed — 100 is the last accepted value, 101 the first refused one (covered by the slice tests).</summary>
    [Fact]
    public async Task Get_PageSizeExactlyAtTheCeiling_ReturnsOk()
    {
        var (client, token) = Login(PermissionCatalog.DictionariesLanguagesRead);

        var response = await client.SendAsync(Get(token, "/api/dictionaries/languages?pageSize=100"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.GetProperty("count").GetInt32() >= SeededLanguageCount);
    }

    /// <summary>`count` is the total, not the size of the page — a page past the end is empty but still reports it.</summary>
    [Fact]
    public async Task Get_PageBeyondTheLastOne_ReturnsEmptyDataAndTheFullCount()
    {
        var (client, token) = Login(PermissionCatalog.DictionariesLanguagesRead);

        var response = await client.SendAsync(Get(token, "/api/dictionaries/languages?page=9999&pageSize=10"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.GetProperty("count").GetInt32() >= SeededLanguageCount);
        Assert.Empty(body.GetProperty("data").EnumerateArray());
    }

    /// <summary>`pageSize=0` would reach Gridify's `Take` unchecked otherwise -- defined as a 400, same code as the ceiling case, rather than an undefined/500 answer.</summary>
    [Fact]
    public async Task Get_PageSizeZero_ReturnsBadRequest_InvalidPageSize()
    {
        var (client, token) = Login(PermissionCatalog.DictionariesLanguagesRead);

        var response = await client.SendAsync(Get(token, "/api/dictionaries/languages?pageSize=0"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("dictionaries.invalid_page_size", body.GetProperty("errorCode").GetString());
    }

    /// <summary>A negative `page` would reach Gridify's `Skip` unchecked otherwise -- defined as a 400 rather than an undefined/500 answer.</summary>
    [Fact]
    public async Task Get_PageNegative_ReturnsBadRequest_InvalidPageSize()
    {
        var (client, token) = Login(PermissionCatalog.DictionariesLanguagesRead);

        var response = await client.SendAsync(Get(token, "/api/dictionaries/languages?page=-1"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("dictionaries.invalid_page_size", body.GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// An explicit `orderBy` replaces the default ordering (sortOrder, then
    /// endonym): ordering by `code` descending puts the three seeded rows in
    /// reverse alphabetical order relative to each other.
    /// </summary>
    [Fact]
    public async Task Get_OrderByCodeDescending_ReplacesTheDefaultOrder()
    {
        var (client, token) = Login(PermissionCatalog.DictionariesLanguagesRead);

        var response = await client.SendAsync(Get(token, "/api/dictionaries/languages?orderBy=code desc&pageSize=100"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var seededInListOrder = body.GetProperty("data").EnumerateArray()
            .Select(x => x.GetProperty("code").GetString())
            .Where(code => code is "ru" or "kk" or "en")
            .ToList();

        Assert.Equal(["ru", "kk", "en"], seededInListOrder);
    }

    // --- A deactivated entry stays in the list ---------------------------------

    /// <summary>
    /// Deactivated entries are returned — registries and filters need them;
    /// the list keeps the row and the `isActive=true` filter is what removes
    /// it, nothing else.
    /// </summary>
    [Fact]
    public async Task Get_DeactivatedEntry_IsStillListedButExcludedByTheActiveFilter()
    {
        await SetActivityDirectlyAsync(isActive: false);

        try
        {
            var (client, token) = Login(PermissionCatalog.DictionariesLanguagesRead);

            var all = await client.SendAsync(Get(token, "/api/dictionaries/languages?pageSize=100"));
            var allBody = await all.Content.ReadFromJsonAsync<JsonElement>();
            var entry = allBody.GetProperty("data").EnumerateArray()
                .Single(x => x.GetProperty("code").GetString() == DeactivatableCode);

            Assert.False(entry.GetProperty("isActive").GetBoolean());

            var activeOnly = await client.SendAsync(Get(token, "/api/dictionaries/languages?filter=isActive=true&pageSize=100"));
            var activeOnlyBody = await activeOnly.Content.ReadFromJsonAsync<JsonElement>();
            var activeCodes = activeOnlyBody.GetProperty("data").EnumerateArray()
                .Select(x => x.GetProperty("code").GetString()).ToList();

            Assert.DoesNotContain(DeactivatableCode, activeCodes);
            Assert.Contains("ru", activeCodes);
        }
        finally
        {
            await SetActivityDirectlyAsync(isActive: true);
        }
    }

    /// <summary>
    /// `version` is the row's `xmin` and the same value is what `If-Match`
    /// compares against — the list's `version` is usable as a precondition,
    /// not a decorative field.
    /// </summary>
    [Fact]
    public async Task Get_ListVersion_MatchesTheRowsConcurrencyToken()
    {
        var (client, token) = Login(PermissionCatalog.DictionariesLanguagesRead);

        var response = await client.SendAsync(Get(token, "/api/dictionaries/languages?pageSize=100"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var listed = body.GetProperty("data").EnumerateArray()
            .Single(x => x.GetProperty("code").GetString() == "ru")
            .GetProperty("version").GetString();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        var language = await db.Languages.SingleAsync(l => l.Code == "ru");
        var expected = ((uint)db.Entry(language).Property("xmin").CurrentValue!).ToString(CultureInfo.InvariantCulture);

        Assert.Equal(expected, listed);
    }
}
