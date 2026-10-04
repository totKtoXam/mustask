using Mustask.Api.Authorization;
using Mustask.Application.Features.UserDirectory.ApplySnapshotPage;
using Mustask.Application.Features.UserDirectory.ApplyUserChanges;
using Mustask.Application.Features.UserDirectory.BeginSnapshot;
using Mustask.Application.Features.UserDirectory.CompleteSnapshot;
using Mustask.Domain.UserDirectory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Mustask.Api.Features.UserDirectory;

/// <summary>
/// <c>/api/user-directory/feed/*</c> — the push channel of the user
/// projection (mod-014 api-contract.md, Part B), called by the Keycloak
/// provider <c>mustask-user-projection</c> only. Public with respect to the
/// Bearer scheme (ADR-0022's precedent) and authenticated by its own: each
/// handler takes its body only as <see cref="SignedPushBody{T}"/>. Out of the
/// OpenAPI document — no person calls these routes (BR-UP-12).
/// </summary>
public static class UserDirectoryEndpoints
{
    /// <summary>The reason recorded on each route's <c>Public</c> category.</summary>
    internal const string PublicReason =
        "authenticated by the push channel's HMAC signature (ADR-0025) in SignedPushBody, not by Bearer";

    public static IEndpointRouteBuilder MapUserDirectoryEndpoints(this IEndpointRouteBuilder app)
    {
        var feed = app.MapGroup(PushBodyReader.FeedBasePath);

        // B-1
        feed.MapPost("/changes", ApplyUserChangesAsync)
            .Public(PublicReason)
            .ExcludeFromDescription()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // B-2
        feed.MapPut("/snapshots/{snapshotId:guid}", BeginSnapshotAsync)
            .Public(PublicReason)
            .ExcludeFromDescription()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // B-3
        feed.MapPut("/snapshots/{snapshotId:guid}/pages/{pageNumber:int}", ApplySnapshotPageAsync)
            .Public(PublicReason)
            .ExcludeFromDescription()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // B-4
        feed.MapPut("/snapshots/{snapshotId:guid}/completion", CompleteSnapshotAsync)
            .Public(PublicReason)
            .ExcludeFromDescription()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> ApplyUserChangesAsync(
        SignedPushBody<ChangesRequest> body,
        ApplyUserChangesHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ApplyUserChangesCommand(body.Value.Changes), cancellationToken);

        return Results.Ok(result.Response);
    }

    private static async Task<IResult> BeginSnapshotAsync(
        Guid snapshotId,
        SignedPushBody<BeginSnapshotRequest> body,
        BeginSnapshotHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new BeginSnapshotCommand(snapshotId, body.Value.EnumerationStart), cancellationToken);

        return Results.Ok(result.Response);
    }

    private static async Task<IResult> ApplySnapshotPageAsync(
        Guid snapshotId,
        int pageNumber,
        SignedPushBody<PageRequest> body,
        ApplySnapshotPageHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new ApplySnapshotPageCommand(snapshotId, pageNumber, body.Value.Entries), cancellationToken);

        return Results.Ok(result.Response);
    }

    private static async Task<IResult> CompleteSnapshotAsync(
        Guid snapshotId,
        SignedPushBody<CompletionRequest> body,
        CompleteSnapshotHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new CompleteSnapshotCommand(snapshotId, body.Value.PageCount, body.Value.EntryCount), cancellationToken);

        return Results.Ok(result.Response);
    }

    /// <summary>B-1's body.</summary>
    internal sealed record ChangesRequest(IReadOnlyList<UserPushEntry?>? Changes);

    /// <summary>B-2's body.</summary>
    internal sealed record BeginSnapshotRequest(long? EnumerationStart);

    /// <summary>B-3's body.</summary>
    internal sealed record PageRequest(IReadOnlyList<UserPushEntry?>? Entries);

    /// <summary>B-4's body.</summary>
    internal sealed record CompletionRequest(int? PageCount, int? EntryCount);
}
