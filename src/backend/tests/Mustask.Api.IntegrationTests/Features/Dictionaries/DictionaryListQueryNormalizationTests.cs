using System.Reflection;
using Gridify;
using Mustask.Api.Features.Dictionaries;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace Mustask.Api.IntegrationTests.Features.Dictionaries;

/// <summary>
/// `GridifyQueryBindingTests`
/// proves that a DTO with nullable `page`/`pageSize` binds the way the
/// contract needs, but it does so on a <i>copy</i> of the production shape
/// inside its own probe app: nothing there would notice if
/// `DictionaryEndpoints` drifted back to `[AsParameters] GridifyQuery` or
/// changed its defaults. These tests hold the production code itself —
/// `DictionaryEndpoints.ListQueryRequest` / `.ToGridifyQuery` and the list
/// route handlers' signatures — against the list contract ("`page`
/// (1-based), `pageSize` (default 10, ceiling 100)"), without Docker: every
/// endpoint test of this feature is `RequiresDocker`.
/// </summary>
public sealed class DictionaryListQueryNormalizationTests
{
    private static readonly Type ListQueryRequestType =
        typeof(DictionaryEndpoints).GetNestedType("ListQueryRequest", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "DictionaryEndpoints no longer has a nested ListQueryRequest — the optional-paging binding has been reshaped, re-read this test.");

    private static readonly MethodInfo ToGridifyQueryMethod =
        typeof(DictionaryEndpoints).GetMethod("ToGridifyQuery", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "DictionaryEndpoints.ToGridifyQuery is gone — the optional-paging binding has been reshaped, re-read this test.");

    private static GridifyQuery Normalize(int? page, int? pageSize, string? filter = null, string? orderBy = null)
    {
        // Program.cs sets this global at startup; the production
        // ToGridifyQuery reads it for an omitted pageSize, so the test has to
        // stand it up the same way (pinned below by
        // Program_SetsTheGridifyDefaultPageSizeToTheContractsTen).
        GridifyGlobalConfiguration.DefaultPageSize = 10;

        var request = Activator.CreateInstance(
            ListQueryRequestType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [page, pageSize, filter, orderBy],
            culture: null);

        return (GridifyQuery)ToGridifyQueryMethod.Invoke(null, [request])!;
    }

    /// <summary>
    /// The ordinary list read — no query string at all — is page 1 of 10,
    /// not the `0`/`0` a bare `GridifyQuery` carries and not a 400. This is
    /// the production half of the omitted-paging fix; the HTTP half stays
    /// Docker-gated.
    /// </summary>
    [Fact]
    public void ToGridifyQuery_BothPagingParametersOmitted_FillsTheContractDefaults()
    {
        var query = Normalize(page: null, pageSize: null);

        Assert.Equal(1, query.Page);
        Assert.Equal(10, query.PageSize);
    }

    /// <summary>One parameter omitted defaults, the other is honoured — the shapes the panel sends.</summary>
    [Theory]
    [InlineData(null, 100, 1, 100)]   // the panel's select box: ?pageSize=100&filter=isActive=true
    [InlineData(2, null, 2, 10)]      // paging through a list without naming a size
    [InlineData(3, 25, 3, 25)]        // both given: nothing is defaulted
    public void ToGridifyQuery_OneParameterGiven_DefaultsOnlyTheOmittedOne(
        int? page, int? pageSize, int expectedPage, int expectedPageSize)
    {
        var query = Normalize(page, pageSize);

        Assert.Equal(expectedPage, query.Page);
        Assert.Equal(expectedPageSize, query.PageSize);
    }

    /// <summary>
    /// The trap the page-size guard sets for the omitted-paging defaults:
    /// normalization must not turn an
    /// explicitly out-of-range value into a valid one, or
    /// `GridifyValidation.EnsurePageSizeWithinLimit` would never see it and
    /// `?pageSize=0` / `?pageSize=101` / `?page=-1` would silently succeed
    /// instead of answering 400 `dictionaries.invalid_page_size`.
    /// </summary>
    [Theory]
    [InlineData(null, 0, 1, 0)]
    [InlineData(null, -5, 1, -5)]
    [InlineData(null, 101, 1, 101)]
    [InlineData(-1, null, -1, 10)]
    [InlineData(0, null, 0, 10)]      // page = 0 stays unguarded on purpose; Gridify clamps it to page 1
    public void ToGridifyQuery_ExplicitOutOfRangeValue_ReachesTheGuardUnchanged(
        int? page, int? pageSize, int expectedPage, int expectedPageSize)
    {
        var query = Normalize(page, pageSize);

        Assert.Equal(expectedPage, query.Page);
        Assert.Equal(expectedPageSize, query.PageSize);
    }

