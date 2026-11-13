using Mustask.Api.Authorization;
using Mustask.Application.Abstractions.Imports;
using Mustask.Application.Features.Imports.CopyTemplate;
using Mustask.Application.Features.Imports.CreateTemplate;
using Mustask.Application.Features.Imports.DeleteTemplate;
using Mustask.Application.Features.Imports.DownloadDryRunReport;
using Mustask.Application.Features.Imports.GetCatalogue;
using Mustask.Application.Features.Imports.GetDryRun;
using Mustask.Application.Features.Imports.GetFileValues;
using Mustask.Application.Features.Imports.GetTemplate;
using Mustask.Application.Features.Imports.GetTemplateColumns;
using Mustask.Application.Features.Imports.ListTemplates;
using Mustask.Application.Features.Imports.ReadFile;
using Mustask.Application.Features.Imports.RunDryRun;
using Mustask.Application.Features.Imports.SampleChain;
using Mustask.Application.Features.Imports.UpdateTemplate;
using Mustask.Application.Features.Imports.UploadFile;
using Mustask.Domain.Identity;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.Source;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;

namespace Mustask.Api.Features.Imports;

/// <summary>
/// The import constructor's routes (mod-024 api-contract). They sit under <c>/api/organizations/{organizationId}/imports</c>. The three
/// reads every member makes declare <c>imports.template.read</c>; the others judge the caller's write or run right <b>anywhere</b>
/// (a project manager's right sits on a project), so the handler decides and the route only requires membership.
/// </summary>
public static class ImportEndpoints
{
    private const string Tag = "Imports";

    private const string DecidedByRights =
        "the rights are template.write / import.run held anywhere in the organization or the canEdit/canDelete rule of the template; the handler reads the caller's access once and decides";

    /// <summary>The upload's cap: the file limit plus the multipart envelope.</summary>
    private const long UploadBodyLimit = ImportLimits.MaxFileBytes + (1024 * 1024);

    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        var imports = app.MapGroup("/api/organizations/{organizationId}/imports").WithTags(Tag);

        imports.MapGet("/catalogue", async (string organizationId, GetCatalogueHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(new GetCatalogueQuery(organizationId), ct)))
            .RequiresOrganizationPermission(PermissionCatalog.ImportsTemplateRead)
            .Produces<CatalogueView>().WithErrors();

        imports.MapGet("/templates", async (
                string organizationId, string? name, string? author, string? sort, int? page, int? pageSize, ListTemplatesHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(new ListTemplatesQuery(organizationId, name, author, sort, page, pageSize), ct)))
            .RequiresOrganizationPermission(PermissionCatalog.ImportsTemplateRead)
            .Produces<TemplatePageView>().WithErrors();

        imports.MapGet("/templates/{templateId}", async (string organizationId, string templateId, GetTemplateHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(new GetTemplateQuery(organizationId, templateId), ct)))
            .RequiresOrganizationPermissionDecidedInHandler(PermissionCatalog.ImportsTemplateWrite, DecidedByRights)
            .Produces<TemplateView>().WithErrors();

        imports.MapGet("/templates/{templateId}/columns", async (string organizationId, string templateId, GetTemplateColumnsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(new GetTemplateColumnsQuery(organizationId, templateId), ct)))
            .RequiresOrganizationPermission(PermissionCatalog.ImportsTemplateRead)
            .Produces<TemplateColumnsView>().WithErrors();

        imports.MapPost("/templates", async (string organizationId, TemplateBody? body, CreateTemplateHandler handler, CancellationToken ct) =>
            {
                var created = await handler.Handle(new CreateTemplateCommand(organizationId, body ?? new TemplateBody(null, null)), ct);

                return Results.Created($"/api/organizations/{organizationId}/imports/templates/{created.Id}", created);
            })
            .RequiresOrganizationPermissionDecidedInHandler(PermissionCatalog.ImportsTemplateWrite, DecidedByRights)
            .Produces<TemplateView>(StatusCodes.Status201Created).WithErrors();

        imports.MapPut("/templates/{templateId}", async (
                string organizationId, string templateId, TemplateBody? body, UpdateTemplateHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(new UpdateTemplateCommand(organizationId, templateId, body ?? new TemplateBody(null, null)), ct)))
            .RequiresOrganizationPermissionDecidedInHandler(PermissionCatalog.ImportsTemplateWrite, DecidedByRights)
            .Produces<TemplateView>().WithErrors();

        imports.MapPost("/templates/{templateId}/copy", async (string organizationId, string templateId, CopyTemplateHandler handler, CancellationToken ct) =>
            {
                var copy = await handler.Handle(new CopyTemplateCommand(organizationId, templateId), ct);

                return Results.Created($"/api/organizations/{organizationId}/imports/templates/{copy.Id}", copy);
            })
            .RequiresOrganizationPermissionDecidedInHandler(PermissionCatalog.ImportsTemplateWrite, DecidedByRights)
            .Produces<TemplateView>(StatusCodes.Status201Created).WithErrors();

        imports.MapDelete("/templates/{templateId}", async (string organizationId, string templateId, DeleteTemplateHandler handler, CancellationToken ct) =>
            {
                await handler.Handle(new DeleteTemplateCommand(organizationId, templateId), ct);

                return Results.NoContent();
            })
            .RequiresOrganizationPermissionDecidedInHandler(PermissionCatalog.ImportsTemplateWrite, DecidedByRights)
            .Produces(StatusCodes.Status204NoContent).WithErrors();

