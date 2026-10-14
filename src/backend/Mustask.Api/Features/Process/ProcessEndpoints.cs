using Mustask.Api.Authorization;
using Mustask.Application.Features.Process.GetProcessDefinition;
using Mustask.Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Mustask.Api.Features.Process;

/// <summary>
/// The process module's routes. The module has one HTTP surface, a read: nothing
/// seeds, creates, renames, reorders or deletes a process, type, state or level
/// over HTTP (BR-M2-13). The seed is an in-process seam, <c>IProcessSeed</c>.
/// </summary>
public static class ProcessEndpoints
{
    public static IEndpointRouteBuilder MapProcessEndpoints(this IEndpointRouteBuilder app)
    {
        // The route sits under /api/organizations/..., not under /api/<module>/... as
        // backend.md's default would put it (status.md D-9, after mod-015 D-17), and the
        // parameter is named organizationId because the scoped check keys on that name
        // (mod-002 D-g, mod-016 ADR-0027).
        //
        // The parameter is a plain string with no constraint, so a malformed value reaches
        // the check and gets the not-found code. A :guid constraint would turn it into
        // http.not_found, and a malformed value must not be told apart from an unknown one.
        //
        // mod-016 (api-contract § 4.2 row 27): the organization code is derived from the caller's
        // membership, never from the token. A non-member, and a deactivated or unknown organization,
        // get the one not-found answer; a role without the code gets 403.
        app.MapGet("/api/organizations/{organizationId}/process", GetProcessDefinitionAsync)
            .RequiresOrganizationPermission(PermissionCatalog.ProcessDefinitionRead)
            .Produces<ProcessDefinitionResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Process");

        return app;
    }

    private static async Task<IResult> GetProcessDefinitionAsync(
        string organizationId,
        GetProcessDefinitionHandler handler,
        CancellationToken cancellationToken)
    {
        var response = await handler.Handle(new GetProcessDefinitionQuery(organizationId), cancellationToken);

        return Results.Ok(response);
    }
}
