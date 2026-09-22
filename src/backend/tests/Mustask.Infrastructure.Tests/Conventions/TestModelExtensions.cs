using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Mustask.Infrastructure.Tests.Conventions;

/// <summary>
/// Test-only helper for reading the column name EF Core will actually use in
/// generated DDL/SQL for a property, given its entity type's table (or view,
/// if it has no table).
///
/// Deliberately not the parameterless <c>IReadOnlyProperty.GetColumnName()</c>
/// overload: for a property that shares a table with another entity type
/// (owned-type table splitting, TPH), that overload returns the property's
/// own unmirrored default column name, not the name
/// <see cref="Mustask.Infrastructure.Conventions.SnakeCaseNamingConvention"/>
/// actually produced for that table -- see that convention's own remarks
/// for why this distinction exists and matters. Every column-name assertion in this
/// test project should go through this helper, not the parameterless
/// overload, or it risks asserting a value EF Core never actually uses.
/// </summary>
internal static class TestModelExtensions
{
    public static string? GetEffectiveColumnName(this IReadOnlyEntityType entity, string propertyName)
    {
        var property = entity.FindProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"No property '{propertyName}' on entity type '{entity.DisplayName()}'.");

        var storeObject = StoreObjectIdentifier.Create(entity, StoreObjectType.Table)
            ?? StoreObjectIdentifier.Create(entity, StoreObjectType.View);

        return storeObject is null ? null : property.GetColumnName(storeObject.Value);
    }
}