        imports.MapPost("/files", UploadAsync)
            .RequiresOrganizationPermissionDecidedInHandler(PermissionCatalog.ImportsTemplateWrite, DecidedByRights)
            .Produces<FileUploadedView>(StatusCodes.Status201Created).WithErrors();

        imports.MapGet("/files/{fileId}/columns", async (
                string organizationId, string fileId, string? sheet, int? headerRow, int? firstDataRow, string? encoding, string? delimiter,
                ReadFileHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(
                    new ReadFileQuery(organizationId, fileId, new FileSourceBody(sheet, headerRow, firstDataRow, encoding, delimiter)), ct)))
            .RequiresOrganizationPermissionDecidedInHandler(PermissionCatalog.ImportsTemplateWrite, DecidedByRights)
            .Produces<FileReadView>().WithErrors();

        imports.MapPost("/files/{fileId}/values", async (
                string organizationId, string fileId, MappingProbeBody? body, GetFileValuesHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(new GetFileValuesQuery(organizationId, fileId, body), ct)))
            .RequiresOrganizationPermissionDecidedInHandler(PermissionCatalog.ImportsTemplateWrite, DecidedByRights)
            .Produces<FileValuesView>().WithErrors();

        imports.MapPost("/files/{fileId}/chain-sample", async (
                string organizationId, string fileId, MappingProbeBody? body, SampleChainHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(new SampleChainQuery(organizationId, fileId, body), ct)))
            .RequiresOrganizationPermissionDecidedInHandler(PermissionCatalog.ImportsTemplateWrite, DecidedByRights)
            .Produces<ChainSampleView>().WithErrors();

        imports.MapPost("/templates/{templateId}/dry-runs", async (
                string organizationId, string templateId, DryRunRequest? body, RunDryRunHandler handler, CancellationToken ct) =>
            {
                var run = await handler.Handle(new RunDryRunCommand(organizationId, templateId, body?.FileId), ct);

                return Results.Created($"/api/organizations/{organizationId}/imports/dry-runs/{run.Id}", run);
            })
            .RequiresOrganizationPermissionDecidedInHandler(PermissionCatalog.ImportsImportRun, DecidedByRights)
            .Produces<DryRunView>(StatusCodes.Status201Created).WithErrors();

        imports.MapGet("/dry-runs/{dryRunId}", async (string organizationId, string dryRunId, string? result, GetDryRunHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(new GetDryRunQuery(organizationId, dryRunId, result), ct)))
            .RequiresOrganizationPermissionDecidedInHandler(PermissionCatalog.ImportsImportRun, DecidedByRights)
            .Produces<DryRunView>().WithErrors();

        imports.MapGet("/dry-runs/{dryRunId}/report", async (string organizationId, string dryRunId, DownloadDryRunReportHandler handler, CancellationToken ct) =>
            {
                var report = await handler.Handle(new DownloadDryRunReportQuery(organizationId, dryRunId), ct);

                return Results.File(report.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", report.FileName);
            })
            .RequiresOrganizationPermissionDecidedInHandler(PermissionCatalog.ImportsImportRun, DecidedByRights)
            .Produces(StatusCodes.Status200OK, contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet").WithErrors();

        return app;
    }

    private static RouteHandlerBuilder WithErrors(this RouteHandlerBuilder builder) =>
        builder
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    /// <summary>
    /// `POST /imports/files` (§ 3.1): the multipart body is read by hand — no anti-forgery token belongs to a bearer-token API — under a body
    /// cap of the file limit plus the envelope. A body over it is the same refusal as a file over the limit.
    /// </summary>
    private static async Task<IResult> UploadAsync(string organizationId, HttpContext context, UploadFileHandler handler, CancellationToken cancellationToken)
    {
        await handler.EnsureAllowedAsync(organizationId, cancellationToken);

        var request = context.Request;

        if (!request.HasFormContentType || request.ContentLength > UploadBodyLimit)
        {
            throw SourceRefusal.Because(request.HasFormContentType ? ImportErrorCodes.FileTooLarge : ImportErrorCodes.FileUnsupportedFormat);
        }

        var limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (limit is { IsReadOnly: false })
        {
            limit.MaxRequestBodySize = UploadBodyLimit;
        }

        IFormCollection form;

        try
        {
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (BadHttpRequestException)
        {
            throw SourceRefusal.Because(ImportErrorCodes.FileTooLarge);
        }

        var file = form.Files.GetFile("file") ?? throw SourceRefusal.Because(ImportErrorCodes.FileUnreadable);

        if (file.Length > ImportLimits.MaxFileBytes)
        {
            throw SourceRefusal.Because(ImportErrorCodes.FileTooLarge);
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        var options = new FileSourceBody(
            form["sheet"].ToString(), IntOf(form["headerRow"]), IntOf(form["firstDataRow"]), form["encoding"].ToString(), form["delimiter"].ToString());
        var uploaded = await handler.Handle(new UploadFileCommand(organizationId, Path.GetFileName(file.FileName), buffer.ToArray(), options), cancellationToken);

        return Results.Created($"/api/organizations/{organizationId}/imports/files/{uploaded.FileId}/columns", uploaded);
    }

    private static int? IntOf(string? text) => int.TryParse(text, out var value) ? value : null;
}

/// <summary>The body of `POST /imports/templates/{id}/dry-runs` (§ 4.1).</summary>
public sealed record DryRunRequest(Guid? FileId);
