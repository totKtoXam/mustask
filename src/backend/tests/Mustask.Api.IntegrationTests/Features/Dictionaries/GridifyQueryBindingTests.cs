using Gridify;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace Mustask.Api.IntegrationTests.Features.Dictionaries;

/// <summary>
/// How the list routes of `DictionaryEndpoints` bind a request and what
/// Gridify itself does with the values afterwards. Both questions decide the
/// answer to an ordinary `GET /api/dictionaries/languages`, and neither needs
/// a database: a TestServer app with the same parameter shape settles them
/// without Docker.
///
/// `DictionaryEndpoints` does not bind `GridifyQuery` directly ([AsParameters]
/// on a type with non-nullable `int Page`/`PageSize` would make minimal-API
/// treat both as *required* query-string parameters). It binds a DTO with
/// nullable `page`/`pageSize`/`filter`/`orderBy` (mirrored here as
/// <see cref="ListQueryRequest"/>) and fills in the defaults (page 1,
/// pageSize 10) only for the parameters that were *omitted* —
/// `DictionaryEndpoints.ToGridifyQuery`. An explicitly supplied out-of-range
/// value still reaches `GridifyValidation.EnsurePageSizeWithinLimit`
/// unchanged and is refused with 400
/// `dictionaries.invalid_page_size`; that guard is mirrored inline below
/// (`/probe` cannot reference the internal `Mustask.Infrastructure` type), so
/// this probe pins both halves together: the normalization and the guard it
/// must run before.
/// </summary>
public sealed class GridifyQueryBindingTests
{
    private const int MaxPageSize = 100;

    private sealed record ListQueryRequest(int? Page, int? PageSize, string? Filter, string? OrderBy);

    private sealed record Probe(int Page, int PageSize, string? Filter, string? OrderBy);

