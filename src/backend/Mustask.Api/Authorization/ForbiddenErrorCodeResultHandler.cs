using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Mustask.Api.Authorization;

/// <summary>
/// Gives a forbidden request its endpoint's own error code (mod-002 D-p). When
/// authorization forbids a request and the endpoint carries
/// <see cref="ForbiddenErrorCodeMetadata"/>, it throws a
/// <see cref="ForbiddenException"/> with that code; <c>UseExceptionHandler</c>
/// sits first in the pipeline, so the business handler renders it with the
/// catalog text (US-1: «Недостаточно прав»). Everything else, every challenge
/// (401) included, goes to the framework's own handler unchanged.
/// <para>
/// mod-016 (ADR-0027): a failure of the scoped check
/// (<see cref="OrganizationPermissionHandler"/>) is answered first, whatever the
/// endpoint carries — <see cref="OrganizationAccessFailureKind.OrganizationNotFound"/> throws the business
/// 404 <see cref="OrganizationErrorCodes.OrganizationNotFound"/> (the same bytes for every
/// cause), <see cref="OrganizationAccessFailureKind.RoleLacksCode"/> the business 403
/// <see cref="OrganizationErrorCodes.AccessForbidden"/>. Each is logged once at Information (<see cref="RefusalLog"/>) with the route,
/// the code, the resource scope, the user id, the reason and the organization — never a name.
/// </para>
/// </summary>
public sealed class ForbiddenErrorCodeResultHandler(ILogger<ForbiddenErrorCodeResultHandler> logger) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden
            && authorizeResult.AuthorizationFailure?.FailureReasons.OfType<OrganizationAccessFailure>().FirstOrDefault() is { } failure)
        {
            var notFound = failure.Kind == OrganizationAccessFailureKind.OrganizationNotFound;

            RefusalLog.Write(
                logger,
                context,
                failure.Kind.ToString(),
                failure.OrganizationId,
                failure.Code,
                failure.Scope,
                notFound ? OrganizationErrorCodes.OrganizationNotFound : OrganizationErrorCodes.AccessForbidden);

            throw notFound
                ? new NotFoundException(OrganizationErrorCodes.OrganizationNotFound)
                : new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
        }

        if (authorizeResult.Forbidden
            && context.GetEndpoint()?.Metadata.GetMetadata<ForbiddenErrorCodeMetadata>() is { } metadata)
        {
            throw new ForbiddenException(metadata.Code);
        }

        return _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
