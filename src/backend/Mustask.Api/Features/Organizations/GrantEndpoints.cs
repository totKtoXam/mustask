using Mustask.Api.Authorization;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations.ChangeGrantRole;
using Mustask.Application.Features.Organizations.GetGrants;
using Mustask.Application.Features.Organizations.GiveGrant;
using Mustask.Application.Features.Organizations.RevokeGrant;
using Mustask.Application.Features.Organizations.SearchGrantCandidates;
using Mustask.Domain.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Mustask.Api.Features.Organizations;

/// <summary>
/// The grant routes of mod-019 (api-contract § 2): the same five routes under a department, a project and a team. Reads of an
/// area and a team declare an organization-wide read code (every member holds it); the writes, the candidate search and the
/// project's read declare their code <b>on the addressed object</b>, so a grant that reaches the object counts
/// (ADR-0029 § 6, as amended by ADR-0037).
/// </summary>
public static class GrantEndpoints
{
    private const string Tag = "Organizations";

    private const string OrganizationPath = "/api/organizations/{organizationId}";

    public static IEndpointRouteBuilder MapGrantEndpoints(this IEndpointRouteBuilder app)
    {
        MapScope(app, "/areas/{areaId}", ResourceScope.Area, PermissionCatalog.OrganizationsAreaRead, PermissionCatalog.OrganizationsAreaGrant, readOnObject: false);
        MapScope(app, "/projects/{projectId}", ResourceScope.Project, PermissionCatalog.OrganizationsProjectRead, PermissionCatalog.OrganizationsProjectGrant, readOnObject: true);
        MapScope(app, "/teams/{teamId}", ResourceScope.Team, PermissionCatalog.OrganizationsTeamRead, PermissionCatalog.OrganizationsTeamGrant, readOnObject: false);

        return app;
    }

    private static void MapScope(
        IEndpointRouteBuilder app, string objectPath, ResourceScope scope, string readCode, string grantCode, bool readOnObject)
    {
        var group = app.MapGroup(OrganizationPath + objectPath).WithTags(Tag);
        var objectParameter = scope switch { ResourceScope.Area => "areaId", ResourceScope.Project => "projectId", _ => "teamId" };

        var read = group.MapGet("/grants", (HttpContext context, GetGrantsHandler handler, CancellationToken cancellationToken) =>
            GetAsync(context, objectParameter, scope, handler, cancellationToken));
        _ = readOnObject ? read.RequiresOrganizationPermission(readCode, scope) : read.RequiresOrganizationPermission(readCode);
        read.Produces<GetGrantsResponse>().WithErrors();

        group.MapGet("/grant-candidates", (HttpContext context, string? text, SearchGrantCandidatesHandler handler, CancellationToken cancellationToken) =>
                CandidatesAsync(context, objectParameter, scope, text, handler, cancellationToken))
            .RequiresOrganizationPermission(grantCode, scope)
            .Produces<SearchGrantCandidatesResponse>().WithErrors();

        group.MapPost("/grants", (HttpContext context, GrantRequest? request, GiveGrantHandler handler, CancellationToken cancellationToken) =>
                GiveAsync(context, objectParameter, scope, request, handler, cancellationToken))
            .RequiresOrganizationPermission(grantCode, scope)
            .Produces<GrantRowView>(StatusCodes.Status201Created).WithErrors();

        group.MapPut("/grants/{userId}", (HttpContext context, string userId, GrantRequest? request, ChangeGrantRoleHandler handler, CancellationToken cancellationToken) =>
                ChangeAsync(context, objectParameter, scope, userId, request, handler, cancellationToken))
            .RequiresOrganizationPermission(grantCode, scope)
            .Produces<GrantRowView>().WithErrors();

        group.MapDelete("/grants/{userId}", (HttpContext context, string userId, RevokeGrantHandler handler, CancellationToken cancellationToken) =>
                RevokeAsync(context, objectParameter, scope, userId, handler, cancellationToken))
            .RequiresOrganizationPermission(grantCode, scope)
            .Produces(StatusCodes.Status204NoContent).WithErrors();
    }

    private static RouteHandlerBuilder WithErrors(this RouteHandlerBuilder builder) =>
        builder
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static string Route(HttpContext context, string name) => (context.Request.RouteValues[name] as string) ?? string.Empty;

    private static async Task<IResult> GetAsync(
        HttpContext context, string objectParameter, ResourceScope scope, GetGrantsHandler handler, CancellationToken cancellationToken)
    {
        var response = await handler.Handle(
            new GetGrantsQuery(Route(context, "organizationId"), scope, Route(context, objectParameter)), cancellationToken);

        context.Response.Headers.CacheControl = "no-store";

        return Results.Ok(response);
    }

    private static async Task<IResult> CandidatesAsync(
        HttpContext context, string objectParameter, ResourceScope scope, string? text, SearchGrantCandidatesHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(
            new SearchGrantCandidatesQuery(Route(context, "organizationId"), scope, Route(context, objectParameter), text), cancellationToken));

    private static async Task<IResult> GiveAsync(
        HttpContext context, string objectParameter, ResourceScope scope, GrantRequest? request, GiveGrantHandler handler,
        CancellationToken cancellationToken)
    {
        var organizationId = Route(context, "organizationId");
        var objectId = Route(context, objectParameter);
        var row = await handler.Handle(new GiveGrantCommand(organizationId, scope, objectId, request?.UserId, request?.Role), cancellationToken);

        return Results.Created($"{context.Request.Path.Value!.ToLowerInvariant()}/{Uri.EscapeDataString(row.User.Id)}", row);
    }

    private static async Task<IResult> ChangeAsync(
        HttpContext context, string objectParameter, ResourceScope scope, string userId, GrantRequest? request,
        ChangeGrantRoleHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(
            new ChangeGrantRoleCommand(Route(context, "organizationId"), scope, Route(context, objectParameter), userId, request?.Role),
            cancellationToken));

    private static async Task<IResult> RevokeAsync(
        HttpContext context, string objectParameter, ResourceScope scope, string userId, RevokeGrantHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.Handle(
            new RevokeGrantCommand(Route(context, "organizationId"), scope, Route(context, objectParameter), userId), cancellationToken);

        return Results.NoContent();
    }

    /// <summary>The body of a POST (both fields) and of a PUT (<c>role</c> only; <c>userId</c> is the path's); every field nullable so a missing one is a validation answer.</summary>
    private sealed record GrantRequest(string? UserId, string? Role);
}
