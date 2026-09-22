using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Mustask.Infrastructure.Conventions;

/// <summary>
/// Configures optimistic concurrency via PostgreSQL's system column `xmin`
/// for every editable entity in the model -- the baseline concurrency rule
/// required by docs/conventions/backend.md. Applies automatically to any
/// entity added later; nothing to configure per entity.
///
/// Registered as an <see cref="IModelFinalizingConvention"/> from
/// <see cref="MustaskDbContext.ConfigureConventions"/>, alongside
/// <see cref="SnakeCaseNamingConvention"/> -- see that class's remarks for
/// why finalizing-convention time matters and why column names are read via
/// the <see cref="StoreObjectIdentifier"/>-scoped overloads. Order between
/// the two conventions does not affect correctness: `xmin` is always the
/// literal column name "xmin" (already snake_case), so it is unaffected by
/// whichever pass renames it.
///
/// `xmin` is mapped as a shadow property so EF Core's normal
/// SaveChanges/query pipeline picks it up (adds it to SELECT projections,
/// includes it in the UPDATE/DELETE WHERE clause) -- that part needs no
/// special casing, since Postgres exposes `xmin` on every row regardless of
/// whether it is declared in DDL. Migrations do not need special handling
/// either: `xmin` is a system column PostgreSQL reserves, and the
/// EF/Npgsql migrations pipeline already strips system columns from
/// generated DDL on its own.
///
/// Three categories of entity type are skipped:
/// - Any entity type with no table/view mapping (keyless read models on
///   <c>ToView</c>, <c>ToFunction</c>, <c>ToSqlQuery</c>): there is nothing
///   to write back to, a concurrency token is meaningless, and for a view
///   specifically PostgreSQL has no `xmin` of its own to select -- adding
///   one breaks every query against the view ("column ... xmin does not
///   exist"). This replaces the narrower `entity.IsKeyless` check, which
///   missed a *keyed* `ToView` entity type (same failure, no key involved).
/// - Non-root entity types of a TPH hierarchy (derived types mapped, by
///   default, onto the same table as their base type): the shadow property
///   only needs to -- and, since a property with the same name already
///   visible from the base type cannot be added again, only can -- be
///   declared once, on the root. Derived types see it automatically by
///   walking up the hierarchy (<c>FindProperty</c> does this on its own).
/// - Owned entity types that share their owner's table (the default for
///   <c>OwnsOne</c>/<c>OwnsMany</c> without an explicit <c>ToTable</c>):
///   the row already gets its one `xmin` from the owner; adding a second,
///   independent shadow property for the same physical column on the owned
///   type's own entity type is not just redundant but wrong -- it would be
///   tracked separately from the owner's, even though there is only one
///   underlying column. (An owned type mapped to its *own* table via an
///   explicit <c>ToTable</c> is a normal, independently persisted table and
///   does get its own token here.)
/// </summary>
internal sealed class OptimisticConcurrencyConvention : IModelFinalizingConvention
{
    /// <summary>
    /// Column name PostgreSQL reserves for the implicit system column used
    /// as the optimistic concurrency token.
    /// </summary>
    public const string XminColumnName = "xmin";

    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entity in modelBuilder.Metadata.GetEntityTypes())
        {
            var tableId = StoreObjectIdentifier.Create(entity, StoreObjectType.Table);
            if (tableId is null)
            {
                continue;
            }

            if (entity.BaseType is not null
                && StoreObjectIdentifier.Create(entity.BaseType, StoreObjectType.Table) == tableId)
            {
                continue;
            }

            var ownership = entity.FindOwnership();
            if (ownership is not null
                && StoreObjectIdentifier.Create(ownership.PrincipalEntityType, StoreObjectType.Table) == tableId)
            {
                continue;
            }

            var xmin = entity.FindProperty(XminColumnName)
                ?? entity.AddProperty(
                    XminColumnName,
                    typeof(uint),
                    setTypeConfigurationSource: true,
                    fromDataAnnotation: false)
                ?? throw new InvalidOperationException(
                    $"Could not add the '{XminColumnName}' shadow property to '{entity.DisplayName()}'.");

            xmin.SetIsConcurrencyToken(true, fromDataAnnotation: false);
            xmin.SetValueGenerated(ValueGenerated.OnAddOrUpdate, fromDataAnnotation: false);
            xmin.SetColumnName(XminColumnName, fromDataAnnotation: false);
            xmin.SetColumnType("xid", fromDataAnnotation: false);
        }
    }
}
