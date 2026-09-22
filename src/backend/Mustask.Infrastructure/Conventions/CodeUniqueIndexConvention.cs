using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Mustask.Infrastructure.Conventions;

/// <summary>
/// A business code is unique by convention (`docs/conventions/backend.md`,
/// "База данных"): an entity
/// type mapped to a table with an ordinary <c>Code</c> property gets a
/// unique index on it automatically, the same way
/// <see cref="OptimisticConcurrencyConvention"/> adds `xmin` — nothing is
/// configured per entity and no one has to remember it.
///
/// Skipped, so the convention is a no-op against today's model and stays one
/// against any future entity that already handles its own `Code` uniqueness:
/// - no table/view mapping (keyless <c>ToView</c>/<c>ToFunction</c> types) —
///   same reasoning as <see cref="OptimisticConcurrencyConvention"/>: there
///   is no row to constrain;
/// - <c>Code</c> is already the entity's single-column primary key
///   (<c>Permission.Code</c>) — a primary key is already unique;
/// - <c>Code</c> is already covered by an explicit unique index or an
///   alternate key of exactly that one property (<c>Role.Code</c>) — adding
///   a second index would be a silent duplicate, not idempotent re-entry.
/// </summary>
internal sealed class CodeUniqueIndexConvention : IModelFinalizingConvention
{
    private const string CodePropertyName = "Code";

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

            var codeProperty = entity.FindProperty(CodePropertyName);
            if (codeProperty is null)
            {
                continue;
            }

            if (IsSingleColumnPrimaryKey(entity, codeProperty) || IsAlreadyUniquelyConstrained(entity, codeProperty))
            {
                continue;
            }

            entity.Builder.HasIndex(new[] { codeProperty }, fromDataAnnotation: false)?
                .IsUnique(true, fromDataAnnotation: false);
        }
    }

    private static bool IsSingleColumnPrimaryKey(IConventionEntityType entity, IConventionProperty codeProperty)
    {
        var primaryKey = entity.FindPrimaryKey();
        return primaryKey is not null && primaryKey.Properties.Count == 1 && primaryKey.Properties[0] == codeProperty;
    }

    private static bool IsAlreadyUniquelyConstrained(IConventionEntityType entity, IConventionProperty codeProperty)
    {
        var singleColumnCoveringCode = new List<IConventionProperty> { codeProperty };

        return entity.GetIndexes().Any(index => index.IsUnique && index.Properties.SequenceEqual(singleColumnCoveringCode))
            || entity.GetKeys().Any(key => key.Properties.SequenceEqual(singleColumnCoveringCode));
    }
}
