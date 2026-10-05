namespace Mustask.Api.Authorization;

/// <summary>The one way to put an endpoint behind the organization-context filter: the metadata and the filter together.</summary>
public static class OrganizationContextEndpoints
{
    /// <summary>
    /// Adds <see cref="OrganizationContextMetadata"/> for the inventory test and
    /// <see cref="OrganizationContextFilter"/> for the runtime. Every route with an
    /// <c>organizationId</c> parameter carries it (mod-002 D-g).
    /// </summary>
    public static RouteHandlerBuilder RequiresOrganizationContext(this RouteHandlerBuilder builder, bool acceptsDeactivated = false)
    {
        builder.WithMetadata(new OrganizationContextMetadata(acceptsDeactivated));

        return builder.AddEndpointFilter(new OrganizationContextFilter());
    }
}