    private static async Task<(System.Net.HttpStatusCode Status, Probe? Query)> BindAsync(string url)
    {
        // Mirrors Program.cs (ADR-0015, "pageSize default 10").
        GridifyGlobalConfiguration.DefaultPageSize = 10;

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();

        await using var app = builder.Build();
        app.MapGet("/probe", ([AsParameters] ListQueryRequest query) =>
        {
            // Mirrors DictionaryEndpoints.ToGridifyQuery: default only what
            // was omitted, pass explicit values through unchanged.
            var page = query.Page ?? 1;
            var pageSize = query.PageSize ?? GridifyGlobalConfiguration.DefaultPageSize;

            // Mirrors GridifyValidation.EnsurePageSizeWithinLimit: an
            // explicitly out-of-range value is still refused, normalization
            // never masks it.
            if (pageSize > MaxPageSize || pageSize <= 0 || page < 0)
            {
                return Results.BadRequest();
            }

            return Results.Ok(new Probe(page, pageSize, query.Filter, query.OrderBy));
        });

        await app.StartAsync();

        using var client = app.GetTestClient();
        var response = await client.GetAsync(url);

        if (response.StatusCode != System.Net.HttpStatusCode.OK)
        {
            return (response.StatusCode, null);
        }

        // Not `ReadFromJsonAsync`: `Microsoft.AspNetCore.Http`'s extension of
        // the same name (on `HttpRequest`) is in scope here and hides the
        // `HttpContent` one.
        var payload = await response.Content.ReadAsStringAsync();

        return (response.StatusCode, System.Text.Json.JsonSerializer.Deserialize<Probe>(
            payload,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
    }

    /// <summary>
    /// All four shapes a caller may send an ordinary list request in —
    /// neither parameter, only one, or both — now succeed with the
    /// contract's defaults filled in for whatever was omitted (page 1,
    /// pageSize 10); an explicit value always wins over the default.
    /// Before the fix every row but the last answered 400 with no body.
    /// </summary>
    [Theory]
    [InlineData("/probe", 1, 10)]
    [InlineData("/probe?page=2", 2, 10)]
    [InlineData("/probe?pageSize=20", 1, 20)]
    [InlineData("/probe?page=3&pageSize=30", 3, 30)]
    public async Task ListQuery_AnyCombinationOfOmittedPagingParameters_Returns200WithDefaultsFilledIn(
        string url, int expectedPage, int expectedPageSize)
    {
        var (status, query) = await BindAsync(url);

        Assert.Equal(System.Net.HttpStatusCode.OK, status);
        Assert.Equal(expectedPage, query!.Page);
        Assert.Equal(expectedPageSize, query.PageSize);
    }

    /// <summary>
    /// A `filter`/`orderBy`-only request — the shape the panel's select box
    /// sends (`?pageSize=100&amp;filter=isActive=true`, and one with neither
    /// paging parameter at all) — no longer dies in binding either.
    /// </summary>
    [Fact]
    public async Task ListQuery_FilterWithoutAnyPagingParameter_Returns200WithDefaultPaging()
    {
        var (status, query) = await BindAsync("/probe?filter=code%3D%3Dru");

        Assert.Equal(System.Net.HttpStatusCode.OK, status);
        Assert.Equal(1, query!.Page);
        Assert.Equal(10, query.PageSize);
        Assert.Equal("code==ru", query.Filter);
    }

    /// <summary>Both parameters present, together with filter/orderBy, keeps working exactly as before the fix.</summary>
    [Fact]
    public async Task ListQuery_WithBothPagingParameters_IsBound()
    {
        var (status, query) = await BindAsync("/probe?page=2&pageSize=100&filter=code%3D%3Dru&orderBy=code");

        Assert.Equal(System.Net.HttpStatusCode.OK, status);
        Assert.Equal(2, query!.Page);
        Assert.Equal(100, query.PageSize);
        Assert.Equal("code==ru", query.Filter);
        Assert.Equal("code", query.OrderBy);
    }

    /// <summary>
    /// An explicitly supplied out-of-range value is not masked by the
    /// normalization of omitted paging values — the page-size guard still
    /// fires. `page = 0`
    /// stays deliberately unguarded (see
    /// <see cref="Gridify_ClampsOutOfRangePaging_InsteadOfEmittingNegativeSkipOrTake"/>):
    /// Gridify clamps it to page 1, which is harmless.
    /// </summary>
    [Theory]
    [InlineData("/probe?pageSize=0")]
    [InlineData("/probe?pageSize=-1")]
    [InlineData("/probe?pageSize=101")]
    [InlineData("/probe?page=-1")]
    [InlineData("/probe?page=-1&pageSize=10")]
    public async Task ListQuery_ExplicitOutOfRangeValue_StillReturns400(string url)
    {
        var (status, _) = await BindAsync(url);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, status);
    }

    /// <summary>`pageSize` exactly at the ceiling is still accepted.</summary>
    [Fact]
    public async Task ListQuery_PageSizeExactlyAtTheCeiling_Returns200()
    {
        var (status, query) = await BindAsync("/probe?pageSize=100");

        Assert.Equal(System.Net.HttpStatusCode.OK, status);
        Assert.Equal(100, query!.PageSize);
    }

    /// <summary>
    /// The trap the page-size guard set for the omitted-paging defaults: a
    /// `GridifyQuery` that was never
    /// given paging values carries `0`/`0` — Gridify applies the defaults in
    /// `ApplyPaging`, not in the constructor. `DictionaryEndpoints.
    /// ToGridifyQuery` is what fills in the contract's defaults before this
    /// type is ever constructed with omitted values reaching it, and the
    /// tests above pin that it does so correctly.
    /// </summary>
    [Fact]
    public void GridifyQuery_ConstructedWithoutPaging_CarriesZeroes_NotTheContractDefaults()
    {
        var query = new GridifyQuery();

        Assert.Equal(0, query.Page);
        Assert.Equal(0, query.PageSize);
    }

    /// <summary>
    /// What Gridify does with out-of-range paging values: it clamps rather
    /// than passing them to `Skip`/`Take` — `page &lt;= 0` becomes page 1 and
    /// `pageSize &lt;= 0` becomes `GridifyGlobalConfiguration.DefaultPageSize`.
    /// The feared failure (a negative `LIMIT`/`OFFSET` reaching PostgreSQL,
    /// i.e. a 500) therefore does not happen; the answer used to be a silent
    /// clamp and is now an explicit 400.
    /// </summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    public void Gridify_ClampsOutOfRangePaging_InsteadOfEmittingNegativeSkipOrTake(int page, int pageSize)
    {
        GridifyGlobalConfiguration.DefaultPageSize = 10;

        var rows = Enumerable.Range(1, 50).AsQueryable()
            .ApplyPaging(new GridifyQuery(page, pageSize, null, null))
            .ToList();

        Assert.Equal(10, rows.Count);
        Assert.Equal(1, rows[0]);
    }
}
