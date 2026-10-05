using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Api.Authorization;

/// <summary>
/// The organization-context boundary (mod-002 D-g, BR-M1-20, ADR-0026 rule 5):
/// an organization-addressed request is answered
/// <see cref="OrganizationErrorCodes.OrganizationNotFound"/> when its
/// <c>organizationId</c> is malformed, names no organization, or names one that
/// is not active, unless the endpoint accepts a deactivated one (its restore).
/// Endpoint filters run after authorization, so an anonymous request still
/// gets 401 first. mod-016 extends this filter with membership: a non-member
/// then gets the same answer, never a 403.
/// </summary>
/// <remarks>
/// It is the boundary, not the only check: handlers re-check reachability for
/// what they address (plan-backend § 5.3). For mod-001's process read, which
/// knows nothing of organizations, it is the only one.
/// </remarks>
public sealed class OrganizationContextFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var metadata = httpContext.GetEndpoint()?.Metadata.GetMetadata<OrganizationContextMetadata>()
            ?? throw new InvalidOperationException(
                "The organization-context filter runs only on an endpoint marked by RequiresOrganizationContext.");

        var raw = httpContext.Request.RouteValues[OrganizationContextMetadata.RouteParameter] as string;

        if (!AddressedId.TryParse(raw, out var organizationId))
        {
            throw NotFound();
        }

        var readModel = httpContext.RequestServices.GetRequiredService<IOrganizationReadModel>();
        var status = await readModel.FindStatusAsync(organizationId, httpContext.RequestAborted);

        var accepted = status switch
        {
            OrganizationStatus.Active => true,
            OrganizationStatus.Deactivated => metadata.AcceptsDeactivated,
            _ => false,
        };

        if (!accepted)
        {
            throw NotFound();
        }

        return await next(context);
    }

    private static NotFoundException NotFound() => new(OrganizationErrorCodes.OrganizationNotFound);
}
