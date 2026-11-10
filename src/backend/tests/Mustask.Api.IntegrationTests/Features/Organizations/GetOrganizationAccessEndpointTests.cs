using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/access</c> (mod-016 api-contract § 1.1; US-4 "Mustask.Api serves the role and the
/// codes of one organization"; BR-M1-31): the caller's role in the organization and the organization-scope codes of its bundle, read
/// from the membership at the moment of the request, never cached, never an instance code.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetOrganizationAccessEndpointTests(MustaskApiFactory factory)
{
    private string Path(Guid organizationId) => $"/api/organizations/{organizationId}/access";

    private static List<string> Permissions(JsonElement body) =>
        body.GetProperty("permissions").EnumerateArray().Select(code => code.GetString()!).ToList();

    private static readonly string[] OrganizationScopeReads =
    [
        "organizations.organization.read",
        "organizations.membership.read",
        "organizations.team.read",
        "organizations.area.read",
        "process.definition.read",
    ];

    private static readonly string[] AdminServed =
    [
        "organizations.organization.read",
        "organizations.organization.write",
        "organizations.organization.deactivate",
        "organizations.membership.read",
        "organizations.membership.write",
        "organizations.project.deactivate",
        "organizations.team.read",
        "organizations.team.create",
        "organizations.team.delete",
        "organizations.area.read",
        "organizations.area.create",
        "process.definition.read",
    ];

    // BR-SA-11 · the answer holds the role and the codes of its bundle decided at the organization, in catalog order — for each role.
    [Theory]
    [InlineData(OrganizationRole.Admin, "admin")]
    [InlineData(OrganizationRole.Member, "member")]
    [InlineData(OrganizationRole.Viewer, "viewer")]
    public async Task Get_AMember_AnswersTheirRoleAndTheOrganizationScopeCodesOfTheBundle_InCatalogOrder(OrganizationRole role, string name)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, organization.Id, role);

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(organization.Id), token: caller.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(["permissions", "role"], answer.Body.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(name, answer.Body.GetProperty("role").GetString());
        Assert.Equal(
            PermissionCatalog.All
                .Where(definition => definition.Resource == ResourceScope.Organization && OrganizationRoleBundles.Holds(role, definition.Code))
                .Select(definition => definition.Code),
            Permissions(answer.Body));
    }

    // BR-SA-11 · the exact served set of each role, spelled out: no project, team or area code, no work-item or board code.
    [Theory]
    [InlineData(OrganizationRole.Admin)]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Get_EachRole_ServesExactlyItsOrganizationScopeSet(OrganizationRole role)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, organization.Id, role);

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(organization.Id), token: caller.Token);

        Assert.Equal(role == OrganizationRole.Admin ? AdminServed : OrganizationScopeReads, Permissions(answer.Body));
    }

    // US-4 · "it holds no instance code — those stay in the token": whatever the token carries, the answer lists none of the three.
    [Fact]
    public async Task Get_NeverListsAnInstanceCode_EvenForATokenCarryingThem()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var subject = MembershipTestData.NewUserId();
        await MembershipTestData.AddMemberAsync(factory, organization.Id, subject, OrganizationRole.Admin);
        var token = KeycloakTestRealm.CreateToken([.. PermissionCatalog.InstanceCodes], options => options.Subject = subject);

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(organization.Id), token: token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Empty(Permissions(answer.Body).Intersect(PermissionCatalog.InstanceCodes));
    }

    // api-contract § 1.1: "Cache-Control: no-store" — the answer is derived from the membership at the moment of the request.
    [Fact]
    public async Task Get_SaysNoStore()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Viewer);
        using var request = new HttpRequestMessage(HttpMethod.Get, Path(organization.Id));
        request.WithBearer(caller.Token);

        using var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore, "The access must not be cached.");
        Assert.Equal("viewer", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());
    }

    // US-4 · "for an organization the user is not a member of it answers 404 «Организация не найдена»": the panel reads it as
    // "no longer a member" and re-reads the organization list.
    [Fact]
    public async Task Get_ANonMember_Is404OrganizationNotFound()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(organization.Id), token: MembershipTestData.Outsider());

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.organization.not_found", answer.ErrorCode);
        Assert.Equal("Организация не найдена.", answer.Detail);
    }

    // BR-M1-6 / BR-M1-31 · the answer changes on the next call after a role change, a removal and a re-adding — same token.
    [Fact]
    public async Task Get_FollowsTheMembershipOnTheNextCall()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);

        var first = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(organization.Id), token: caller.Token);
        await MembershipTestData.RemoveMemberAsync(factory, organization.Id, caller.Id);
        var removed = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(organization.Id), token: caller.Token);
        await MembershipTestData.AddMemberAsync(factory, organization.Id, caller.Id, OrganizationRole.Viewer);
        var readded = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(organization.Id), token: caller.Token);

        Assert.Equal("member", first.Body.GetProperty("role").GetString());
        Assert.Equal(HttpStatusCode.NotFound, removed.Status);
        Assert.Equal("viewer", readded.Body.GetProperty("role").GetString());
        Assert.Equal(OrganizationScopeReads, Permissions(readded.Body));
    }

    // US-4 · "Instance codes still come from the token": a user with the create right and no membership still creates an
    // organization, and the served access of any organization is 404 for them (organization codes are never granted by the token).
    [Fact]
    public async Task InstanceCodesStillComeFromTheToken_OrganizationCodesDoNot()
    {
        var subject = OrganizationsTokens.NewSubject();
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var creator = OrganizationsTokens.WithCreateRight(subject);

        var created = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, "/api/organizations", new { name = OrganizationsTestData.UniqueName("Пример организации") }, creator);
        var otherAccess = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(organization.Id), token: creator);
        var ownAccess = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(created.Body.GetProperty("id").GetGuid()), token: creator);

        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal(HttpStatusCode.NotFound, otherAccess.Status);
        Assert.Equal(HttpStatusCode.OK, ownAccess.Status);
        Assert.Equal("admin", ownAccess.Body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Get_NoToken_Is401()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(organization.Id));

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
    }
}
