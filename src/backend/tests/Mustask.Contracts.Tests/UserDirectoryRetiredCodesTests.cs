using System.Reflection;
using Mustask.Domain.UserDirectory;

namespace Mustask.Contracts.Tests;

/// <summary>
/// tech-025 (ADR-0034): the seven codes of the retired push channel keep their catalog entries, marked deprecated, with the
/// texts they had: a retired id stays taken and is never reused (ADR-0005 rule 3). Their C# constants are gone. The codes
/// the sign-in upsert still uses are not deprecated.
/// </summary>
public sealed class UserDirectoryRetiredCodesTests
{
    [Theory]
    [InlineData("user_directory.too_many_entries")]
    [InlineData("user_directory.push_malformed")]
    [InlineData("user_directory.push_unauthorized")]
    [InlineData("user_directory.snapshot_not_found")]
    [InlineData("user_directory.snapshot_not_open")]
    [InlineData("user_directory.snapshot_conflict")]
    [InlineData("user_directory.snapshot_incomplete")]
    public void RetiredPushCode_StaysInTheCatalog_Deprecated_WithNoConstant(string code)
    {
        var found = CatalogFixture.Catalog.TryGet(code, out var entry);

        Assert.True(found, $"{code} left the error catalog: a retired code stays, marked deprecated.");
        Assert.True(entry!.Deprecated);
        Assert.False(string.IsNullOrWhiteSpace(entry.Ru));
        Assert.DoesNotContain(
            typeof(UserDirectoryErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static),
            field => field.IsLiteral && (string?)field.GetRawConstantValue() == code);
    }

    [Fact]
    public void EveryCodeTheSignInUpsertStillUses_IsLiveInTheCatalog()
    {
        var constants = typeof(UserDirectoryErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!);

        foreach (var code in constants)
        {
            Assert.True(CatalogFixture.Catalog.TryGet(code, out var entry), $"{code} is not in the catalog.");
            Assert.False(entry!.Deprecated, $"{code} is in use but marked deprecated.");
        }
    }
}
