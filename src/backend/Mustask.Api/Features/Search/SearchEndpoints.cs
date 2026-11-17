using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Mustask.Api.Authorization;
using Mustask.Application.Features.Search.SearchStructure;
using Mustask.Domain.Identity;

namespace Mustask.Api.Features.Search;

/// <summary>The body of the search read: the typed text and the group to expand.</summary>
public sealed record SearchRequest(string? Query, string? Expand);

/// <summary>
/// The quick search's route (feat-071 api-contract). A read sent as POST so the typed text is in the body and never in the request
/// line the API logs (BR-GS-10). Behind the organization gate (any member; a non-member gets the organization's 404) and the
/// search limiter. Nothing is written, no idempotency key, no audit.
/// </summary>
public static class SearchEndpoints
{
    private const string Tag = "Search";

    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/organizations/{organizationId}").WithTags(Tag)
            .MapPost("/search", SearchStructureAsync)
            // Every member holds the organization's read; what is returned is filtered by sight, not by this code.
            .RequiresOrganizationPermission(PermissionCatalog.OrganizationsOrganizationRead)
            .AddEndpointFilter<SearchReadLimitFilter>()
            .Produces<SearchStructureResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static async Task<IResult> SearchStructureAsync(
        string organizationId, SearchRequest? request, SearchStructureHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(new SearchStructureQuery(organizationId, request?.Query, request?.Expand), cancellationToken));
}
