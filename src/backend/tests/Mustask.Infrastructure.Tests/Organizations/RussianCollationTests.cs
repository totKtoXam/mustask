using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// plan-backend § 6.5 and risk R-3: display order is by name in ICU's Russian
/// collation, letters before letter case, «Ё» with «Е», then by id (design
/// § 4.6). Ordinal order would put «Ёлка» first and «альфа» last.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class RussianCollationTests(OrganizationsDatabaseFixture database)
{
    [Fact]
    public async Task TheTestImage_HasTheRussianIcuCollation()
    {
        await using var context = database.NewContext();

        var exists = await context.Database
            .SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM pg_collation WHERE collname = {OrganizationsCollation.Russian}) AS \"Value\"")
            .SingleAsync();

        Assert.True(exists, $"PostgreSQL has no collation {OrganizationsCollation.Russian}: the list reads would fail with 42704.");
    }

    // US-8 · "Lists in order": «альфа», «Бета», «Ёлка», «Ель», as a person reads them.
    [Fact]
    public async Task ProjectList_IsInRussianOrder_IgnoringLetterCase_WithYoBesideYe()
    {
        var structure = await database.CreateStructureAsync(projectName: "Ель", key: "YEL");
        var organizationId = structure.Organization.Id;
        await database.AddProjectAsync(organizationId, "Ёлка", "YOL");
        await database.AddProjectAsync(organizationId, "альфа", "ALF");
        await database.AddProjectAsync(organizationId, "Бета", "BET");

        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var projects = await scope.ServiceProvider.GetRequiredService<IProjectReadModel>()
            .ListAsync(organizationId, ProjectStatus.Active, CancellationToken.None);

        Assert.Equal(["альфа", "Бета", "Ёлка", "Ель"], projects.Select(project => project.Name));
    }
}
