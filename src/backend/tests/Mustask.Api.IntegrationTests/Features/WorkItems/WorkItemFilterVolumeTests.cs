using System.Diagnostics;
using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.UserDirectory;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// feat-003 spec § NFR Volumes, api-contract § 6, through the real host and a real PostgreSQL: an organization of 100 000 work items,
/// 5 000 of them in the project read (500 of those deleted) and 95 000 in another, and 2 000 people assigned across both. The project's
/// first page and its count, unfiltered, by each dimension alone and by all four at once — «Удалена» among the states — and the
/// «Исполнитель» options, with the bin chosen and without, are each served within 2 s, as the panel sends them, after one warm-up call
/// of each. The items are inserted with one statement per project: through the endpoints the setup alone would take hours.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class WorkItemFilterVolumeTests(MustaskApiFactory factory, ITestOutputHelper output)
{
    private const int InProject = 5_000;
    private const int InOtherProject = 95_000;
    private const int People = 2_000;
    private static readonly TimeSpan ReadBudget = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task List_100000ItemsInTheOrganization_EveryFilter_And_TheOptions_ReadWithin2s()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var other = await WorkItemsTestData.AddProjectAsync(factory, world, "BIG", "Большой");
        var people = Enumerable.Range(0, People).Select(_ => Guid.NewGuid().ToString()).ToArray();

        await UserDirectoryTestData.SeedAsync(
            factory, people.Select((id, index) => UserDirectoryTestData.Push(id, $"Имя{index:D4}", $"Фамилия{index:D4}", $"user{index:D4}@volume.test")));
        await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var memberships = services.GetRequiredService<IMembershipRepository>();

            foreach (var id in people.Take(People / 2))
            {
                memberships.Add(OrganizationMembership.Add(world.OrganizationId, id, OrganizationRole.Member, OrganizationsTestData.Creator, OrganizationsTestData.Now));
            }

            await memberships.SaveAsync(ct);

            return true;
        });

        var task = world.Types["TASK"];
        var open = task.StateId("Новая");
        var removed = task.StateId(Mustask.Domain.Process.StateCategory.Removed);

        foreach (var (project, count) in new[] { (world.ProjectId, InProject), (other.ProjectId, InOtherProject) })
        {
            // Item n is titled «Задача n», assigned to the person n mod 2 000 but every tenth to nobody, and deleted (with the marks a
            // Delete leaves) when n mod 10 is 7: 500 of the project's 5 000.
            await WorkItemsTestData.ExecuteAsync(factory, $"""
                INSERT INTO work_items (id, project_id, number, type_id, state_id, title, description, creation_request_id, created_at, created_by, changed_at,
                                        assignee_id, deleted_from_state_id, deleted_by, deleted_at)
                SELECT gen_random_uuid(), {project}, n, {task.Id}, CASE WHEN n % 10 = 7 THEN {removed} ELSE {open} END,
                       'Задача ' || n, '', gen_random_uuid(), now(), 'volume-test', now() - n * interval '1 second',
                       CASE WHEN n % 10 = 0 THEN NULL ELSE ({people})[(n % {People}) + 1] END,
                       CASE WHEN n % 10 = 7 THEN {open} END, CASE WHEN n % 10 = 7 THEN 'volume-test' END, CASE WHEN n % 10 = 7 THEN now() END
                FROM generate_series(1, {count}) AS n
                """);
            await WorkItemsTestData.ExecuteAsync(factory, $"""
                INSERT INTO work_item_counters (project_id, last_number) VALUES ({project}, {count})
                ON CONFLICT (project_id) DO UPDATE SET last_number = excluded.last_number
                """);
        }

        string Escaped(string value) => Uri.EscapeDataString(value);
        var list = WorkItemsTestData.List(world);

        Task<Answer> Get(string path) => OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: OrganizationsTokens.SignedIn());

        var requests = new (string What, Func<Task<Answer>> Send)[]
        {
            ("the list", () => Get(list)),
            ("the list, page 3", () => Get(list + "?page=3")),
            ("one state", () => Get($"{list}?state={Escaped("Новая")}")),
            ("the bin", () => Get($"{list}?state={Escaped("Удалена")}")),
            ("the bin, page 5", () => Get($"{list}?state={Escaped("Удалена")}&page=5")),
            ("a state beside the bin", () => Get($"{list}?state={Escaped("Новая")}&state={Escaped("Удалена")}")),
            ("one person", () => Get($"{list}?assigneeId={people[1]}")),
            ("several people and nobody", () => Get($"{list}?assigneeId={people[1]}&assigneeId={people[2]}&assigneeId=none")),
            ("a title", () => Get($"{list}?search={Escaped("Задача 77")}")),
            ("a title that is in every item", () => Get($"{list}?search={Escaped("задача")}")),
            ("an identifier", () => Get($"{list}?search=msk-task-77")),
            ("a number", () => Get($"{list}?search=77")),
            ("a keyword nothing holds", () => Get($"{list}?search={Escaped("нет такого заголовка")}")),
            (
                "all four, the bin among the states",
                () => Get($"{list}?state={Escaped("Новая")}&state={Escaped("Удалена")}&assigneeId={people[1]}&assigneeId=none&search={Escaped("Задача")}&teamId=none")),
            ("the options", () => Get(list + "/assignee-options")),
            ("the options, the bin chosen", () => Get($"{list}/assignee-options?state={Escaped("Удалена")}")),
        };

        // Warm-up, not asserted: a process's first call of an operation also pays JIT, query compilation and connection warm-up.
        foreach (var (_, send) in requests)
        {
            Assert.Equal(HttpStatusCode.OK, (await send()).Status);
        }

        foreach (var (what, send) in requests)
        {
            var watch = Stopwatch.StartNew();
            var answer = await send();
            watch.Stop();
            output.WriteLine($"{what}: {watch.Elapsed.TotalMilliseconds:F0} ms");

            Assert.Equal(HttpStatusCode.OK, answer.Status);
            Assert.True(watch.Elapsed < ReadBudget, $"{what} took {watch.Elapsed}.");
        }

        // The answers are the data's, not just fast: read back from the stored rows — 4 500 live items, the bin's 500, the keyword's
        // titles, and the people of the live items and of all of them (the options with the bin chosen).
        var stored = await WorkItemsTestData.StoredAsync(factory, world.ProjectId);
        var live = stored.Where(item => item.StateId != removed).ToList();
        static int peopleOf(IEnumerable<StoredItem> items) => items.Select(item => item.AssigneeId).OfType<string>().Distinct().Count();

        Assert.Equal((4_500, 500), (live.Count, stored.Count - live.Count));
        Assert.Equal(live.Count, (await Get(list)).Body.GetProperty("count").GetInt32());
        Assert.Equal(stored.Count - live.Count, (await Get($"{list}?state={Escaped("Удалена")}")).Body.GetProperty("count").GetInt32());
        Assert.Equal(stored.Count, (await Get($"{list}?state={Escaped("Новая")}&state={Escaped("Удалена")}")).Body.GetProperty("count").GetInt32());
        Assert.Equal(
            live.Count(item => item.Title.Contains("Задача 77", StringComparison.Ordinal)),
            (await Get($"{list}?search={Escaped("Задача 77")}")).Body.GetProperty("count").GetInt32());
        Assert.Equal(peopleOf(live), (await Get($"{list}/assignee-options")).Body.GetArrayLength());
        Assert.Equal(peopleOf(stored), (await Get($"{list}/assignee-options?state={Escaped("Удалена")}")).Body.GetArrayLength());
        Assert.True(peopleOf(live) > People / 2, "The options read is measured over well over 500 people (several lookups).");
    }
}
