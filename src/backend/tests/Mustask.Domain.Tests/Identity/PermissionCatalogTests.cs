using Mustask.Domain.Identity;

namespace Mustask.Domain.Tests.Identity;

// Правило именования <модуль>.<ресурс>.<действие>, отсутствие дублей.
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

    [Fact]
    public void AllCodes_HaveNoDuplicates()
    {
        Assert.Equal(PermissionCatalog.AllCodes.Count, PermissionCatalog.AllCodes.Distinct().Count());
    }

    [Fact]
    public void AllCodes_EachHasDescription()
    {
        Assert.All(PermissionCatalog.AllCodes, code =>
            Assert.True(PermissionCatalog.Descriptions.ContainsKey(code)));
    }
}
