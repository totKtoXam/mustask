using Mustask.Domain.Identity;

namespace Mustask.Domain.Tests.Identity;

/// <summary>
/// US-2 · "The read's code is registered, and nothing grants it yet" (BR-M2-10,
/// status.md D-8): the code is registered under its exact name, in a catalog of
/// its own that does not overlap the one the realm import must match.
/// </summary>
public sealed class OrganizationPermissionCatalogTests
{
    [Fact]
    public void ProcessDefinitionRead_IsRegisteredUnderItsExactName()
    {
        Assert.Equal("process.definition.read", OrganizationPermissionCatalog.ProcessDefinitionRead);
        Assert.Contains("process.definition.read", OrganizationPermissionCatalog.AllCodes);
    }

    // mod-003 BR-M3-27: the work item codes are declared under their exact names as organization-scoped codes, beside
    // mod-001's read, and so never reach the realm import (OrganizationPermissionRealmTests scans for every one of them).
    [Fact]
    public void WorkItemCodes_AreRegisteredUnderTheirExactNames()
    {
        Assert.Equal("workitems.workitem.read", OrganizationPermissionCatalog.WorkItemsWorkItemRead);
        Assert.Equal("workitems.workitem.write", OrganizationPermissionCatalog.WorkItemsWorkItemWrite);
        Assert.Contains("workitems.workitem.read", OrganizationPermissionCatalog.AllCodes);
        Assert.Contains("workitems.workitem.write", OrganizationPermissionCatalog.AllCodes);
    }

    // mod-006 BR-M4-27: the reorder's code is registered under its exact name as an organization-scoped code, outside
    // the realm import (OrganizationPermissionRealmTests scans every code of AllCodes).
    [Fact]
    public void BoardsBacklogReorder_IsRegisteredUnderItsExactName()
    {
        Assert.Equal("boards.backlog.reorder", OrganizationPermissionCatalog.BoardsBacklogReorder);
        Assert.Contains("boards.backlog.reorder", OrganizationPermissionCatalog.AllCodes);
    }

    // feat-001 BR-H-18: the parent write's code is registered under its exact name as an organization-scoped code,
    // outside the realm import (OrganizationPermissionRealmTests scans every code of AllCodes).
    [Fact]
    public void WorkItemsLinkWrite_IsRegisteredUnderItsExactName()
    {
        Assert.Equal("workitems.link.write", OrganizationPermissionCatalog.WorkItemsLinkWrite);
        Assert.Contains("workitems.link.write", OrganizationPermissionCatalog.AllCodes);
    }

    [Fact]
    public void AllCodes_AreWellFormed()
    {
        Assert.All(OrganizationPermissionCatalog.AllCodes, code => Assert.True(PermissionCode.IsWellFormed(code)));
    }

    [Fact]
    public void AllCodes_EachHasDescription()
    {
        Assert.All(OrganizationPermissionCatalog.AllCodes, code =>
            Assert.False(string.IsNullOrWhiteSpace(OrganizationPermissionCatalog.Descriptions[code])));
    }

    [Fact]
    public void AllCodes_HaveNoDuplicates()
    {
        Assert.Equal(OrganizationPermissionCatalog.AllCodes.Count, OrganizationPermissionCatalog.AllCodes.Distinct().Count());
    }

    [Fact]
    public void AllCodes_DoNotIntersectPermissionCatalog()
    {
        Assert.Empty(OrganizationPermissionCatalog.AllCodes.Intersect(PermissionCatalog.AllCodes));
    }
}
