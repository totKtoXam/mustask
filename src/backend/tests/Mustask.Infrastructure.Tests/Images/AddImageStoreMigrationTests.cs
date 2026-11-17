using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mustask.Infrastructure.Tests.Organizations;
using Npgsql;

namespace Mustask.Infrastructure.Tests.Images;

/// <summary>
/// feat-078 plan-backend § 3.2 and § 13 (Migrations): <c>AddImageStore</c> goes up, down and up again; the key onto
/// <c>work_items</c> cascades across schemas; the content column is stored out of line; the checks refuse a bad row. The
/// database is this class's own: the test rolls a migration back.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class AddImageStoreMigrationTests(OrganizationsDatabaseFixture database) : IClassFixture<OrganizationsDatabaseFixture>
{
    private const string MigrationName = "_AddImageStore";

    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    // US-5 · "A purge takes the images": the table pair goes up, down and up again.
    [Fact]
    public async Task Migrator_AddImageStore_GoesUpDownAndUpAgain()
    {
        var world = new ImagesTestWorld(database);
        await world.SeedWorkItemAsync();

        Assert.True(await SchemaHasTableAsync("images"));
        Assert.True(await SchemaHasTableAsync("image_contents"));

        await MigrateToAsync(previous: true);

        Assert.False(await SchemaHasTableAsync("images"));
        Assert.False(await SchemaHasTableAsync("image_contents"));
        Assert.Equal(["0"], await database.QueryAsync("SELECT count(*)::text FROM information_schema.schemata WHERE schema_name = 'images'"));

        await MigrateToAsync(previous: false);

        Assert.True(await SchemaHasTableAsync("images"));
    }

    [Fact]
    public async Task Model_HasNoPendingChanges_AfterTheMigrations()
    {
        await using var context = database.NewContext();

        await context.Database.MigrateAsync();

        Assert.Contains(await context.Database.GetAppliedMigrationsAsync(), id => id.EndsWith(MigrationName, StringComparison.Ordinal));
        Assert.False(context.Database.HasPendingModelChanges(), "The model has changes no migration carries.");
    }

    // US-5 · "A purge takes the images": deleting the item removes its bound and unbound images and their bytes.
    [Fact]
    public async Task DeletingTheWorkItem_CascadesToImagesAndTheirContent_AcrossSchemas()
    {
        var world = new ImagesTestWorld(database);
        var item = await world.SeedWorkItemAsync();
        var bound = await world.InsertImageAsync(item, "u1", Now, bound: true);
        await world.InsertImageAsync(item, "u1", Now, bound: false);

        await database.ExecuteAsync("DELETE FROM work_items WHERE id = @item", ("item", item));

        Assert.Equal(0, await world.CountImagesAsync());
        Assert.Equal(["0"], await database.QueryAsync("SELECT count(*)::text FROM images.image_contents WHERE image_id = @id", ("id", bound)));
    }

    // ADR-0045 decision 1: bytes out of line and uncompressed.
    [Fact]
    public async Task Content_IsStoredExternal()
    {
        var world = new ImagesTestWorld(database);
        await world.SeedWorkItemAsync();

        Assert.Equal(
            ["e"],
            await database.QueryAsync(
                "SELECT attstorage::text FROM pg_attribute WHERE attrelid = 'images.image_contents'::regclass AND attname = 'content'"));
    }

    [Fact]
    public async Task Checks_RefuseABadRow()
    {
        var world = new ImagesTestWorld(database);
        var item = await world.SeedWorkItemAsync();

        async Task Insert(string purpose = "WorkItemDescription", string format = "Png", int length = 10, int width = 10, int frames = 1, Guid? owner = null) =>
            await database.ExecuteAsync(
                """
                INSERT INTO images.images (id, purpose, format, byte_length, width, height, frame_count, uploaded_by, uploaded_at, work_item_id)
                VALUES (gen_random_uuid(), @purpose, @format, @length, @width, 10, @frames, 'u', now(), @item)
                """,
                ("purpose", purpose), ("format", format), ("length", length), ("width", width), ("frames", frames), ("item", owner ?? item));

        await Insert();
        await Assert.ThrowsAsync<PostgresException>(() => Insert(purpose: "FeedbackSubmission"));
        await Assert.ThrowsAsync<PostgresException>(() => Insert(format: "Bmp"));
        await Assert.ThrowsAsync<PostgresException>(() => Insert(length: 0));
        await Assert.ThrowsAsync<PostgresException>(() => Insert(length: 5_242_881));
        await Assert.ThrowsAsync<PostgresException>(() => Insert(width: 20_001));
        await Assert.ThrowsAsync<PostgresException>(() => Insert(frames: 1_001));
        await Assert.ThrowsAsync<PostgresException>(() => Insert(owner: Guid.NewGuid()));
    }

    private async Task MigrateToAsync(bool previous)
    {
        await using var context = database.NewContext();
        var migrations = context.Database.GetMigrations().ToList();
        var index = migrations.FindIndex(id => id.EndsWith(MigrationName, StringComparison.Ordinal));
        var target = previous ? migrations[index - 1] : migrations[index];

        await context.GetService<IMigrator>().MigrateAsync(target);
    }

    private async Task<bool> SchemaHasTableAsync(string table) =>
        (await database.QueryAsync("SELECT (to_regclass(@table) IS NOT NULL)::text", ("table", "images." + table))).Single() == "true";
}
