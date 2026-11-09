using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Domain.Tests.Identity;

// mod-019 BR-SA-8 / ADR-0029 § 6 as amended by ADR-0038: the effective permissions are an additive union filtered by the target's scope.
public sealed class EffectivePermissionsTests
{
    private static readonly GrantRole[] Roles = [GrantRole.Reader, GrantRole.Member, GrantRole.Manager];

    [Fact]
    public void ViewerWithNoGrant_HoldsTheViewersBundleOnly()
    {
        Assert.True(EffectivePermissions.Holds(OrganizationRole.Viewer, ResourceScope.Project, [], PermissionCatalog.OrganizationsProjectRead));
        Assert.False(EffectivePermissions.Holds(OrganizationRole.Viewer, ResourceScope.Project, [], PermissionCatalog.OrganizationsProjectWrite));
    }

    [Fact]
    public void ProjectManagerGrant_AddsProjectWriteLinkAndGrant_OnTheProject()
    {
        var reaching = new[] { new ReachingGrant(ResourceScope.Project, GrantRole.Manager) };

        Assert.True(EffectivePermissions.Holds(OrganizationRole.Viewer, ResourceScope.Project, reaching, PermissionCatalog.OrganizationsProjectWrite));
        Assert.True(EffectivePermissions.Holds(OrganizationRole.Viewer, ResourceScope.Project, reaching, PermissionCatalog.OrganizationsProjectLink));
        Assert.True(EffectivePermissions.Holds(OrganizationRole.Viewer, ResourceScope.Project, reaching, PermissionCatalog.OrganizationsProjectGrant));
    }

    [Fact]
    public void ProjectManagerGrant_OnItsSubArea_HoldsOnlyTheAreaCode()
    {
        var reaching = new[] { new ReachingGrant(ResourceScope.Project, GrantRole.Manager) };

        Assert.True(EffectivePermissions.Holds(OrganizationRole.Member, ResourceScope.Area, reaching, PermissionCatalog.OrganizationsAreaWrite));
        Assert.False(EffectivePermissions.Holds(OrganizationRole.Member, ResourceScope.Area, reaching, PermissionCatalog.OrganizationsProjectWrite));
        Assert.False(EffectivePermissions.Holds(OrganizationRole.Member, ResourceScope.Area, reaching, PermissionCatalog.OrganizationsAreaGrant));
    }

    [Fact]
    public void TeamReaderGrant_AddsNothing()
    {
        var reaching = new[] { new ReachingGrant(ResourceScope.Team, GrantRole.Reader) };

        Assert.Empty(ScopedRoleBundles.CodesOf(ResourceScope.Team, GrantRole.Reader));
        Assert.False(EffectivePermissions.Holds(OrganizationRole.Viewer, ResourceScope.Team, reaching, PermissionCatalog.OrganizationsTeamGrant));
    }

    [Fact]
    public void TheCodesAreTheSameInAnyOrderAndWithAGrantRepeated()
    {
        ReachingGrant[] forward = [new(ResourceScope.Area, GrantRole.Reader), new(ResourceScope.Project, GrantRole.Manager)];
        ReachingGrant[] reversed = [forward[1], forward[0], forward[0]];

        Assert.Equal(
            EffectivePermissions.CodesOn(OrganizationRole.Viewer, ResourceScope.Project, forward).Order(),
            EffectivePermissions.CodesOn(OrganizationRole.Viewer, ResourceScope.Project, reversed).Order());
    }

    [Theory]
    [InlineData(ResourceScope.Area)]
    [InlineData(ResourceScope.Project)]
    [InlineData(ResourceScope.Team)]
    public void AGrant_NeverRemovesWhatTheOrganizationRoleHolds(ResourceScope grantScope)
    {
        foreach (var role in Roles)
        {
            foreach (var orgRole in Enum.GetValues<OrganizationRole>())
            {
                foreach (var target in new[] { ResourceScope.Area, ResourceScope.Project, ResourceScope.Team, ResourceScope.Organization })
                {
                    var without = EffectivePermissions.CodesOn(orgRole, target, []);
                    var with = EffectivePermissions.CodesOn(orgRole, target, [new ReachingGrant(grantScope, role)]);

                    Assert.Subset(with.ToHashSet(), without.ToHashSet());
                }
            }
        }
    }

    [Fact]
    public void Admin_HoldsEveryCode_OnEveryTarget()
    {
        foreach (var target in new[] { ResourceScope.Area, ResourceScope.Project, ResourceScope.Team })
        {
            var expected = PermissionCatalog.OrganizationCodes.Where(code => PermissionCatalog.ResourceScopeOf(code) == target);

            Assert.Equal(expected.Order(), EffectivePermissions.CodesOn(OrganizationRole.Admin, target, []).Order());
        }
    }

    [Fact]
    public void EveryBundleCode_IsAnOrganizationCodeOfTheCatalog()
    {
        foreach (var scope in ScopedRoleBundles.GrantScopes)
        {
            foreach (var role in Roles)
            {
                Assert.All(ScopedRoleBundles.CodesOf(scope, role), code => Assert.Contains(code, PermissionCatalog.OrganizationCodes));
            }
        }
    }

    [Fact]
    public void ProjectManager_DoesNotHoldWhatADepartmentManagerHolds_OnDepartments()
    {
        Assert.Contains(PermissionCatalog.OrganizationsAreaGrant, ScopedRoleBundles.CodesOf(ResourceScope.Area, GrantRole.Manager));
        Assert.DoesNotContain(PermissionCatalog.OrganizationsAreaGrant, ScopedRoleBundles.CodesOf(ResourceScope.Project, GrantRole.Manager));
    }

    [Fact]
    public void ASelfGrant_IsRecognisedByUserId()
    {
        Assert.True(GrantRules.IsSelfGrant("u-1", "u-1"));
        Assert.False(GrantRules.IsSelfGrant("u-1", "U-1"));
    }

    [Fact]
    public void GrantEntities_RecordTheGiver_AndReplaceKeepsTheKey()
    {
        var organization = Guid.CreateVersion7();
        var project = Guid.CreateVersion7();
        var at = DateTimeOffset.UtcNow;
        var grant = ProjectGrant.Give(organization, project, "u-1", GrantRole.Reader, "u-2", at);

        grant.Replace(GrantRole.Manager, "u-3", at.AddMinutes(1));

        Assert.Equal(("u-1", GrantRole.Manager, "u-3"), (grant.UserId, grant.Role, grant.GrantedBy));
        Assert.Equal(project, grant.ObjectId);
        Assert.Equal(ResourceScope.Project, grant.Scope);
    }

    [Theory]
    [InlineData("reader", GrantRole.Reader)]
    [InlineData("member", GrantRole.Member)]
    [InlineData("manager", GrantRole.Manager)]
    public void TheRoleCodes_RoundTrip(string code, GrantRole role)
    {
        Assert.Equal(code, GrantRoles.ToCode(role));
    }
}
