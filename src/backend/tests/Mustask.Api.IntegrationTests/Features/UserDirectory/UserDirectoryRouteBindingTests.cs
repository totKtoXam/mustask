using System.Reflection;
using Mustask.Api.Features.UserDirectory;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014 ADR-0025 § 3: every push route takes its body only through
/// <see cref="SignedPushBody{T}"/>, so a route added later cannot skip the
/// signature check; and the routes stay out of the OpenAPI document.
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class UserDirectoryRouteBindingTests(MustaskApiFactory factory)
{
    private const string FeedPrefix = "/api/user-directory/feed/";

    private List<RouteEndpoint> FeedEndpoints()
    {
        using var scope = factory.Services.CreateScope();

        return scope.ServiceProvider.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith(FeedPrefix, StringComparison.Ordinal))
            .ToList();
    }

    [Fact]
    public void EveryPushRoute_BindsItsBodyThroughSignedPushBody()
    {
        var endpoints = FeedEndpoints();

        Assert.NotEmpty(endpoints);

        foreach (var endpoint in endpoints)
        {
            var handler = endpoint.Metadata.GetMetadata<MethodInfo>()
                ?? throw new InvalidOperationException($"{endpoint.RoutePattern.RawText} carries no handler MethodInfo.");

            var signed = handler.GetParameters()
                .Count(parameter => parameter.ParameterType.IsGenericType
                    && parameter.ParameterType.GetGenericTypeDefinition() == typeof(SignedPushBody<>));

            Assert.True(signed == 1, $"{endpoint.RoutePattern.RawText} binds {signed} SignedPushBody<T> parameters, expected exactly one.");
        }
    }

    [Fact]
    public void PushRoutes_AreNotInTheOpenApiDocument()
    {
        foreach (var endpoint in FeedEndpoints())
        {
            Assert.NotNull(endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.IExcludeFromDescriptionMetadata>());
        }
    }
}
