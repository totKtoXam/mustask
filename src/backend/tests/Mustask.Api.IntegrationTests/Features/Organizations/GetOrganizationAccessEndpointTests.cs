using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/access</c> (mod-016 api-contract § 1.1; US-4 "Mustask.Api serves the role and the
/// codes of one organization"; BR-M1-31): the caller's role in the organization and exactly its bundle of organization codes, read
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

    // US-4 · the answer holds the role and EXACTLY the role's bundle of the spec's matrix, in ordinal order — for each role.
    [Theory]
    [InlineData(OrganizationRole.Admin, "admin")]
    [InlineData(OrganizationRole.Member, "member")]
    [InlineData(OrganizationRole.Viewer, "viewer")]
    public async Task Get_AMember_AnswersTheirRoleAndExactlyTheRolesBundle_Sorted(OrganizationRole role, string name)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, organization.Id, role);

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(organization.Id), token: caller.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(["permissions", "role"], answer.Body.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(name, answer.Body.GetProperty("role").GetString());
        Assert.Equal(OrganizationRoleBundles.CodesOf(role).Order(StringComparer.Ordinal), Permissions(answer.Body));
    }

    // US-4 · the spec's matrix, spelled out in the test too, so a change of a bundle fails here and in the domain's matrix test.
    [Fact]
    public async Task Get_TheMemberBundle_IsTheSevenReadsAndTheFourNonAdminWrites()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(organization.Id), token: caller.Token);

        Assert.Equal(
            [
                "boards.backlog.reorder",
                "organizations.area.read",
                "organizations.membership.read",
                "organizations.organization.read",
                "organizations.project.read",
                "organizations.team.read",
                "process.definition.read",
                "workitems.link.write",
                "workitems.workitem.delete",
                "workitems.workitem.read",
                "workitems.workitem.write",
            ],
            Permissions(answer.Body));
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
        Assert.Equal(7, Permissions(readded.Body).Count);
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
