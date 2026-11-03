using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Dictionaries;

/// <summary>
/// tech-024 (ADR-0033) · the dictionaries are guarded by instance codes that the API derives from the client roles in the token
/// (<c>resource_access.mustask-api.roles</c>, table <c>InstanceRoles</c>): <c>user</c> reads; <c>dictionaries-admin</c> and
/// <c>system-admin</c> also write; <c>organization-creator</c> adds nothing here; and without <c>user</c> a principal holds no right
/// at all, whatever else it has. The roles of the real realm import are the names used here (<c>KeycloakRealmRolesTests</c>
/// compares the import with the table). None of the codes opens an organization: membership decides the rest.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DictionariesByClientRoleTests(MustaskApiFactory factory)
{
    private const string Languages = "/api/dictionaries/languages";

    private async Task<(HttpStatusCode Read, HttpStatusCode Write)> ReadAndWriteAsync(string[] roles)
    {
        var token = KeycloakTestRealm.CreateToken(configure: options =>
        {
            options.Roles = roles;
            options.Subject = OrganizationsTokens.NewSubject();
        });

        var read = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Languages, token: token);
        var write = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, Languages, new { }, token);

        return (read.Status, write.Status);
    }

    // The roles a user can hold and whether the dictionaries read and write are open to them: the user reads; only the
    // dictionaries administrator and the system administrator write; a write by anyone else is refused.
    [Theory]
    [InlineData("user", true, false)]
    [InlineData("user,organization-creator", true, false)]
    [InlineData("user,dictionaries-admin", true, true)]
    [InlineData("user,system-admin", true, true)]
    [InlineData("user,organization-creator,dictionaries-admin,system-admin", true, true)]
    public async Task WithUser_EveryoneReadsTheLanguages_OnlyTheAdministratorsWrite(string roleList, bool mayRead, bool mayWrite)
    {
        var (read, write) = await ReadAndWriteAsync(roleList.Split(','));

        Assert.Equal(mayRead ? HttpStatusCode.OK : HttpStatusCode.Forbidden, read);

        if (mayWrite)
        {
            // Past the guard: the empty body is refused by the validator, not by authorization.
            Assert.NotEqual(HttpStatusCode.Forbidden, write);
            Assert.NotEqual(HttpStatusCode.Unauthorized, write);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Forbidden, write);
        }
    }

    // Without `user` nothing opens - the other roles alone, an unknown role, and a raw permission code as a role grant nothing.
    [Theory]
    [InlineData("organization-creator")]
    [InlineData("dictionaries-admin")]
    [InlineData("system-admin")]
    [InlineData("instance-admin")]
    [InlineData("dictionaries.languages.read,dictionaries.languages.write")]
    public async Task WithoutUser_NothingOpens(string roleList)
    {
        var (read, write) = await ReadAndWriteAsync(roleList.Split(','));

        Assert.Equal(HttpStatusCode.Forbidden, read);
        Assert.Equal(HttpStatusCode.Forbidden, write);
    }

    // US-4 · none of those codes opens an organization: a system administrator, with every code the table can give, and no
    // membership, gets 404 on an organization and an empty list of organizations - the codes are instance rights, membership decides
    // the rest.
    [Fact]
    public async Task TheSystemAdministratorsCodes_OpenNoOrganization()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var token = KeycloakTestRealm.CreateToken(
            configure: options =>
            {
                options.Roles = ["user", "system-admin"];
                options.Subject = OrganizationsTokens.NewSubject();
            });

        var opened = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, OrganizationsApi.Organization(organization.Id), token: token);
        var listed = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, "/api/organizations", token: token);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (opened.Status, opened.ErrorCode));
        Assert.Equal(HttpStatusCode.OK, listed.Status);
        Assert.Equal(0, listed.Body.GetArrayLength());
    }
}
