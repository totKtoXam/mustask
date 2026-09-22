using Mustask.Application.Abstractions;
using Mustask.Domain.Dictionaries;
using Mustask.Infrastructure;
using Mustask.Infrastructure.Dictionaries.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Dictionaries;

/// <summary>
/// ADR-0021 §5: <see cref="MustaskApiFactory"/> already runs
/// <see cref="DictionarySeedHostedService"/> once at host startup — these
/// tests re-run <see cref="DictionarySeeder"/> directly against the
/// already-seeded database.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DictionarySeederTests
{
    private readonly MustaskApiFactory _factory;

    public DictionarySeederTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    private static DictionarySeeder CreateSeeder(MustaskDbContext db, IClock clock) =>
        new(db, clock);

    /// <summary>
    /// This class shares its database with the whole
    /// `MustaskApiCollection`, and several other classes in it create helper
    /// languages (per-test `zz-*`/`dp-*`) that may be present while this
    /// class runs (`GetLanguagesEndpointTests`'s own comment explains why)
    /// -- an exact count over the whole table is fragile
    /// against class execution order for that reason alone. Asserts only
    /// that the three seeded rows are present and unchanged, the same
    /// discipline `GetLanguagesEndpointTests` already uses.
    /// </summary>
    [Fact]
    public async Task HostStartup_SeedsThreeLanguages_RuDefaultActive()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        var languages = await db.Languages.AsNoTracking().ToListAsync();

        var ru = Assert.Single(languages, l => l.Code == "ru");
        Assert.True(ru.IsDefault);
        Assert.True(ru.IsActive);
        Assert.Equal("Русский", ru.Endonym);
        Assert.Contains(languages, l => l.Code == "kk" && l.IsActive && !l.IsDefault);
        Assert.Contains(languages, l => l.Code == "en" && l.IsActive && !l.IsDefault);
    }

    [Fact]
    public async Task SeedAsync_RunAgainstAlreadySeededDatabase_DoesNotDuplicateRows()
    {
        using var beforeScope = _factory.Services.CreateScope();
        var beforeDb = beforeScope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        var languageCountBefore = await beforeDb.Languages.CountAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var seeder = CreateSeeder(db, clock);

        await seeder.SeedAsync(CancellationToken.None);

        using var afterScope = _factory.Services.CreateScope();
        var afterDb = afterScope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        Assert.Equal(languageCountBefore, await afterDb.Languages.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_AdministratorEditedEndonym_IsNotOverwrittenByReseed()
    {
        using (var setupScope = _factory.Services.CreateScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<MustaskDbContext>();
            var ru = await setupDb.Languages.SingleAsync(l => l.Code == "ru");
            ru.Replace("Русский (правка администратора)", ru.SortOrder, isActive: true, DateTimeOffset.UtcNow, "admin");
            await setupDb.SaveChangesAsync();
        }

        try
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var seeder = CreateSeeder(db, clock);

            await seeder.SeedAsync(CancellationToken.None);

            using var afterScope = _factory.Services.CreateScope();
            var afterDb = afterScope.ServiceProvider.GetRequiredService<MustaskDbContext>();
            var ru = await afterDb.Languages.AsNoTracking().SingleAsync(l => l.Code == "ru");
            Assert.Equal("Русский (правка администратора)", ru.Endonym);
        }
        finally
        {
            using var restoreScope = _factory.Services.CreateScope();
            var restoreDb = restoreScope.ServiceProvider.GetRequiredService<MustaskDbContext>();
            var ru = await restoreDb.Languages.SingleAsync(l => l.Code == "ru");
            ru.Replace("Русский", ru.SortOrder, isActive: true, DateTimeOffset.UtcNow, "admin");
            await restoreDb.SaveChangesAsync();
        }
    }

    /// <summary>
    /// "Inserts what is missing, never updates what exists" also covers the
    /// activity flag: a seeded language an administrator has deactivated
    /// stays inactive after a reseed -- the seed file's `isActive` applies
    /// only to a row it creates.
    /// </summary>
    [Fact]
    public async Task SeedAsync_AdministratorDeactivatedLanguage_IsNotReactivatedByReseed()
    {
        using (var setupScope = _factory.Services.CreateScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<MustaskDbContext>();
            var en = await setupDb.Languages.SingleAsync(l => l.Code == "en");
            en.Replace(en.Endonym, en.SortOrder, isActive: false, DateTimeOffset.UtcNow, "admin");
            await setupDb.SaveChangesAsync();
        }

        try
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var seeder = CreateSeeder(db, clock);

            await seeder.SeedAsync(CancellationToken.None);

            using var afterScope = _factory.Services.CreateScope();
            var afterDb = afterScope.ServiceProvider.GetRequiredService<MustaskDbContext>();
            var en = await afterDb.Languages.AsNoTracking().SingleAsync(l => l.Code == "en");
            Assert.False(en.IsActive);
        }
        finally
        {
            using var restoreScope = _factory.Services.CreateScope();
            var restoreDb = restoreScope.ServiceProvider.GetRequiredService<MustaskDbContext>();
            var en = await restoreDb.Languages.SingleAsync(l => l.Code == "en");
            en.Replace(en.Endonym, en.SortOrder, isActive: true, DateTimeOffset.UtcNow, "admin");
            await restoreDb.SaveChangesAsync();
        }
    }
}
