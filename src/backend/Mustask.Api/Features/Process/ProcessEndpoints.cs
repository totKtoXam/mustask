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
        // parameter is named organizationId because mod-002's organization-context filter
        // keys on that name (mod-002 D-g).
        //
        // The parameter is a plain string with no constraint, so a malformed value reaches
        // the handler and gets the not-found code. A :guid constraint would turn it into
        // http.not_found, and a malformed value must not be told apart from an unknown one.
        app.MapGet("/api/organizations/{organizationId}/process", GetProcessDefinitionAsync)
            .AuthenticatedOnly(
                $"Declares {OrganizationPermissionCatalog.ProcessDefinitionRead}, an organization-scoped code that "
                + "no token carries and nothing checks yet: it is not in the realm import (BR-M2-10). "
                + "mod-016 retrofits the check, and a non-member then gets the not-found answer, not a 403.")
            // mod-002 D-g: an organization that does not exist or is deactivated answers not found before the read.
            .RequiresOrganizationContext()
            .Produces<ProcessDefinitionResponse>()
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
