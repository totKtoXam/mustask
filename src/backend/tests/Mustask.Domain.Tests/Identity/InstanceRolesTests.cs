using Mustask.Domain.Identity;

namespace Mustask.Domain.Tests.Identity;

/// <summary>tech-024 (ADR-0033) · the one table of the four client roles of <c>mustask-api</c> and the instance codes each holds.</summary>
public sealed class InstanceRolesTests
{
    [Fact]
    public void Roles_AreExactlyTheFour()
    {
        Assert.Equal(
            ["user", "organization-creator", "dictionaries-admin", "system-admin"],
            InstanceRoles.All.Select(role => role.Name));
    }

    [Theory]
    [InlineData("user", new[] { "dictionaries.languages.read" })]
    [InlineData("organization-creator", new[] { "organizations.organization.create" })]
    [InlineData("dictionaries-admin", new[] { "dictionaries.languages.read", "dictionaries.languages.write" })]
    public void Role_HoldsItsCodes(string roleName, string[] expected)
    {
        var role = InstanceRoles.All.Single(candidate => candidate.Name == roleName);

        Assert.Equal(expected.Order(StringComparer.Ordinal), role.Codes.Order(StringComparer.Ordinal));
    }

    // feat-033 BR-RS-1, ADR-0035 § 2: the rescue code is held by system-admin and by no other role.
    [Fact]
    public void RescueCode_IsHeldBySystemAdminOnly()
    {
        var holders = InstanceRoles.All
            .Where(role => role.Codes.Contains(PermissionCatalog.OrganizationsOrganizationRescue))
            .Select(role => role.Name);

        Assert.Equal([InstanceRoles.SystemAdmin], holders);
    }

    [Fact]
    public void SystemAdmin_HoldsEveryInstanceCode()
    {
        var role = InstanceRoles.All.Single(candidate => candidate.Name == InstanceRoles.SystemAdmin);

        Assert.True(role.Codes.SetEquals(PermissionCatalog.InstanceCodes));
    }

    [Fact]
    public void EveryInstanceCode_IsReachableByARole()
    {
        var reachable = InstanceRoles.All.SelectMany(role => role.Codes).ToHashSet(StringComparer.Ordinal);

        Assert.Empty(PermissionCatalog.InstanceCodes.Except(reachable));
    }

    [Fact]
    public void NoRole_HoldsAnOrganizationCode()
    {
        Assert.Empty(InstanceRoles.All.SelectMany(role => role.Codes).Intersect(PermissionCatalog.OrganizationCodes));
    }

    [Fact]
    public void EveryRole_HasADescription()
    {
        Assert.All(InstanceRoles.All, role => Assert.False(string.IsNullOrWhiteSpace(role.Description)));
    }

    [Fact]
    public void CodesOf_UnionOfTheKnownRoles()
    {
        Assert.Equal(
            ["dictionaries.languages.read", "organizations.organization.create"],
            InstanceRoles.CodesOf(["user", "organization-creator"]).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void CodesOf_UnknownRoleNames_AreIgnored()
    {
        Assert.Equal(
            ["dictionaries.languages.read"],
            InstanceRoles.CodesOf(["user", "instance-admin", "dictionaries.languages.write", "offline_access"]));
    }

    [Theory]
    [InlineData("organization-creator")]
    [InlineData("dictionaries-admin")]
    [InlineData("system-admin")]
    public void CodesOf_WithoutUser_IsEmptyWhateverElseIsThere(string role)
    {
        Assert.Empty(InstanceRoles.CodesOf([role]));
        Assert.Empty(InstanceRoles.CodesOf([]));
    }

    [Fact]
    public void IsKnown_OnlyTheFourNames()
    {
        Assert.True(InstanceRoles.IsKnown("system-admin"));
        Assert.False(InstanceRoles.IsKnown("instance-admin"));
        Assert.False(InstanceRoles.IsKnown("System-Admin"));
    }
}
