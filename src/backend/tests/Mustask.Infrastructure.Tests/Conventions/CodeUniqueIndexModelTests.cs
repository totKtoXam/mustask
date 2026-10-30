namespace Mustask.Infrastructure.Tests.Conventions;

/// <summary>
/// Model test over the real <see cref="MustaskDbContext"/> (docs/conventions/backend.md):
/// every entity type with a `Code` property has a unique constraint
/// covering exactly that property -- the check that the rule keeps holding for
/// dictionaries added later, without touching a Docker/Postgres connection
/// (building the model does not open one -- see <see cref="MustaskDbContextFactoryTests"/>).
/// </summary>
[Collection(ProcessEnvironmentCollection.Name)]
public sealed class CodeUniqueIndexModelTests
{
    private const string ConnectionStringEnvVar = "ConnectionStrings__mustask";

    [Fact]
    public void EveryEntityTypeWithACodeProperty_HasAUniqueConstraintCoveringExactlyIt()
    {
        Environment.SetEnvironmentVariable(ConnectionStringEnvVar, "Host=localhost;Database=mustask;Username=u;Password=p");

        try
        {
            using var context = new MustaskDbContextFactory().CreateDbContext([]);

            var violations = new List<string>();

            foreach (var entity in context.Model.GetEntityTypes())
            {
                var codeProperty = entity.FindProperty("Code");
                if (codeProperty is null)
                {
                    continue;
                }

                var primaryKey = entity.FindPrimaryKey();
                var isSingleColumnPrimaryKey = primaryKey is not null
                    && primaryKey.Properties.Count == 1
                    && primaryKey.Properties[0] == codeProperty;

                var hasCoveringUniqueIndex = entity.GetIndexes()
                    .Any(index => index.IsUnique && index.Properties.Count == 1 && index.Properties[0] == codeProperty);

                var hasCoveringAlternateKey = entity.GetKeys()
                    .Any(key => key.Properties.Count == 1 && key.Properties[0] == codeProperty);

                if (!isSingleColumnPrimaryKey && !hasCoveringUniqueIndex && !hasCoveringAlternateKey)
                {
                    violations.Add(entity.DisplayName());
                }
            }

            Assert.True(
                violations.Count == 0,
                $"Сущности с полем Code без покрывающего его уникального ограничения: {string.Join(", ", violations)}. " +
                "CodeUniqueIndexConvention должна была добавить индекс сама — см. docs/conventions/backend.md.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, null);
        }
    }
}
