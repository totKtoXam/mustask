using Mustask.Domain.UserDirectory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Mustask.Infrastructure.Tests.UserDirectory;

/// <summary>
/// mod-014 plan-backend.md § 4: the three tables of the user projection as the
/// model maps them — names, lengths, the string enums and the xmin token of
/// ADR-0010 — without a database (building the model opens no connection).
/// </summary>
public sealed class ProjectedUserModelTests : IDisposable
{
    private const string ConnectionStringEnvVar = "ConnectionStrings__mustask";

    private readonly MustaskDbContext _context;

    public ProjectedUserModelTests()
    {
        Environment.SetEnvironmentVariable(ConnectionStringEnvVar, "Host=localhost;Database=mustask;Username=u;Password=p");
        _context = new MustaskDbContextFactory().CreateDbContext([]);
    }

    public void Dispose()
    {
        _context.Dispose();
        Environment.SetEnvironmentVariable(ConnectionStringEnvVar, null);
    }

    [Theory]
    [InlineData(typeof(ProjectedUser), "directory_users")]
    [InlineData(typeof(DirectorySnapshot), "directory_snapshots")]
    [InlineData(typeof(DirectorySnapshotPage), "directory_snapshot_pages")]
    public void Entity_MapsToItsTable_WithAnXminToken(Type entityType, string table)
    {
        var entity = _context.Model.FindEntityType(entityType)!;

        Assert.Equal(table, entity.GetTableName());
        Assert.True(entity.FindProperty("xmin")?.IsConcurrencyToken, $"{table} has no xmin concurrency token.");
    }

    [Theory]
    [InlineData(nameof(ProjectedUser.Id), "id", 255)]
    [InlineData(nameof(ProjectedUser.Username), "username", 255)]
    [InlineData(nameof(ProjectedUser.FirstName), "first_name", 255)]
    [InlineData(nameof(ProjectedUser.LastName), "last_name", 255)]
    [InlineData(nameof(ProjectedUser.Email), "email", 255)]
    [InlineData(nameof(ProjectedUser.DisplayName), "display_name", 511)]
    [InlineData(nameof(ProjectedUser.Lifecycle), "lifecycle", 16)]
    [InlineData(nameof(ProjectedUser.SortKeyVersion), "sort_key_version", 64)]
    public void ProjectedUser_TextColumns_HaveTheirNamesAndLengths(string property, string column, int length)
    {
        var entity = _context.Model.FindEntityType(typeof(ProjectedUser))!;
        var mapped = entity.FindProperty(property)!;

        Assert.Equal(column, mapped.GetColumnName(StoreObjectIdentifier.Table("directory_users", null)));
        Assert.Equal(length, mapped.GetMaxLength());
        Assert.False(mapped.IsNullable);
    }

    [Fact]
    public void Lifecycle_And_State_AreStoredAsStrings()
    {
        var lifecycle = _context.Model.FindEntityType(typeof(ProjectedUser))!.FindProperty(nameof(ProjectedUser.Lifecycle))!;
        var state = _context.Model.FindEntityType(typeof(DirectorySnapshot))!.FindProperty(nameof(DirectorySnapshot.State))!;

        Assert.Equal(typeof(string), lifecycle.GetTypeMapping().Converter?.ProviderClrType);
        Assert.Equal(typeof(string), state.GetTypeMapping().Converter?.ProviderClrType);
    }

    [Fact]
    public void ProjectedUser_SortKey_IsBytea_AndIndexedWithIsActive()
    {
        var entity = _context.Model.FindEntityType(typeof(ProjectedUser))!;

        Assert.Equal("bytea", entity.FindProperty(nameof(ProjectedUser.SortKey))!.GetColumnType());

        var index = Assert.Single(entity.GetIndexes());
        Assert.Equal(
            [nameof(ProjectedUser.IsActive), nameof(ProjectedUser.SortKey)],
            index.Properties.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void SnapshotPage_CascadesWithItsSnapshot()
    {
        var page = _context.Model.FindEntityType(typeof(DirectorySnapshotPage))!;

        var foreignKey = Assert.Single(page.GetForeignKeys());
        Assert.Equal(typeof(DirectorySnapshot), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }
}