    /// <summary>`filter`/`orderBy` survive the normalization untouched (whitelist checks run on them afterwards).</summary>
    [Fact]
    public void ToGridifyQuery_FilterAndOrderBy_ArePassedThroughUnchanged()
    {
        var query = Normalize(page: null, pageSize: null, filter: "isActive=true", orderBy: "name");

        Assert.Equal("isActive=true", query.Filter);
        Assert.Equal("name", query.OrderBy);
        Assert.Equal(1, query.Page);
        Assert.Equal(10, query.PageSize);
    }

    /// <summary>
    /// Every list route must bind the optional DTO, never `GridifyQuery`
    /// directly, and its two paging properties must stay nullable — a
    /// non-nullable `int` there makes minimal-API treat the parameters as
    /// required and answer a bare 400 to an ordinary
    /// `GET /api/dictionaries/languages`.
    /// </summary>
    [Theory]
    [InlineData("GetLanguagesAsync")]
    public void ListRoutes_BindTheOptionalListQueryDto_NotGridifyQuery(string handlerName)
    {
        var handler = typeof(DictionaryEndpoints)
            .GetMethod(handlerName, BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(handler);

        var queryParameter = handler!.GetParameters()[0];
        Assert.Same(ListQueryRequestType, queryParameter.ParameterType);
        Assert.NotSame(typeof(GridifyQuery), queryParameter.ParameterType);

        foreach (var property in new[] { "Page", "PageSize" })
        {
            var propertyType = ListQueryRequestType.GetProperty(property)!.PropertyType;
            Assert.Equal(typeof(int?), propertyType);
        }
    }

    /// <summary>
    /// The list contract fixes the default page size at 10, and `ToGridifyQuery` takes it
    /// from `GridifyGlobalConfiguration` — a global set once, in `Program.cs`.
    /// Gridify's own default is 20, so nothing but this line makes the
    /// contract's number true. A source check is what runs without Docker; the value
    /// lives in one statement.
    /// </summary>
    [Fact]
    public void Program_SetsTheGridifyDefaultPageSizeToTheContractsTen()
    {
        var srcDirectory = FindDirectoryUpFromHere("src");
        var programPath = Path.Combine(srcDirectory, "backend", "Mustask.Api", "Program.cs");

        Assert.True(File.Exists(programPath), $"Program.cs not found at '{programPath}'.");

        var source = File.ReadAllText(programPath);

        Assert.Contains("GridifyGlobalConfiguration.DefaultPageSize = 10;", source, StringComparison.Ordinal);
    }

    private sealed record ProbeListQuery(int? Page, int? PageSize, string? Filter, string? OrderBy);

    /// <summary>
    /// A question about the platform rather than about our code, so a mirrored
    /// shape is the right instrument here: `page`/`pageSize` are `int?` now,
    /// and what happens to a value that is *present but unusable* — empty
    /// (`?pageSize=`, exactly what a form-driven client emits from a cleared
    /// field) or garbage (`?pageSize=abc`)? Measured: **all of them are 400**;
    /// an empty value is not treated as an omitted one. Nothing was lost by
    /// the omitted-paging fix (a non-nullable `int` answered 400 to the same
    /// requests), and the contract defines neither case — but the panel must
    /// *omit* a
    /// paging parameter it does not want, not send it empty. In the real app
    /// this 400 is given a body by `UseStatusCodePages()` +
    /// `CustomizeProblemDetails` (`errorCode` = `http.bad_request`), not by
    /// `dictionaries.invalid_page_size`; the probe below has no error pipeline,
    /// so it only pins the status.
    /// </summary>
    [Theory]
    [InlineData("/probe?page=&pageSize=")]
    [InlineData("/probe?pageSize=")]
    [InlineData("/probe?pageSize=abc")]
    [InlineData("/probe?page=1.5")]
    public async Task ListQueryShape_PresentButUnusableValue_Is400_NotTheDefault(string url)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();

        await using var app = builder.Build();
        app.MapGet("/probe", ([AsParameters] ProbeListQuery query) =>
            Results.Ok(new { Page = query.Page ?? 1, PageSize = query.PageSize ?? 10 }));

        await app.StartAsync();

        using var client = app.GetTestClient();
        var response = await client.GetAsync(url);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string FindDirectoryUpFromHere(string relativeDirectoryName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativeDirectoryName);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not find '{relativeDirectoryName}' by walking up from '{AppContext.BaseDirectory}'.");
    }
}
