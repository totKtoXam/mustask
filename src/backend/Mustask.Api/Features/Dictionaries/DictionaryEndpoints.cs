using Gridify;
using Mustask.Api.Authorization;
using Mustask.Application.Abstractions.Dictionaries;
using Mustask.Application.Features.Dictionaries.CreateLanguage;
using Mustask.Application.Features.Dictionaries.GetLanguages;
using Mustask.Application.Features.Dictionaries.SetDefaultLanguage;
using Mustask.Application.Features.Dictionaries.UpdateLanguage;
using Mustask.Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Mustask.Api.Features.Dictionaries;

/// <summary>
/// `/api/dictionaries/*` — the languages dictionary routes, each wired to its
/// own use-case handler (one vertical slice per route).
/// </summary>
public static class DictionaryEndpoints
{
    public static IEndpointRouteBuilder MapDictionaryEndpoints(this IEndpointRouteBuilder app)
    {
        var languages = app.MapGroup("/api/dictionaries/languages");

        languages.MapGet("/", GetLanguagesAsync)
            .RequiresPermission(PermissionCatalog.DictionariesLanguagesRead);

        languages.MapPost("/", CreateLanguageAsync)
            .RequiresPermission(PermissionCatalog.DictionariesLanguagesWrite);

        languages.MapPut("/default", SetDefaultLanguageAsync)
            .RequiresPermission(PermissionCatalog.DictionariesLanguagesWrite);

        // "/default" is mapped above /{code} so it is not shadowed by it.
        languages.MapPut("/{code}", UpdateLanguageAsync)
            .RequiresPermission(PermissionCatalog.DictionariesLanguagesWrite);

        return app;
    }

    // --- List query binding ------------------------------------------------------

    // `GridifyQuery.Page`/`PageSize` are non-nullable `int`s, so binding it
    // directly via [AsParameters] makes minimal-API treat both query-string
    // parameters as required -- an ordinary GET with neither, or with only
    // one of them, would die in binding with a bare 400 before the handler
    // ever ran. This DTO makes all four query-string parameters optional and
    // ToGridifyQuery fills in the defaults (page 1, pageSize 10) only for the
    // ones that were *omitted* -- an explicitly supplied out-of-range value
    // (pageSize <= 0 or > 100, page < 0) is passed through unchanged so
    // GridifyValidation.EnsurePageSizeWithinLimit still turns it into 400
    // dictionaries.invalid_page_size.
    private sealed record ListQueryRequest(int? Page, int? PageSize, string? Filter, string? OrderBy);

    private static GridifyQuery ToGridifyQuery(ListQueryRequest query) => new(
        query.Page ?? 1,
        query.PageSize ?? GridifyGlobalConfiguration.DefaultPageSize,
        query.Filter,
        query.OrderBy);

    // --- Languages ---------------------------------------------------------------

    private static async Task<IResult> GetLanguagesAsync(
        [AsParameters] ListQueryRequest query,
        HttpRequest request,
        HttpResponse response,
        GetLanguagesHandler handler,
        IRequestLanguageResolver languageResolver,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetLanguagesQuery(ToGridifyQuery(query)), cancellationToken);

        // Every response of a read endpoint carries Content-Language
        // unconditionally -- resolved from Accept-Language the same way any
        // localized read would, even though a language has no translation
        // for it to select and the body itself is unaffected by
        // Accept-Language.
        var acceptedTags = RequestLanguageHeader.ParseAcceptedTags(request);
        var language = await languageResolver.ResolveAsync(acceptedTags, cancellationToken);
        RequestLanguageHeader.SetContentLanguage(response, language.Code);

        return Results.Ok(result.Page);
    }

    private static async Task<IResult> CreateLanguageAsync(
        CreateLanguageRequest? request,
        CreateLanguageHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new CreateLanguageCommand(request?.Code, request?.Endonym, request?.SortOrder);

        var result = await handler.Handle(command, cancellationToken);

        return Results.Created($"/api/dictionaries/languages/{result.Response.Code}", result.Response);
    }

    private static async Task<IResult> UpdateLanguageAsync(
        string code,
        UpdateLanguageRequest? request,
        HttpRequest httpRequest,
        UpdateLanguageHandler handler,
        CancellationToken cancellationToken)
    {
        var expectedVersion = IfMatchHeader.Parse(httpRequest);

        // SortOrder/IsActive are passed through as-is (no ?? default) so an
        // absent field surfaces as dictionaries.value_required instead of
        // silently deactivating the language / resetting its order.
        var command = new UpdateLanguageCommand(
            code, request?.Endonym, request?.SortOrder, request?.IsActive, expectedVersion);

        var result = await handler.Handle(command, cancellationToken);

        return Results.Ok(result.Response);
    }

    private static async Task<IResult> SetDefaultLanguageAsync(
        SetDefaultLanguageRequest? request,
        SetDefaultLanguageHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new SetDefaultLanguageCommand(request?.Code);

        var result = await handler.Handle(command, cancellationToken);

        return Results.Ok(result.Response);
    }

    private sealed record CreateLanguageRequest(string? Code, string? Endonym, int? SortOrder);

    private sealed record UpdateLanguageRequest(string? Endonym, int? SortOrder, bool? IsActive);

    private sealed record SetDefaultLanguageRequest(string? Code);
}
