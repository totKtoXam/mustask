using System.Net;
using Mustask.Api.Authorization;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// mod-002 D-p, plan-backend § 7.4: the create endpoint answers its own 403
/// code, «Недостаточно прав», where the platform would answer the generic
/// <c>http.forbidden</c>. Every other permission endpoint, and every challenge,
/// is unchanged.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ForbiddenErrorCodeTests(MustaskApiFactory factory)
{
    // US-1 · "A user without the right cannot create an organization" (backend half): its own code and text, nothing created.
    [Fact]
    public async Task CreateOrganization_WithoutTheRight_Returns403CreateForbidden_CreatesNothing()
    {
        var name = OrganizationsTestData.UniqueName();

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, "/api/organizations", new { name }, OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.Forbidden, answer.Status);
        Assert.Equal("application/problem+json", answer.MediaType);
        Assert.Equal("organizations.organization.create_forbidden", answer.ErrorCode);
        Assert.Equal("Недостаточно прав", answer.Detail);

        using var scope = factory.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IOrganizationRepository>()
            .FindNameHolderAsync(EntityName.Normalize(name), exceptId: null, CancellationToken.None));
    }

    // The challenge is never touched: an anonymous request is a 401, not the create endpoint's 403.
    [Fact]
    public async Task CreateOrganization_Anonymous_Returns401()
    {
        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, "/api/organizations", new { name = "Х" });

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal("http.unauthorized", answer.ErrorCode);
    }

    // Every other permission endpoint keeps the platform's http.forbidden.
    [Fact]
    public async Task OtherPermissionEndpoint_WithoutTheRight_StillAnswersHttpForbidden()
    {
        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, "/api/dictionaries/languages", token: KeycloakTestRealm.CreateToken([PermissionCatalog.OrganizationsOrganizationCreate]));

        Assert.Equal(HttpStatusCode.Forbidden, answer.Status);
        Assert.Equal("http.forbidden", answer.ErrorCode);
    }

    // The policy stays a policy: the endpoint is still a permission endpoint in the category inventory, with its code.
    [Fact]
    public void CreateEndpoint_StaysAPermissionEndpoint_WithItsOwnForbiddenCode()
    {
        using var scope = factory.Services.CreateScope();
        var endpoint = scope.ServiceProvider.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(candidate =>
                candidate.RoutePattern.RawText!.TrimEnd('/') == "/api/organizations"
                && candidate.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains("POST"));

        var category = Assert.Single(endpoint.Metadata.GetOrderedMetadata<EndpointCategoryMetadata>());
        Assert.Equal(EndpointCategoryKind.Permission, category.Kind);
        Assert.Equal(PermissionCatalog.OrganizationsOrganizationCreate, category.PermissionCode);
        Assert.Equal(
            OrganizationErrorCodes.OrganizationCreateForbidden,
            endpoint.Metadata.GetMetadata<ForbiddenErrorCodeMetadata>()!.Code);
    }
}
