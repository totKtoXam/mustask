using System.Net;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// mod-002 D-g, BR-M1-20, api-contract § 0.3: the organization-context filter
/// answers every organization-addressed route, mod-001's process read
/// included, before any handler runs. The handlers are phase-1 stubs, which is
/// the point: an answer here can only come from the filter.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class OrganizationContextFilterTests(MustaskApiFactory factory)
{
    private const string NotFoundCode = "organizations.organization.not_found";

    public static TheoryData<string, string> Routes()
    {
        var data = new TheoryData<string, string>();

        foreach (var (method, template) in OrganizationAddressedRoutes.All)
        {
            data.Add(method, template);
        }

        return data;
    }

    public static TheoryData<string, string> RoutesRefusingADeactivatedOrganization()
    {
        var data = new TheoryData<string, string>();

        foreach (var (method, template) in OrganizationAddressedRoutes.All.Where(route => $"{route.Method} {route.Template}" != OrganizationAddressedRoutes.Restore))
        {
            data.Add(method, template);
        }

        return data;
    }

    // US-2 · "An unknown organization answers not found" (BR-M1-20): a malformed id and an unknown one answer alike.
    [Theory]
    [MemberData(nameof(Routes))]
    public async Task EveryOrganizationRoute_MalformedOrUnknownOrganization_Returns404OrganizationNotFound(string method, string template)
    {
        foreach (var organizationId in new[] { "abc", "00000000-0000-0000-0000-000000000000", Guid.CreateVersion7().ToString() })
        {
            var answer = await SendAsync(method, template, organizationId);

            Assert.True(answer.Status == HttpStatusCode.NotFound, $"{method} {template} with «{organizationId}» answered {answer.Status}.");
            Assert.Equal(NotFoundCode, answer.ErrorCode);
            Assert.Equal("Организация не найдена.", answer.Detail);
        }
    }

    // US-2 · "A deactivated organization takes no change and answers not found": every route but its restore, and the
    // organization is left exactly as it was.
    [Theory]
    [MemberData(nameof(RoutesRefusingADeactivatedOrganization))]
    public async Task EveryOrganizationRoute_DeactivatedOrganization_Returns404OrganizationNotFound_ChangesNothing(string method, string template)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, status: OrganizationStatus.Deactivated);

        var answer = await SendAsync(method, template, organization.Id.ToString());

        Assert.True(answer.Status == HttpStatusCode.NotFound, $"{method} {template} answered {answer.Status}.");
        Assert.Equal(NotFoundCode, answer.ErrorCode);

        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IOrganizationStructure>()
            .FindOrganizationAsync(organization.Id, CancellationToken.None);
        Assert.Equal((organization.Name, OrganizationStatus.Deactivated), (stored!.Name, stored.Status));
    }

    // US-1 · "A visitor who is not signed in": the challenge comes before the not-found, whatever the id.
    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Anonymous_GetsTheChallengeBeforeTheNotFound(string method, string template)
    {
        var answer = await OrganizationsApi.SendAsync(
            factory, new HttpMethod(method), OrganizationAddressedRoutes.Path(template, "abc"), OrganizationAddressedRoutes.Body(method));

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal("http.unauthorized", answer.ErrorCode);
    }

    // mod-001's process read passes the filter for an active organization and reads its process (US-1 "created with its process").
    [Fact]
    public async Task ProcessRead_ActiveOrganization_PassesTheFilter()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, $"/api/organizations/{organization.Id}/process", token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(organization.ProcessId.Value, answer.Body.GetProperty("id").GetGuid());
    }

    private Task<Answer> SendAsync(string method, string template, string organizationId) =>
        OrganizationsApi.SendAsync(
            factory,
            new HttpMethod(method),
            OrganizationAddressedRoutes.Path(template, organizationId),
            OrganizationAddressedRoutes.Body(method),
            OrganizationsTokens.SignedIn());
}
