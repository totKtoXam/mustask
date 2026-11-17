using Microsoft.EntityFrameworkCore;
using Mustask.Domain.Images;

namespace Mustask.Infrastructure.Tests.Conventions;

/// <summary>
/// ADR-0045 decision 1 (the Images module owns the schema <c>images</c>): every entity mapped to that schema lives in
/// <c>Mustask.Domain.Images</c>, and every entity of that namespace maps to it. Model only, no Docker.
/// </summary>
[Collection(ProcessEnvironmentCollection.Name)]
public sealed class ImagesSchemaOwnershipTests
{
    private const string ConnectionStringEnvVar = "ConnectionStrings__mustask";

    [Fact]
    public void EntitiesOfTheImagesSchema_AreTheEntitiesOfTheImagesNamespace_AndTheSchemaHasBoth()
    {
        Environment.SetEnvironmentVariable(ConnectionStringEnvVar, "Host=localhost;Database=mustask;Username=u;Password=p");

        try
        {
            using var context = new MustaskDbContextFactory().CreateDbContext([]);
            var entities = context.Model.GetEntityTypes().ToList();

            var inSchema = entities.Where(entity => entity.GetSchema() == "images").Select(entity => entity.ClrType).ToList();
            var inNamespace = entities.Where(entity => entity.ClrType.Namespace == typeof(Image).Namespace).Select(entity => entity.ClrType).ToList();

            Assert.Equal([typeof(Image), typeof(ImageContent)], inSchema.OrderBy(type => type.Name));
            Assert.Equal(inSchema.OrderBy(type => type.Name), inNamespace.OrderBy(type => type.Name));
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, null);
        }
    }
}
