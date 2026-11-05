using Mustask.Api.Authorization;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations.AppointAdministrator;
using Mustask.Application.Features.Organizations.GetRescueAppointments;
using Mustask.Application.Features.Organizations.GetRescueOrganizations;
using Mustask.Application.Features.Organizations.SearchAppointeeCandidates;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Mustask.Api.Features.Organizations;

/// <summary>
/// The rescue routes of feat-033 (api-contract § 1): a system administrator sees every organization and appoints an administrator
/// in any of them. Each declares the <b>instance</b> code <c>organizations.organization.rescue</c> (ADR-0035 § 2) and answers its
/// own 403, <c>organizations.rescue.forbidden</c> «Недостаточно прав», never <c>http.forbidden</c>. The policy runs before the
/// body is read. The two routes with <c>{organizationId}</c> are the inventory exceptions of ADR-0029 § 9: the caller is not a
/// member and reads no organization content. <c>rescue</c> is a literal segment, which the router ranks above
/// <c>{organizationId}</c> (the <c>/deactivated</c> precedent).
/// </summary>
public static class RescueEndpoints
{
    private const string Tag = "Organizations";

    public static IEndpointRouteBuilder MapRescueEndpoints(this IEndpointRouteBuilder app)
    {
        var rescue = app.MapGroup("/api/organizations/rescue").WithTags(Tag);

        rescue.MapGet("/", GetRescueOrganizationsAsync)
            .RequiresPermission(PermissionCatalog.OrganizationsOrganizationRescue)
            .WithForbiddenErrorCode(OrganizationErrorCodes.RescueForbidden)
            .Produces<RescueOrganizationsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        rescue.MapGet("/appointee-candidates", SearchAppointeeCandidatesAsync)
            .RequiresPermission(PermissionCatalog.OrganizationsOrganizationRescue)
            .WithForbiddenErrorCode(OrganizationErrorCodes.RescueForbidden)
            .Produces<AppointeeCandidatesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        rescue.MapPost("/{organizationId}/appointments", AppointAdministratorAsync)
            .RequiresPermission(PermissionCatalog.OrganizationsOrganizationRescue)
            .WithForbiddenErrorCode(OrganizationErrorCodes.RescueForbidden)
            .Produces<AppointmentView>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        rescue.MapGet("/{organizationId}/appointments", GetRescueAppointmentsAsync)
            .RequiresPermission(PermissionCatalog.OrganizationsOrganizationRescue)
            .WithForbiddenErrorCode(OrganizationErrorCodes.RescueForbidden)
            .Produces<IReadOnlyList<AppointmentView>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetRescueOrganizationsAsync(
        string? page, string? name, GetRescueOrganizationsHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(new GetRescueOrganizationsQuery(page, name), cancellationToken));

    private static async Task<IResult> SearchAppointeeCandidatesAsync(
        string? text, SearchAppointeeCandidatesHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(new SearchAppointeeCandidatesQuery(text), cancellationToken));

    private static async Task<IResult> AppointAdministratorAsync(
        string organizationId, AppointRequest? request, AppointAdministratorHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new AppointAdministratorCommand(organizationId, request?.UserId, request?.Reason), cancellationToken);

        // A record has no read of its own, so no Location.
        return Results.Json(result.Appointment, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetRescueAppointmentsAsync(
        string organizationId, GetRescueAppointmentsHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.Handle(new GetRescueAppointmentsQuery(organizationId), cancellationToken));

    // Every field nullable, so a missing one reaches the handler as a validation answer.
    private sealed record AppointRequest(string? UserId, string? Reason);
}
