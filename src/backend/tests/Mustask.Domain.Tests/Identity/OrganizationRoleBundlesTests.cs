using Mustask.Domain.Identity;

namespace Mustask.Domain.Tests.Identity;

/// <summary>
/// mod-016 US-2/US-4 · the bundles are the spec's permission matrix, as data (BR-M1-30, status.md D-8): for each of the 23
/// organization codes and each of the three roles, the expected yes or no. A new organization code with no row here
/// fails <see cref="EveryOrganizationCode_HasARowInTheMatrix"/>: it must get its decision for every role.
/// </summary>
public sealed class OrganizationRoleBundlesTests
{
    /// <summary>The matrix of the spec: code, then admin, member, viewer.</summary>
    private static readonly (string Code, bool Admin, bool Member, bool Viewer)[] Matrix =
    [
        (PermissionCatalog.OrganizationsOrganizationRead, true, true, true),
        (PermissionCatalog.OrganizationsOrganizationWrite, true, false, false),
        (PermissionCatalog.OrganizationsOrganizationDeactivate, true, false, false),
        (PermissionCatalog.OrganizationsMembershipRead, true, true, true),
        (PermissionCatalog.OrganizationsMembershipWrite, true, false, false),
        (PermissionCatalog.OrganizationsProjectRead, true, true, true),
        (PermissionCatalog.OrganizationsProjectWrite, true, false, false),
        (PermissionCatalog.OrganizationsProjectLink, true, false, false),
        (PermissionCatalog.OrganizationsProjectDeactivate, true, false, false),
        (PermissionCatalog.OrganizationsTeamRead, true, true, true),
        (PermissionCatalog.OrganizationsTeamWrite, true, false, false),
        (PermissionCatalog.OrganizationsTeamCreate, true, false, false),
        (PermissionCatalog.OrganizationsTeamDelete, true, false, false),
        (PermissionCatalog.OrganizationsTeamLink, true, false, false),
        (PermissionCatalog.OrganizationsAreaRead, true, true, true),
        (PermissionCatalog.OrganizationsAreaWrite, true, false, false),
        (PermissionCatalog.ProcessDefinitionRead, true, true, true),
        (PermissionCatalog.WorkItemsWorkItemRead, true, true, true),
        (PermissionCatalog.WorkItemsWorkItemWrite, true, true, false),
        (PermissionCatalog.WorkItemsLinkWrite, true, true, false),
        (PermissionCatalog.WorkItemsWorkItemDelete, true, true, false),
        (PermissionCatalog.WorkItemsWorkItemPurge, true, false, false),
        (PermissionCatalog.BoardsBacklogReorder, true, true, false),
    ];

    public static TheoryData<string, OrganizationRole, bool> Cells()
    {
        var data = new TheoryData<string, OrganizationRole, bool>();

        foreach (var row in Matrix)
        {
            data.Add(row.Code, OrganizationRole.Admin, row.Admin);
            data.Add(row.Code, OrganizationRole.Member, row.Member);
            data.Add(row.Code, OrganizationRole.Viewer, row.Viewer);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void Holds_EachCodeAndRole_IsTheSpecsMatrixCell(string code, OrganizationRole role, bool expected)
    {
        Assert.Equal(expected, OrganizationRoleBundles.Holds(role, code));
        Assert.Equal(expected, OrganizationRoleBundles.CodesOf(role).Contains(code));
    }

    [Fact]
    public void EveryOrganizationCode_HasARowInTheMatrix()
    {
        Assert.Equal(
            PermissionCatalog.OrganizationCodes.Order(StringComparer.Ordinal),
            Matrix.Select(row => row.Code).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Admin_HoldsEveryOrganizationCode()
    {
        Assert.Equal(
            PermissionCatalog.OrganizationCodes.Order(StringComparer.Ordinal),
            OrganizationRoleBundles.CodesOf(OrganizationRole.Admin).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Viewer_HoldsOnlyReadCodes()
    {
        var viewer = OrganizationRoleBundles.CodesOf(OrganizationRole.Viewer);

        Assert.Equal(7, viewer.Count);
        Assert.All(viewer, code => Assert.EndsWith(".read", code, StringComparison.Ordinal));
    }

    [Fact]
    public void Member_IsTheViewerPlusTheFourNonAdminWrites()
    {
        var viewer = OrganizationRoleBundles.CodesOf(OrganizationRole.Viewer);
        var member = OrganizationRoleBundles.CodesOf(OrganizationRole.Member);

        Assert.True(viewer.IsSubsetOf(member));
        Assert.Equal(
            new[] { "boards.backlog.reorder", "workitems.link.write", "workitems.workitem.delete", "workitems.workitem.write" },
            member.Except(viewer).Order(StringComparer.Ordinal));
    }

    // ADR-0027 rule 1: an organization membership never grants an instance code.
    [Theory]
    [InlineData(OrganizationRole.Admin)]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public void NoBundle_HoldsAnInstanceCode(OrganizationRole role)
    {
        Assert.Empty(OrganizationRoleBundles.CodesOf(role).Intersect(PermissionCatalog.InstanceCodes));
        Assert.All(PermissionCatalog.InstanceCodes, code => Assert.False(OrganizationRoleBundles.Holds(role, code)));
    }

    // BR-M1-35: the deactivated lists derive from the roles whose bundle holds the *.deactivate code.
    [Fact]
    public void RolesHolding_DeactivateCode_IsAdminOnly()
    {
        Assert.Equal([OrganizationRole.Admin], OrganizationRoleBundles.RolesHolding(PermissionCatalog.OrganizationsOrganizationDeactivate));
        Assert.Equal(
            [OrganizationRole.Admin, OrganizationRole.Member, OrganizationRole.Viewer],
            OrganizationRoleBundles.RolesHolding(PermissionCatalog.OrganizationsOrganizationRead));
        Assert.Empty(OrganizationRoleBundles.RolesHolding(PermissionCatalog.DictionariesLanguagesRead));
    }

    [Theory]
    [InlineData("admin", true, OrganizationRole.Admin)]
    [InlineData("member", true, OrganizationRole.Member)]
    [InlineData("viewer", true, OrganizationRole.Viewer)]
    [InlineData("Admin", false, default(OrganizationRole))]
    [InlineData("owner", false, default(OrganizationRole))]
    [InlineData("", false, default(OrganizationRole))]
    [InlineData(" admin", false, default(OrganizationRole))]
    [InlineData(null, false, default(OrganizationRole))]
    public void TryParse_OnlyTheThreeExactSpellings_AreRoles(string? value, bool parsed, OrganizationRole expected)
    {
        Assert.Equal(parsed, OrganizationRoles.TryParse(value, out var role));
        Assert.Equal(expected, role);
    }

    [Fact]
    public void ToCode_RoundTripsThroughTryParse()
    {
        Assert.All(OrganizationRoles.All, role =>
        {
            Assert.True(OrganizationRoles.TryParse(OrganizationRoles.ToCode(role), out var parsed));
            Assert.Equal(role, parsed);
        });
    }
}
