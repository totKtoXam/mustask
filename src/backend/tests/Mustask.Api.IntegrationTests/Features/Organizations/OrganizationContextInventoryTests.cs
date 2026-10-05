using Mustask.Api.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// mod-002 D-g, fail-closed as the category inventory is: every real endpoint
/// whose route has an <c>organizationId</c> parameter carries the
/// organization-context filter, every endpoint that carries it has the
/// parameter, and only the organization's restore accepts a deactivated one.
/// A later module that adds an organization-addressed route without the filter
/// turns the build red.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class OrganizationContextInventoryTests(MustaskApiFactory factory)
{
    [Fact]
    public void EveryRouteWithAnOrganizationId_CarriesTheFilter_AndOnlyThose()
    {
        var unfiltered = new List<string>();
        var stray = new List<string>();

        foreach (var endpoint in RealEndpoints())
        {
            var addressed = endpoint.RoutePattern.GetParameter(OrganizationContextMetadata.RouteParameter) is not null;
            var filtered = endpoint.Metadata.GetMetadata<OrganizationContextMetadata>() is not null;

            if (addressed && !filtered)
            {
                unfiltered.Add(RouteKey(endpoint));
            }

            if (filtered && !addressed)
            {
                stray.Add(RouteKey(endpoint));
            }
        }

        Assert.True(unfiltered.Count == 0, $"Organization-addressed routes without the organization-context filter: {string.Join(", ", unfiltered)}.");
        Assert.True(stray.Count == 0, $"Routes with the filter but no organizationId parameter: {string.Join(", ", stray)}.");
    }

    // The set is the contract's (api-contract § 5) plus mod-001's process read, no more and no less.
    [Fact]
    public void FilteredRoutes_AreExactlyTheOrganizationAddressedRoutes()
    {
        var filtered = RealEndpoints()
            .Where(endpoint => endpoint.Metadata.GetMetadata<OrganizationContextMetadata>() is not null)
            .Select(RouteKey)
            .Order(StringComparer.Ordinal);

        Assert.Equal(
            OrganizationAddressedRoutes.All.Select(route => $"{route.Method} {route.Template}").Order(StringComparer.Ordinal),
            filtered);
    }

    // api-contract § 1.7: the restore is the one request a deactivated organization accepts.
    [Fact]
    public void OnlyTheOrganizationsRestore_AcceptsADeactivatedOrganization()
    {
        var accepting = RealEndpoints()
            .Where(endpoint => endpoint.Metadata.GetMetadata<OrganizationContextMetadata>() is { AcceptsDeactivated: true })
            .Select(RouteKey);

        Assert.Equal([OrganizationAddressedRoutes.Restore], accepting);
    }

    private List<RouteEndpoint> RealEndpoints()
    {
        using var scope = factory.Services.CreateScope();

        return scope.ServiceProvider.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => !endpoint.RoutePattern.RawText!.StartsWith("/__test/", StringComparison.Ordinal))
            .ToList();
    }

    private static string RouteKey(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
        var template = "/" + endpoint.RoutePattern.RawText!.Trim('/');

        return $"{string.Join("/", methods)} {template}";
    }
}
