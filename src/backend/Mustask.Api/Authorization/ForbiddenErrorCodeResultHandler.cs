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
/// </summary>
public sealed class ForbiddenErrorCodeResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden
            && context.GetEndpoint()?.Metadata.GetMetadata<ForbiddenErrorCodeMetadata>() is { } metadata)
        {
            throw new ForbiddenException(metadata.Code);
        }

        return _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
