using Mustask.Domain.Identity;

namespace Mustask.Domain.Tests.Identity;

// The naming rule <module>.<resource>.<action>, no duplicates, and the scope of every code (mod-016 BR-M1-30, ADR-0027).
public sealed class PermissionCatalogTests
{
    [Theory]
    [InlineData("tasks.items.read")]
    [InlineData("dictionaries.languages.write")]
    [InlineData("orders.requests.approve")]
    public void IsWellFormed_ValidCode_ReturnsTrue(string code)
    {
        Assert.True(PermissionCode.IsWellFormed(code));
    }

    [Theory]
    [InlineData("tasks.items")]
    [InlineData("Tasks.Items.Read")]
    [InlineData("tasks..read")]
    [InlineData("")]
    [InlineData("tasks.items.read.extra")]
    public void IsWellFormed_InvalidCode_ReturnsFalse(string code)
    {
        Assert.False(PermissionCode.IsWellFormed(code));
    }

    [Fact]
    public void AllCodes_AreWellFormed()
    {
        Assert.All(PermissionCatalog.AllCodes, code => Assert.True(PermissionCode.IsWellFormed(code)));
    }

    // mod-002 D-i: the create right is an instance-level code of the catalog, well formed and described.
    [Fact]
    public void OrganizationsOrganizationCreate_IsAWellFormedInstanceCodeOfTheCatalog()
    {
        Assert.True(PermissionCode.IsWellFormed(PermissionCatalog.OrganizationsOrganizationCreate));
        Assert.Contains(PermissionCatalog.OrganizationsOrganizationCreate, PermissionCatalog.AllCodes);
        Assert.Equal(PermissionScope.Instance, PermissionCatalog.ScopeOf(PermissionCatalog.OrganizationsOrganizationCreate));
        Assert.Equal("Создание организаций", PermissionCatalog.Descriptions[PermissionCatalog.OrganizationsOrganizationCreate]);
    }

    [Fact]
    public void AllCodes_HaveNoDuplicates()
    {
        Assert.Equal(PermissionCatalog.AllCodes.Count, PermissionCatalog.AllCodes.Distinct().Count());
    }

    [Fact]
    public void AllCodes_EachHasADescriptionAndAScope()
    {
        Assert.All(PermissionCatalog.All, definition =>
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Description));
            Assert.True(Enum.IsDefined(definition.Scope));
            Assert.Equal(definition.Description, PermissionCatalog.Descriptions[definition.Code]);
            Assert.Equal(definition.Scope, PermissionCatalog.ScopeOf(definition.Code));
        });
    }

    // ADR-0027 rule 1: exactly three instance codes live in the token; every other code is organization-scoped.
    [Fact]
    public void InstanceCodes_AreExactlyTheThreeTheTokenCarries()
    {
        Assert.Equal(
            new[]
            {
                "dictionaries.languages.read",
                "dictionaries.languages.write",
                "organizations.organization.create",
            },
            PermissionCatalog.InstanceCodes.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void OrganizationCodes_AreTheEighteenOfTheMatrix_AndNoCodeIsInBothScopes()
    {
        Assert.Equal(18, PermissionCatalog.OrganizationCodes.Count);
        Assert.Empty(PermissionCatalog.OrganizationCodes.Intersect(PermissionCatalog.InstanceCodes));
        Assert.Equal(
            PermissionCatalog.AllCodes.Order(StringComparer.Ordinal),
            PermissionCatalog.InstanceCodes.Concat(PermissionCatalog.OrganizationCodes).Order(StringComparer.Ordinal));
    }

    // mod-001 BR-M2-10, mod-003 BR-M3-27, mod-006 BR-M4-27, feat-001 BR-H-18: the codes mod-001..feat-001 declared are
    // organization-scoped codes of the one catalog, under their exact names.
    [Theory]
    [InlineData("process.definition.read", nameof(PermissionCatalog.ProcessDefinitionRead))]
    [InlineData("workitems.workitem.read", nameof(PermissionCatalog.WorkItemsWorkItemRead))]
    [InlineData("workitems.workitem.write", nameof(PermissionCatalog.WorkItemsWorkItemWrite))]
    [InlineData("boards.backlog.reorder", nameof(PermissionCatalog.BoardsBacklogReorder))]
    [InlineData("workitems.link.write", nameof(PermissionCatalog.WorkItemsLinkWrite))]
    [InlineData("workitems.workitem.delete", nameof(PermissionCatalog.WorkItemsWorkItemDelete))]
    public void FoldedCodes_AreRegisteredUnderTheirExactNames_AsOrganizationCodes(string code, string constantName)
    {
        var constant = (string)typeof(PermissionCatalog).GetField(constantName)!.GetRawConstantValue()!;

        Assert.Equal(code, constant);
        Assert.Equal(PermissionScope.Organization, PermissionCatalog.ScopeOf(code));
        Assert.Contains(code, PermissionCatalog.OrganizationCodes);
    }

    [Fact]
    public void ScopeOf_AnUnknownCode_Throws()
    {
        Assert.Throws<ArgumentException>(() => PermissionCatalog.ScopeOf("orders.requests.approve"));
        Assert.Throws<ArgumentException>(() => PermissionCatalog.ScopeOf(string.Empty));
    }
}
