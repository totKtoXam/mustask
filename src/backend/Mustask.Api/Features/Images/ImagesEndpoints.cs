using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Mustask.Api.Authorization;
using Mustask.Application.Features.Images;
using Mustask.Application.Features.Images.ReduceDescriptionImage;
using Mustask.Application.Features.Images.UploadDescriptionImage;
using Mustask.Domain.Identity;

namespace Mustask.Api.Features.Images;

/// <summary>
/// Images in a work item's Описание (feat-078 api-contract). The two upload routes sit beside the item's own under
/// <c>/api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/images</c>, behind the item's write code;
/// the body is the file's bytes, read by the handler after its address checks.
/// </summary>
public static class ImagesEndpoints
{
    private const string Tag = "Images";

    public static IEndpointRouteBuilder MapImagesEndpoints(this IEndpointRouteBuilder app)
    {
        var uploads = app.MapGroup("/api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/images").WithTags(Tag);

        // api-contract § 1. An unbound upload of the caller for this item.
        uploads.MapPost("/", UploadAsync)
            .RequiresOrganizationPermission(PermissionCatalog.WorkItemsWorkItemWrite, ResourceScope.Project)
            .Produces<ImageUploadView>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        // api-contract § 2. The reduction of a picture over 5 MB; the stored result is an unbound upload.
        uploads.MapPost("/reductions", ReduceAsync)
            .RequiresOrganizationPermission(PermissionCatalog.WorkItemsWorkItemWrite, ResourceScope.Project)
            .Produces<ReduceDescriptionImageResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> UploadAsync(
        string organizationId,
        string projectId,
        string identifier,
        HttpRequest request,
        UploadDescriptionImageHandler handler,
        CancellationToken cancellationToken)
    {
        var response = await handler.Handle(
            new UploadDescriptionImageCommand(organizationId, projectId, identifier, new HttpUploadBody(request)), cancellationToken);

        return Results.Json(response.Image, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> ReduceAsync(
        string organizationId,
        string projectId,
        string identifier,
        HttpRequest request,
        ReduceDescriptionImageHandler handler,
        CancellationToken cancellationToken)
    {
        var response = await handler.Handle(
            new ReduceDescriptionImageCommand(organizationId, projectId, identifier, new HttpUploadBody(request)), cancellationToken);

        return Results.Json(response, statusCode: StatusCodes.Status201Created);
    }
}
