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
