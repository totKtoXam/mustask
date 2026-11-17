using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Domain.Tests.Identity;

// feat-041 · doc-016 BR-TD-12..-14, BR-TD-16: the sight rule as a pure function, and the new visibility code's bundles.
public sealed class SightRulesTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    public void AProject_IsSeenByAGrantAMembershipOfALinkedTeamOrTheFlag_AndOtherwiseNot(bool flag, bool grant, bool linkedTeam, bool expected) =>
        Assert.Equal(expected, SightRules.SeesProject(OrganizationRole.Member, flag, grant, linkedTeam));

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    public void ATeam_IsSeenByMembershipAGrantOnItOrItsDepartmentOrALinkedProjectSeen(bool member, bool grant, bool linkedProject, bool expected) =>
        Assert.Equal(expected, SightRules.SeesTeam(OrganizationRole.Viewer, member, grant, linkedProject));

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    public void ADepartment_IsSeenByAGrantOrAVisibleTeamOrProjectInside(bool grant, bool team, bool project, bool expected) =>
        Assert.Equal(expected, SightRules.SeesDepartment(OrganizationRole.Viewer, grant, team, project));

    [Fact]
    public void TheAdministrator_SeesEverything()
    {
        Assert.True(SightRules.SeesProject(OrganizationRole.Admin, false, false, false));
        Assert.True(SightRules.SeesTeam(OrganizationRole.Admin, false, false, false));
        Assert.True(SightRules.SeesDepartment(OrganizationRole.Admin, false, false, false));
    }

    // BR-TD-16 · sight adds no code: the flag, like a link, is not an input of the union.
    [Fact]
    public void TheVisibilityCode_IsHeldByTheProjectAndDepartmentManagersAndTheAdministratorOnly()
    {
        var code = PermissionCatalog.OrganizationsVisibilityWrite;

        Assert.Equal(ResourceScope.Project, PermissionCatalog.ResourceScopeOf(code));
        Assert.Contains(code, ScopedRoleBundles.CodesOf(ResourceScope.Project, GrantRole.Manager));
        Assert.Contains(code, ScopedRoleBundles.CodesOf(ResourceScope.Area, GrantRole.Manager));

        foreach (var (scope, role) in new[]
                 {
                     (ResourceScope.Project, GrantRole.Reader), (ResourceScope.Project, GrantRole.Member),
                     (ResourceScope.Area, GrantRole.Reader), (ResourceScope.Area, GrantRole.Member),
                     (ResourceScope.Team, GrantRole.Manager),
                 })
        {
            Assert.DoesNotContain(code, ScopedRoleBundles.CodesOf(scope, role));
        }
    }

    [Fact]
    public void TheFlag_IsOffAtCreation_AndSettingItReportsWhetherItChanged()
    {
        var project = Project.Create(Guid.CreateVersion7(), Mustask.Domain.Tests.Organizations.OrganizationsDomainTestData.Name("Проект"), "PRJ", DateTimeOffset.UnixEpoch, "u", "blue").Project;

        Assert.False(project.VisibleToOrganization);
        Assert.True(project.SetVisibleToOrganization(true));
        Assert.False(project.SetVisibleToOrganization(true));
        Assert.True(project.VisibleToOrganization);
    }
}
