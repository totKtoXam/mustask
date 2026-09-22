using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Mustask.Infrastructure.Conventions;

/// <summary>
/// Renames tables, views, columns, primary/alternate keys, foreign keys, and
/// indexes to snake_case, per the naming convention required for PostgreSQL
/// identifiers (docs/conventions/backend.md, "База данных").
///
/// Registered as an <see cref="IModelFinalizingConvention"/> from
/// <see cref="MustaskDbContext.ConfigureConventions"/> via
/// <c>configurationBuilder.Conventions.Add(...)</c>, which places it after
/// every one of EF Core's own model-finalizing conventions -- including
/// <c>SharedTableConvention</c>, the built-in convention that resolves the
/// final column names for table-split owned types (mirroring a dependent's
/// shared key onto its principal's column, and accumulating the
/// <c>&lt;Navigation&gt;_&lt;Property&gt;</c> prefix for nested owned types,
/// one level per navigation). By the time this convention runs, every name
/// it reads is already the one EF Core intends to use -- for ordinary
/// properties the job is strictly "rewrite this already-correct name to
/// snake_case", with no need to reproduce any of EF's own naming logic by
/// hand. This replaces a ~100-line topological, ownership-graph-aware
/// renamer that tried to reproduce that logic itself and kept falling
/// behind it (owned types with two navigations sharing a table, nested
/// owned types).
///
/// Column names specifically are read/written through the
/// <see cref="StoreObjectIdentifier"/>-scoped overloads
/// (<c>GetColumnName(in StoreObjectIdentifier)</c> /
/// <c>SetColumnName(string, in StoreObjectIdentifier, bool)</c>), never the
/// parameterless ones. This is not a style choice: for a property that
/// shares a table with another entity type (owned-type table splitting, or
/// a TPH-derived type sharing its base type's table), the parameterless
/// <c>GetColumnName()</c> returns the property's own unmirrored default --
/// e.g. "TwoOwnedOwnerId" for an owned type's shared-key shadow property,
/// even once EF has already resolved the *physical* column for that table
/// to be "Id" (mirrored from the principal). The StoreObjectIdentifier
/// overload is table/view-aware and reports the name EF Core will actually
/// generate in DDL/SQL for that specific store object.
///
/// An owned type's shared key/FK property (table splitting) needs no special
/// handling here: by the time this convention runs, EF Core's own
/// <c>SharedTableConvention</c> has already mirrored that property's
/// StoreObjectIdentifier-scoped column name onto its principal's column
/// (verified directly -- with none of this project's own conventions
/// registered, the dependent's shared-key shadow property already reports
/// its principal's column name through the store-scoped overload). Both
/// sides therefore read the identical starting name and snake_case it
/// identically, regardless of which entity type this convention happens to
/// visit first. Verified against two owned navigations sharing a table,
/// nested owned types on two navigations, a TPH hierarchy, a keyed
/// <c>ToView</c>, an explicit <c>HasColumnName</c> on the owner's key, and an implicit many-to-many join table; see the
/// tests alongside this class.
///
/// Table/view names are renamed in a first pass over every entity type,
/// before any column renaming starts (the second pass): <c>SetColumnName</c>
/// associates a column name with a specific <see cref="StoreObjectIdentifier"/>
/// (which embeds the table/view name), so renaming a table after its
/// columns had already been (re)named against it would orphan those column
/// names under the old identifier.
/// </summary>
internal sealed class SnakeCaseNamingConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        var entities = modelBuilder.Metadata.GetEntityTypes().ToList();

        foreach (var entity in entities)
        {
            RenameTableAndView(entity);
        }

        foreach (var entity in entities)
        {
            RenameColumns(entity);
            RenameKeysForeignKeysAndIndexes(entity);
        }
    }

    private static void RenameTableAndView(IConventionEntityType entity)
    {
        var tableName = entity.GetTableName();
        if (tableName is not null)
        {
            entity.SetTableName(tableName.ToSnakeCase(), fromDataAnnotation: false);
        }

        var viewName = entity.GetViewName();
        if (viewName is not null)
        {
            entity.SetViewName(viewName.ToSnakeCase(), fromDataAnnotation: false);
        }
    }

    private static void RenameColumns(IConventionEntityType entity)
    {
        // A property's *effective* column name for a given table/view can
        // only be read/written reliably through the StoreObjectIdentifier
        // overloads -- see the class remarks above.
        var storeObject = StoreObjectIdentifier.Create(entity, StoreObjectType.Table)
            ?? StoreObjectIdentifier.Create(entity, StoreObjectType.View);

        if (storeObject is null)
        {
            // No table/view mapping (e.g. ToFunction/ToSqlQuery, or a
            // keyless type mapped to neither) -- nothing to rename.
            return;
        }

        var so = storeObject.Value;
        foreach (var property in entity.GetProperties())
        {
            var sourceName = property.GetColumnName(so);
            if (sourceName is not null)
            {
                property.SetColumnName(sourceName.ToSnakeCase(), so, fromDataAnnotation: false);
            }
        }
    }

    private static void RenameKeysForeignKeysAndIndexes(IConventionEntityType entity)
    {
        foreach (var key in entity.GetKeys())
        {
            var keyName = key.GetName();
            if (keyName is not null)
            {
                key.SetName(keyName.ToSnakeCase(), fromDataAnnotation: false);
            }
        }

        foreach (var foreignKey in entity.GetForeignKeys())
        {
            var constraintName = foreignKey.GetConstraintName();
            if (constraintName is not null)
            {
                foreignKey.SetConstraintName(constraintName.ToSnakeCase(), fromDataAnnotation: false);
            }
        }

        foreach (var index in entity.GetIndexes())
        {
            var indexName = index.GetDatabaseName();
            if (indexName is not null)
            {
                index.SetDatabaseName(indexName.ToSnakeCase(), fromDataAnnotation: false);
            }
        }
    }
}
