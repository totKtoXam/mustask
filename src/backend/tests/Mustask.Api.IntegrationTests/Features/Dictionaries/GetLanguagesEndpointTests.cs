using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Dictionaries;

/// <summary>
/// "Список языков", "состав на старте": a straight Gridify paging read —
/// no `Accept-Language` resolution. Exercised against the three languages
/// the host seeds at startup (`contracts/dictionaries/languages.json`) —
/// never mutated by this file. The languages table is shared with the whole
/// collection (`MustaskApiCollection`), and it is not always exactly the
/// three seeded rows: other test classes create (and clean up) their own
/// languages and may be mid-flight. Assertions here therefore check that the three seeded rows
/// are present and in their seeded relative order (`code=ru|code=kk|code=en`
/// — a Gridify `Or` filter narrows the read to exactly that fixed universe),
/// never an exact total over the whole table.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetLanguagesEndpointTests
{
    private readonly MustaskApiFactory _factory;

    public GetLanguagesEndpointTests(MustaskApiFactory factory)
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

    private static HttpRequestMessage Request(string? accessToken, string query = "")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/dictionaries/languages" + query);
        if (accessToken is not null)
        {
            request.WithBearer(accessToken);
        }

        return request;
    }

    [Fact]
    public async Task Get_NoToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().SendAsync(Request(accessToken: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_AuthenticatedWithoutPermission_ReturnsForbidden()
    {
        var (client, token) = await LoginAsync("languages-noperm");

        var response = await client.SendAsync(Request(token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithReadPermission_ReturnsAllThreeSeededLanguagesOrderedBySortOrder()
    {
        // "Порядок записей в списке": без orderBy — по sortOrder
        // по возрастанию; contracts/dictionaries/languages.json задаёт
        // ru=10, kk=20, en=30 — порядков без совпадений, так что этот тест
        // не покрывает алфавитный tie-break.
        // The `code=ru|code=kk|code=en` filter (Gridify `Or`) pins the read
        // to exactly the seeded universe -- the table itself may carry more
        // rows (the class comment explains why), an exact `count` over the
        // whole table would not be robust.
        var (client, token) = await LoginAsync("languages-list", PermissionCatalog.DictionariesLanguagesRead);

        var response = await client.SendAsync(Request(token, "?filter=code=ru|code=kk|code=en"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, body.GetProperty("count").GetInt32());
        var codes = body.GetProperty("data").EnumerateArray().Select(x => x.GetProperty("code").GetString()).ToList();
        Assert.Equal(["ru", "kk", "en"], codes);
    }

    /// <summary>`Content-Language` is set unconditionally, even though a language has no translation for it to select.</summary>
    [Fact]
    public async Task Get_ReturnsContentLanguageHeader_EvenThoughTheBodyIgnoresAcceptLanguage()
    {
        var (client, token) = await LoginAsync("languages-content-language", PermissionCatalog.DictionariesLanguagesRead);

        var request = Request(token, "?filter=code=ru|code=kk|code=en");
        request.Headers.Add("Accept-Language", "kk");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("kk", response.Content.Headers.ContentLanguage.Single());
    }

    [Fact]
    public async Task Get_WithReadPermission_ReturnsCodeEndonymDefaultAndActiveFlags()
    {
        // "Список языков": код, собственное название (эндоним), признаки
        // «по умолчанию» и активности; "состав на старте" — ru default.
        // Filtered to the seeded universe (class comment) rather than relying
        // on `ru`/`kk` landing on the default (unfiltered) first page.
        var (client, token) = await LoginAsync("languages-fields", PermissionCatalog.DictionariesLanguagesRead);

        var response = await client.SendAsync(Request(token, "?filter=code=ru|code=kk|code=en"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var ru = body.GetProperty("data").EnumerateArray().Single(x => x.GetProperty("code").GetString() == "ru");
        Assert.Equal("Русский", ru.GetProperty("endonym").GetString());
        Assert.True(ru.GetProperty("isDefault").GetBoolean());
        Assert.True(ru.GetProperty("isActive").GetBoolean());

        var kk = body.GetProperty("data").EnumerateArray().Single(x => x.GetProperty("code").GetString() == "kk");
        Assert.False(kk.GetProperty("isDefault").GetBoolean());
    }

    [Fact]
    public async Task Get_FilterIsDefaultTrue_ReturnsOnlyTheDefaultLanguage()
    {
        var (client, token) = await LoginAsync("languages-filter-default", PermissionCatalog.DictionariesLanguagesRead);

        var response = await client.SendAsync(Request(token, "?filter=isDefault=true"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, body.GetProperty("count").GetInt32());
        Assert.Equal("ru", body.GetProperty("data")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Get_PageSizeAboveCeiling_ReturnsBadRequest()
    {
        var (client, token) = await LoginAsync("languages-pagesize", PermissionCatalog.DictionariesLanguagesRead);

        var response = await client.SendAsync(Request(token, "?pageSize=101"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // dictionaries.invalid_page_size
        // is the top-level errorCode, not a field reason under validation_failed.
        Assert.Equal("dictionaries.invalid_page_size", body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Get_UnknownFilterField_ReturnsBadRequestNamingTheField()
    {
        var (client, token) = await LoginAsync("languages-unknown-field", PermissionCatalog.DictionariesLanguagesRead);

        var response = await client.SendAsync(Request(token, "?filter=notAField=1"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // Top-level errorCode, not a field reason.
        Assert.Equal("dictionaries.unknown_query_field", body.GetProperty("errorCode").GetString());
        Assert.Equal("notAField", body.GetProperty("params").GetProperty("field").GetProperty("value").GetString());
    }

    /// <summary>The languages whitelist has no `name` field -- a language is not translated, `endonym` plays that role -- so `orderBy=name` is `unknown_query_field`.</summary>
    [Fact]
    public async Task Get_OrderByName_ReturnsBadRequest_UnknownQueryField()
    {
        var (client, token) = await LoginAsync("languages-orderby-name", PermissionCatalog.DictionariesLanguagesRead);

        var response = await client.SendAsync(Request(token, "?orderBy=name"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("dictionaries.unknown_query_field", body.GetProperty("errorCode").GetString());
        Assert.Equal("name", body.GetProperty("params").GetProperty("field").GetProperty("value").GetString());
    }

    /// <summary>`endonym` is the whitelisted field the wording now names explicitly.</summary>
    [Fact]
    public async Task Get_OrderByEndonym_ReturnsOk()
    {
        var (client, token) = await LoginAsync("languages-orderby-endonym", PermissionCatalog.DictionariesLanguagesRead);

        var response = await client.SendAsync(Request(token, "?orderBy=endonym&filter=code=ru|code=kk|code=en"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Unbalanced parentheses fail Gridify's own parser, not the whitelist check -- a 400 `invalid_query_syntax`, never a 500.</summary>
    [Fact]
    public async Task Get_MalformedFilterSyntax_ReturnsBadRequest_InvalidQuerySyntax()
    {
        var (client, token) = await LoginAsync("languages-malformed-filter", PermissionCatalog.DictionariesLanguagesRead);

        var response = await client.SendAsync(Request(token, "?filter=((("));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("dictionaries.invalid_query_syntax", body.GetProperty("errorCode").GetString());
        Assert.Equal("filter", body.GetProperty("params").GetProperty("field").GetProperty("value").GetString());
    }

    /// <summary>A known field with an operator Gridify does not support parses fine as a member name (passes the whitelist) but fails once `GridifyAsync` builds the expression -- still a 400 `invalid_query_syntax`, not a 500.</summary>
    [Fact]
    public async Task Get_KnownFieldWithUnsupportedOperator_ReturnsBadRequest_InvalidQuerySyntax()
    {
        var (client, token) = await LoginAsync("languages-bad-operator", PermissionCatalog.DictionariesLanguagesRead);

        var response = await client.SendAsync(Request(token, "?filter=code>>1"));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("dictionaries.invalid_query_syntax", body.GetProperty("errorCode").GetString());
        Assert.Equal("filter", body.GetProperty("params").GetProperty("field").GetProperty("value").GetString());
    }
}
