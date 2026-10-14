using System.Net;
using System.Text.Json;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/{organizationId}</c> (api-contract § 1.4) through
/// the real host and a real PostgreSQL. The organization level of the not-found
/// theories (malformed, unknown, deactivated, a non-member) is the scoped check's,
/// once for every route: <c>ScopedAccessTests</c> (mod-016 ADR-0027). Every test builds its own organization
/// (<see cref="OrganizationsTestData"/>): the database is shared and never
/// emptied, so none asserts the content of a global list.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetOrganizationEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> GetAsync(Guid organizationId, string? token = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, OrganizationsApi.Organization(organizationId), token: token ?? OrganizationsTokens.SignedIn());

    // US-2 · "Organization settings": the settings frame reads the id and the name as stored, trimmed. Nothing else is
    // served: not the process, the creator or the time (api-contract § 1.3, § 7).
    [Fact]
    public async Task Get_Active_ReturnsIdAndName()
    {
        var name = OrganizationsTestData.UniqueName("Ёлка");
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, $"  {name}  ");

        var answer = await GetAsync(organization.Id);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(["id", "name"], PropertyNames(answer.Body));
        Assert.Equal(organization.Id, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal(name, answer.Body.GetProperty("name").GetString());
    }

    // US-1 · "Every signed-in user sees every organization" — since mod-016 every MEMBER does (BR-M1-7): the reader holds no
    // create right, is not the creator and is only a viewer, and the organization still opens.
    [Fact]
    public async Task Get_AViewerWithoutRight_Opens()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var reader = await MembershipTestData.NewMemberAsync(factory, organization.Id, Mustask.Domain.Identity.OrganizationRole.Viewer);

        var answer = await GetAsync(organization.Id, reader.Token);

        Assert.NotEqual(organization.CreatedBy, reader.Id);
        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(organization.Id, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal(organization.Name, answer.Body.GetProperty("name").GetString());
    }

    // US-1 · the same answer for a member who holds the create right: the right narrows nothing but the creation (BR-M1-19).
    [Fact]
    public async Task Get_AMemberWithCreateRight_OpensTheSameOrganization()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var user = await MembershipTestData.NewMemberAsync(factory, organization.Id, Mustask.Domain.Identity.OrganizationRole.Member);

        var answer = await GetAsync(organization.Id, OrganizationsTokens.WithCreateRight(user.Id));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(organization.Name, answer.Body.GetProperty("name").GetString());
    }

    // mod-016 api-contract § 0.1 · the create right is an instance code and opens no organization: a user holding it and no
    // membership gets the one not-found answer (a non-member learns nothing about the organization).
    [Fact]
    public async Task Get_UserWithCreateRightButNoMembership_Gets404OrganizationNotFound()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await GetAsync(organization.Id, OrganizationsTokens.WithCreateRight());

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.organization.not_found", answer.ErrorCode);
    }

    // The id in the address is read in either letter case (api-contract § 0.2) and the answer writes it in lowercase.
    [Fact]
    public async Task Get_UpperCaseId_ReturnsTheOrganizationWithItsLowerCaseId()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"/api/organizations/{organization.Id.ToString().ToUpperInvariant()}",
            token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(organization.Id.ToString(), answer.Body.GetProperty("id").GetString());
    }

    // US-1 · "A visitor who is not signed in sees nothing": the challenge comes first.
    [Fact]
    public async Task Get_NoToken_Returns401()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, OrganizationsApi.Organization(organization.Id));

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal("http.unauthorized", answer.ErrorCode);
    }

    // US-2 · "A deactivated organization answers not found" and "restored exactly as it was": the same organization is
    // refused while deactivated, never revealed as such, and opens again, unchanged, once restored (ADR-0026 rule 5).
    [Fact]
    public async Task Get_DeactivatedThenRestored_Returns404ThenTheSameOrganization()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        await OrganizationsTestData.SetOrganizationStatusAsync(factory, organization.Id, OrganizationStatus.Deactivated);

        var refused = await GetAsync(organization.Id);
        await OrganizationsTestData.SetOrganizationStatusAsync(factory, organization.Id, OrganizationStatus.Active);
        var restored = await GetAsync(organization.Id);

        Assert.Equal(HttpStatusCode.NotFound, refused.Status);
        Assert.Equal("organizations.organization.not_found", refused.ErrorCode);
        Assert.Equal("Организация не найдена.", refused.Detail);
        Assert.Equal(HttpStatusCode.OK, restored.Status);
        Assert.Equal(organization.Id, restored.Body.GetProperty("id").GetGuid());
        Assert.Equal(organization.Name, restored.Body.GetProperty("name").GetString());
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
}
