using Microsoft.EntityFrameworkCore;
using Mustask.Infrastructure.Tests.Organizations;

namespace Mustask.Infrastructure.Tests.Images;

/// <summary>A work item on the fixture's own database, written by SQL like the migration tests do, and raw image rows (feat-078).</summary>
internal sealed class ImagesTestWorld(OrganizationsDatabaseFixture database)
{
    public OrganizationsDatabaseFixture Database { get; } = database;

    /// <summary>Applies the migrations and seeds one story; returns its id. <paramref name="number"/> distinguishes several items of one project.</summary>
    public async Task<Guid> SeedWorkItemAsync()
    {
        await using (var context = Database.NewContext())
        {
            await context.Database.MigrateAsync();
        }

        var structure = await Database.CreateStructureAsync();
        var row = (await Database.QueryAsync(
            """
            SELECT concat_ws('|', type.id::text, state.id::text)
            FROM work_item_types AS type
            JOIN processes AS process ON process.id = type.process_id
            JOIN state_definitions AS state ON state.work_item_type_id = type.id AND state.position = 1
            WHERE process.organization_id = @organization AND type.key = 'US'
            """,
            ("organization", structure.Organization.Id))).Single().Split('|');
        var item = Guid.CreateVersion7();

        await Database.ExecuteAsync(
            """
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, owning_team_id)
            VALUES (@item, @project, 1, @type, @state, @area, 'История', '', gen_random_uuid(), now(), 'images-test', now(), @team)
            """,
            ("item", item),
            ("project", structure.Project.Project.Id),
            ("type", Guid.Parse(row[0])),
            ("state", Guid.Parse(row[1])),
            ("area", structure.Project.Root.Id),
            ("team", structure.FirstTeam.Id));

        return item;
    }

    /// <summary>Inserts an image row and its content with raw SQL; <paramref name="uploadedAt"/> may be in the past.</summary>
    public async Task<Guid> InsertImageAsync(Guid workItem, string uploadedBy, DateTimeOffset uploadedAt, bool bound, int byteLength = 4)
    {
        var id = Guid.NewGuid();

        await Database.ExecuteAsync(
            """
            INSERT INTO images.images (id, purpose, format, byte_length, width, height, frame_count, uploaded_by, uploaded_at, bound_at, work_item_id)
            VALUES (@id, 'WorkItemDescription', 'Png', @length, 10, 10, 1, @by, @at, CASE WHEN @bound THEN @at END, @item);
            INSERT INTO images.image_contents (image_id, content) VALUES (@id, decode(repeat('ab', @length), 'hex'));
            """,
            ("id", id), ("length", byteLength), ("by", uploadedBy), ("at", uploadedAt), ("bound", bound), ("item", workItem));

        return id;
    }

    public async Task<int> CountImagesAsync(Guid? id = null) =>
        int.Parse((await Database.QueryAsync(
            id is null ? "SELECT count(*)::text FROM images.images" : "SELECT count(*)::text FROM images.images WHERE id = @id",
            id is null ? [] : [("id", (object)id.Value)])).Single());
}
