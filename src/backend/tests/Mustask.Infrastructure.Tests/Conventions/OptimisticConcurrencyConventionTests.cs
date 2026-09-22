using Mustask.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Mustask.Infrastructure.Tests.Conventions;

// docs/conventions/backend.md, "База данных": optimistic concurrency (xmin)
// для редактируемых сущностей — базовое правило, применяется автоматически,
// без конфигурации на каждой сущности.
public sealed class OptimisticConcurrencyConventionTests
{
    [Theory]
    [InlineData(typeof(ParentTestEntity))]
    [InlineData(typeof(ChildTestEntity))]
    public void ApplyOptimisticConcurrency_AnyEntity_AddsXminAsConcurrencyToken(Type entityClrType)
    {
        using var context = ConventionsTestDbContext.Create();
        var entity = context.Model.FindEntityType(entityClrType)!;

        var xmin = entity.FindProperty(OptimisticConcurrencyConvention.XminColumnName);

        Assert.NotNull(xmin);
        Assert.True(xmin.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, xmin.ValueGenerated);
        Assert.Equal("xmin", xmin.GetColumnName());
        Assert.Equal("xid", xmin.GetColumnType());
        Assert.Equal(typeof(uint), xmin.ClrType);
    }
}
