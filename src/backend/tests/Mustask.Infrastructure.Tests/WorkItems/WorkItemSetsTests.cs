using Mustask.Infrastructure.Tests.Organizations;
using Mustask.Infrastructure.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Tests.WorkItems;

/// <summary>
/// mod-005 (ADR-0030 rule 2): the entry points pick items by the <b>category</b> of their state, never by its name, and
/// across organizations — each with a process of its own. In one organization a state named «Удалена» sits in the Proposed
/// category and one named «Убрана» in the Removed category; only the second is deleted.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class WorkItemSetsTests(OrganizationsDatabaseFixture database)
{
    [Fact]
    public async Task Deleted_ReturnsExactlyTheItemsInTheRemovedCategory_AcrossTwoOrganizations_ByCategoryNeverByName()
    {
        var first = await SeedAsync(trap: false);
        var second = await SeedAsync(trap: true);

        await using var context = database.NewContext();
        var projects = new[] { first.Project, second.Project };

        var deleted = await WorkItemSets.Deleted(context).AsNoTracking().Where(item => projects.Contains(item.ProjectId)).Select(item => item.Id).ToListAsync();
        var live = await WorkItemSets.Live(context).AsNoTracking().Where(item => projects.Contains(item.ProjectId)).Select(item => item.Id).ToListAsync();
        var all = await WorkItemSets.InAnyState(context).AsNoTracking().Where(item => projects.Contains(item.ProjectId)).Select(item => item.Id).ToListAsync();

        Assert.Equal(
            new[] { first.InRemoved, second.InRemoved, second.InRemovedByCategoryOnly }.Order(),
            deleted.Order());

        // The state that is only named «Удалена» is live, and so are the items of every other state.
        Assert.Contains(second.InProposedNamedRemoved, live);
        Assert.DoesNotContain(second.InProposedNamedRemoved, deleted);
        Assert.Contains(first.InProposed, live);
        Assert.Equal(all.Order(), live.Concat(deleted).Order());
    }

    // feat-003 BR-WS-4, BR-WS-15: the list's entry point. Without a state filter it is the project's live items; with one, the live
    // and the deleted items of the project beside each other, every item once — by category, never by name, and never another project's.
    [Fact]
    public async Task Listing_IsTheProjectsLiveItems_AndWithStatesChosen_TheDeletedOnesBesideThem_OfThatProjectAlone()
    {
        var first = await SeedAsync(trap: false);
        var second = await SeedAsync(trap: true);

        await using var context = database.NewContext();

        async Task<List<Guid>> Ids(Guid project, bool statesChosen) =>
            await WorkItemSets.Listing(context, project, statesChosen).AsNoTracking().Select(item => item.Id).ToListAsync();

        Assert.Equal([first.InProposed], await Ids(first.Project, false));
        Assert.Equal(new[] { first.InProposed, first.InRemoved }.Order(), (await Ids(first.Project, true)).Order());

        // The state that is only named «Удалена» is live; the one in the Removed category is deleted, whatever it is named.
        Assert.Equal(new[] { second.InProposed, second.InProposedNamedRemoved }.Order(), (await Ids(second.Project, false)).Order());
        Assert.Equal(
            new[] { second.InProposed, second.InRemoved, second.InProposedNamedRemoved, second.InRemovedByCategoryOnly }.Order(),
            (await Ids(second.Project, true)).Order());
    }

    private sealed record Seeded(Guid Project, Guid InProposed, Guid InRemoved, Guid InProposedNamedRemoved, Guid InRemovedByCategoryOnly);

    /// <summary>
    /// A project of a fresh organization with one task in its first state and one in its «Удалена»; with <paramref name="trap"/>,
    /// a type whose Proposed state is named «Удалена» and whose Removed state is named «Убрана» holds one item in each.
    /// </summary>
    private async Task<Seeded> SeedAsync(bool trap)
    {
        var structure = await database.CreateStructureAsync();
        var project = structure.Project.Project.Id;
        var task = (await database.QueryAsync(
            """
            SELECT concat_ws('|', type.id::text, first.id::text, removed.id::text, type.process_id::text, type.backlog_level_id::text)
            FROM work_item_types AS type
            JOIN processes AS process ON process.id = type.process_id
            JOIN state_definitions AS first ON first.work_item_type_id = type.id AND first.position = 1
            JOIN state_definitions AS removed ON removed.work_item_type_id = type.id AND removed.category = 'Removed'
            WHERE process.organization_id = @organization AND type.key = 'TASK'
            """,
            ("organization", structure.Organization.Id))).Single().Split('|');

        var (inProposed, inRemoved) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        await InsertAsync(project, structure.Project.Root.Id, 1, inProposed, Guid.Parse(task[0]), Guid.Parse(task[1]));
        await InsertAsync(project, structure.Project.Root.Id, 2, inRemoved, Guid.Parse(task[0]), Guid.Parse(task[2]));

        if (!trap)
        {
            return new Seeded(project, inProposed, inRemoved, Guid.Empty, Guid.Empty);
        }

        var (type, named, renamed) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        await database.ExecuteAsync(
            """
            INSERT INTO work_item_types (id, process_id, backlog_level_id, key, name, appearance, position)
            VALUES (@type, @process, @level, 'TC', 'Тест-кейс', 'testcase', 99);
            INSERT INTO state_definitions (id, work_item_type_id, name, category, position) VALUES
                (@named, @type, 'Удалена', 'Proposed', 1),
                (@renamed, @type, 'Убрана', 'Removed', 2)
            """,
            ("type", type),
            ("process", Guid.Parse(task[3])),
            ("level", Guid.Parse(task[4])),
            ("named", named),
            ("renamed", renamed));

        var (byName, byCategory) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        await InsertAsync(project, structure.Project.Root.Id, 3, byName, type, named);
        await InsertAsync(project, structure.Project.Root.Id, 4, byCategory, type, renamed);

        return new Seeded(project, inProposed, inRemoved, byName, byCategory);
    }

    private Task InsertAsync(Guid project, Guid area, int number, Guid id, Guid type, Guid state) =>
        database.ExecuteAsync(
            """
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at)
            VALUES (@id, @project, @number, @type, @state, @area, 'Элемент', '', gen_random_uuid(), now(), 'sets-test', now())
            """,
            ("id", id),
            ("project", project),
            ("number", number),
            ("type", type),
            ("state", state),
            ("area", area));
}
