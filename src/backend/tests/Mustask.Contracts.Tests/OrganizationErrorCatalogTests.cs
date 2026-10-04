using Mustask.Api.ErrorHandling.Catalog;
using Mustask.Domain.Organizations;

namespace Mustask.Contracts.Tests;

/// <summary>
/// US-2 (mod-001-process-seed) · "The not-found code is registered with its
/// texts" (BR-M2-11, status.md D-7): the code exists in the organizations
/// module's catalog, with Russian and Kazakh texts, and the generated reference
/// lists it.
/// </summary>
public sealed class OrganizationErrorCatalogTests
{
    [Fact]
    public void OrganizationNotFound_IsInTheOrganizationsCatalogWithRuAndKk()
    {
        Assert.Equal("organizations.organization.not_found", OrganizationErrorCodes.OrganizationNotFound);

        var found = CatalogFixture.Catalog.TryGet(OrganizationErrorCodes.OrganizationNotFound, out var entry);

        Assert.True(found, "organizations.organization.not_found is not in the error catalog.");
        Assert.Equal("organizations", entry!.Module);
        Assert.Equal(ErrorCatalogOrigin.Slice, entry.Origin);
        Assert.Equal(new[] { 404 }, entry.Statuses);
        Assert.Empty(entry.Params);
        Assert.False(entry.Deprecated);
        Assert.Equal("Организация не найдена.", entry.Ru);
        Assert.False(string.IsNullOrWhiteSpace(entry.Kk));
        Assert.NotEqual(entry.Ru, entry.Kk);
    }

    [Fact]
    public void OrganizationNotFound_IsListedInTheGeneratedErrorCodeReference()
    {
        var reference = File.ReadAllText(Path.Combine(RepositoryLayout.Root.FullName, "docs", "error-codes.md"));

        var row = reference.Split('\n').SingleOrDefault(line => line.StartsWith("|organizations.organization.not_found|", StringComparison.Ordinal));

        Assert.NotNull(row);
        Assert.Contains("|404|", row, StringComparison.Ordinal);
        Assert.Contains("Организация не найдена.", row, StringComparison.Ordinal);
    }
}
