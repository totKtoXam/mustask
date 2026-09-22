using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Tests.Conventions;

// docs/conventions/backend.md, "База данных": имена snake_case.
public sealed class SnakeCaseNamingConventionTests
{
    [Fact]
    public void ApplySnakeCaseNaming_TableAndColumnNames_ConvertedToSnakeCase()
    {
        using var context = ConventionsTestDbContext.Create();
        var entity = context.Model.FindEntityType(typeof(ParentTestEntity))!;

        Assert.Equal("parents", entity.GetTableName());
        Assert.Equal("id", entity.GetEffectiveColumnName(nameof(ParentTestEntity.Id)));
        Assert.Equal("display_name", entity.GetEffectiveColumnName(nameof(ParentTestEntity.DisplayName)));
    }

    [Fact]
    public void ApplySnakeCaseNaming_PrimaryKeyName_ConvertedToSnakeCase()
    {
        using var context = ConventionsTestDbContext.Create();
        var entity = context.Model.FindEntityType(typeof(ParentTestEntity))!;

        Assert.Equal("pk_parents", entity.FindPrimaryKey()!.GetName());
    }

    [Fact]
    public void ApplySnakeCaseNaming_ForeignKeyConstraintName_ConvertedToSnakeCase()
    {
        using var context = ConventionsTestDbContext.Create();
        var child = context.Model.FindEntityType(typeof(ChildTestEntity))!;

        var foreignKey = Assert.Single(child.GetForeignKeys());
        Assert.Equal("fk_children_parents_parent_test_entity_id", foreignKey.GetConstraintName());
    }

    [Fact]
    public void ApplySnakeCaseNaming_IndexName_ConvertedToSnakeCase()
    {
        using var context = ConventionsTestDbContext.Create();
        var parent = context.Model.FindEntityType(typeof(ParentTestEntity))!;

        var index = Assert.Single(parent.GetIndexes());
        Assert.Equal("ix_parents_display_name", index.GetDatabaseName());
    }
}
