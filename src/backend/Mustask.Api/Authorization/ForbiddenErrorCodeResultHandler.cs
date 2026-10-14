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
/// <see cref="OrganizationErrorCodes.AccessForbidden"/>. Each is logged once at Information with
/// the reason, the organization and the code the route required — never a name.
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
            logger.LogInformation(
                "Organization access refused: {Reason}, organization {OrganizationId}, code {Code}",
                failure.Kind,
                failure.OrganizationId,
                failure.Code);

            throw failure.Kind == OrganizationAccessFailureKind.OrganizationNotFound
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
