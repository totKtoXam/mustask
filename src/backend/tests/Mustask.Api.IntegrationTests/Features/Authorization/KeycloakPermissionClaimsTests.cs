using System.Security.Claims;
using Mustask.Api.Authentication;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Authorization;

/// <summary>
/// tech-024 (ADR-0033) · <c>resource_access.mustask-api.roles</c> carries the four client-role names; the <c>perm</c> claims are
/// the codes the table <see cref="InstanceRoles"/> gives them. Unknown names are ignored; a principal without <c>user</c> gets none.
/// Pure function over a principal — no host.
/// </summary>
public sealed class KeycloakPermissionClaimsTests
{
    private const string Audience = "mustask-api";

    private static string[] PermsOf(string resourceAccessJson)
    {
        var identity = new ClaimsIdentity([new Claim("resource_access", resourceAccessJson)], "test");
        var principal = new ClaimsPrincipal(identity);

        KeycloakPermissionClaims.Append(principal, Audience);

        return principal.FindAll(KeycloakPermissionClaims.PermissionClaimType).Select(claim => claim.Value).Order(StringComparer.Ordinal).ToArray();
    }

    private static string Roles(params string[] roles) =>
        "{\"mustask-api\":{\"roles\":[" + string.Join(",", roles.Select(role => "\"" + role + "\"")) + "]}}";

    [Fact]
    public void User_GetsTheDictionariesRead()
    {
        Assert.Equal([PermissionCatalog.DictionariesLanguagesRead], PermsOf(Roles("user")));
    }

    [Fact]
    public void UserAndOrganizationCreator_GetBothCodes()
    {
        Assert.Equal(
            [PermissionCatalog.DictionariesLanguagesRead, PermissionCatalog.OrganizationsOrganizationCreate],
            PermsOf(Roles("user", "organization-creator")));
    }

    [Fact]
    public void UserAndDictionariesAdmin_GetReadAndWrite()
    {
        Assert.Equal(
            [PermissionCatalog.DictionariesLanguagesRead, PermissionCatalog.DictionariesLanguagesWrite],
            PermsOf(Roles("user", "dictionaries-admin")));
    }

    [Fact]
    public void UserAndSystemAdmin_GetEveryInstanceCode()
    {
        Assert.Equal(PermissionCatalog.InstanceCodes.Order(StringComparer.Ordinal), PermsOf(Roles("user", "system-admin")));
    }

    [Fact]
    public void UnknownRoleNames_AreIgnored()
    {
        // The old model's names, a raw permission code and a Keycloak built-in are not roles of ours.
        Assert.Equal(
            [PermissionCatalog.DictionariesLanguagesRead],
            PermsOf(Roles("user", "instance-admin", PermissionCatalog.DictionariesLanguagesWrite, "offline_access")));
    }

    [Theory]
    [InlineData("organization-creator")]
    [InlineData("dictionaries-admin")]
    [InlineData("system-admin")]
    public void WithoutUser_NoPermissionClaimsAtAll(string role)
    {
        Assert.Empty(PermsOf(Roles(role)));
    }

    [Fact]
    public void RawPermissionCodesAsRoles_GrantNothing()
    {
        Assert.Empty(PermsOf(Roles(PermissionCatalog.DictionariesLanguagesRead, PermissionCatalog.OrganizationsOrganizationCreate)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"other-api":{"roles":["user","system-admin"]}}""")]
    [InlineData("""{"mustask-api":{"roles":"user"}}""")]
    [InlineData("""{"mustask-api":{"roles":[1,null,"",{}]}}""")]
    [InlineData("[]")]
    public void ShapesWithoutOurRoles_GrantNothing(string resourceAccess)
    {
        Assert.Empty(PermsOf(resourceAccess));
    }

    [Fact]
    public void NoResourceAccessClaim_GrantsNothing()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([], "test"));

        KeycloakPermissionClaims.Append(principal, Audience);

        Assert.Empty(principal.FindAll(KeycloakPermissionClaims.PermissionClaimType));
    }
}
