using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Identity;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// feat-003's world (spec § Common data): «Альфа» with its project (key ALP, standing for the spec's «Платформа» MSK), the teams
/// MARS and VENUS, six people, and the seven items of the stories under the numbers the stories give them — so the keyword "42"
/// finds a bug by its number and a task by its title. Built through the endpoints (create, save, delete, membership), then the
/// times of change set with SQL so the order is the stories': latest change first, the two deleted items changed after every
/// live one. Every test builds its own world: the database is shared and never emptied.
/// </summary>
internal static class SearchTestData
{
    /// <summary>The people of the data: Ann «Администратор», Mia, Zoya and Petr «Участник», Rita «Читатель», and Ivan, who has left.</summary>
    public sealed record Cast(string Ann, string Mia, string Rita, string Zoya, string Ivan, string Petr);

    /// <param name="Teams">The organization of the stories with MARS, VENUS, the other project (BET) and the other organization.</param>
    /// <param name="Items">Keyed as the stories name them: BUG42, US3, TASK4, BUG2, US1 (live), FEAT6, US5 (deleted).</param>
    public sealed record SearchWorld(OwningTeamWorld Teams, Cast People, IReadOnlyDictionary<string, Item> Items)
    {
        public WorkItemsWorld World => Teams.Items;

        public string Id(string key) => Items[key].Identifier;
    }

    /// <summary>The unfiltered list of the data, latest change first (spec § Common data).</summary>
    public static readonly string[] Live = ["BUG42", "US3", "TASK4", "BUG2", "US1"];

    public static async Task<SearchWorld> CreateAsync(MustaskApiFactory factory)
    {
        var teams = await CreateWorldAsync(factory);
        var world = teams.Items;
        var people = new Cast(
            await WorkItemsTestData.AddMemberAsync(factory, world, "Анна", "Смирнова", "anna@alfa.test", role: OrganizationRole.Admin),
            await WorkItemsTestData.AddMemberAsync(factory, world, "Мия", "Петрова", "mia@alfa.test"),
            await WorkItemsTestData.AddMemberAsync(factory, world, "Рита", "Орлова", "rita@alfa.test", role: OrganizationRole.Viewer),
            await WorkItemsTestData.AddMemberAsync(factory, world, "Зоя", "Ким", "zoya@alfa.test"),
            await WorkItemsTestData.AddMemberAsync(factory, world, "Иван", "Котов", "ivan@alfa.test"),
            await WorkItemsTestData.AddMemberAsync(factory, world, "Пётр", "Ли", "petr@alfa.test"));

        // The numbers 1 to 6 in the order the stories name them; 7 to 41 belonged to items purged earlier (BR-M3-5: gaps are normal).
        var us1 = await CreateItemAsync(factory, teams, "US", "Оплата картой", teams.Mars);
        var bug2 = await CreateItemAsync(factory, teams, "BUG", "Ошибка оплаты", teams.Venus);
        var us3 = await CreateItemAsync(factory, teams, "US", "Отчёт склада: 100%", null);
        var task4 = await CreateItemAsync(factory, teams, "TASK", "Проверить 42 сценария", teams.Mars);
        var us5 = await CreateItemAsync(factory, teams, "US", "Возврат оплаты", teams.Mars);
        var feat6 = await CreateItemAsync(factory, teams, "FEAT", "Импорт склада", teams.Venus);
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_item_counters SET last_number = 41 WHERE project_id = {world.ProjectId}");
        var bug42 = await CreateItemAsync(factory, teams, "BUG", "Падение при входе", teams.Venus);

        await SaveAsync(factory, world, us1, new
        {
            stateId = world.Types["US"].StateId("Активна").ToString(),
            assignee = new { userId = people.Mia },
            description = "Через Stripe",
        });
        await SaveAsync(factory, world, bug2, new { stateId = world.Types["BUG"].StateId("Разрешена").ToString(), assignee = new { userId = people.Ivan } });
        await SaveAsync(factory, world, us3, new { stateId = world.Types["US"].StateId("Разрешена").ToString() });
        await SaveAsync(factory, world, task4, new { stateId = world.Types["TASK"].StateId("Закрыта").ToString(), assignee = new { userId = people.Petr } });
        await SaveAsync(factory, world, us5, new { stateId = world.Types["US"].StateId("Активна").ToString(), assignee = new { userId = people.Ivan } });
        await SaveAsync(factory, world, feat6, new { assignee = new { userId = people.Zoya } });
        await SaveAsync(factory, world, bug42, new { assignee = new { userId = people.Mia } });

        await LinksTestData.DeleteAsync(factory, world, us5);
        await LinksTestData.DeleteAsync(factory, world, feat6);

        // Ivan has left the organization; Petr's account is disabled (both only after they were assigned).
        await MembershipTestData.RemoveMemberAsync(factory, world.OrganizationId, people.Ivan);
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE directory_users SET enabled = false, is_active = false WHERE id = {people.Petr}");

        var items = new Dictionary<string, Item>
        {
            ["US1"] = us1, ["BUG2"] = bug2, ["US3"] = us3, ["TASK4"] = task4, ["US5"] = us5, ["FEAT6"] = feat6, ["BUG42"] = bug42,
        };

        // Latest change first: BUG-42, US-3, TASK-4, BUG-2, US-1; the deleted FEAT-6 then US-5 changed after all of them.
        var origin = DateTimeOffset.UtcNow.AddDays(-1);
        var order = new[] { "US1", "BUG2", "TASK4", "US3", "BUG42", "US5", "FEAT6" };

        for (var minute = 0; minute < order.Length; minute++)
        {
            var at = origin.AddMinutes(minute);
            var id = items[order[minute]].Id;
            await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_items SET changed_at = {at} WHERE id = {id}");
        }

        return new SearchWorld(teams, people, items);
    }

    /// <summary>An item of the project created with its owning team (none for «Без команды»); fails the test unless it answers 201.</summary>
    public static async Task<Item> CreateItemAsync(MustaskApiFactory factory, OwningTeamWorld teams, string typeKey, string title, Guid? team)
    {
        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(teams.Items),
            new { requestId = Guid.NewGuid().ToString(), typeId = teams.Items.Types[typeKey].Id.ToString(), title, team = new { teamId = team?.ToString() } },
            OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
    }

    private static async Task SaveAsync(MustaskApiFactory factory, WorkItemsWorld world, Item item, object body)
    {
        var answer = await WorkItemsTestData.SaveAsync(factory, world, item.Identifier, body);
        Assert.True(answer.Status == HttpStatusCode.OK, $"Saving {item.Identifier} answered {answer.Status} {answer.ErrorCode}.");
    }

    /// <summary>The query string of a list or options request: each value escaped, repeated per value, in the order given.</summary>
    public static string Query(
        IEnumerable<string>? states = null,
        IEnumerable<string>? assignees = null,
        string? search = null,
        IEnumerable<string>? teams = null,
        int? page = null)
    {
        var parts = new List<string>();

        if (page is not null)
        {
            parts.Add($"page={page}");
        }

        parts.AddRange((states ?? []).Select(state => "state=" + Uri.EscapeDataString(state)));
        parts.AddRange((assignees ?? []).Select(assignee => "assigneeId=" + Uri.EscapeDataString(assignee)));
        parts.AddRange((teams ?? []).Select(team => "teamId=" + Uri.EscapeDataString(team)));

        if (search is not null)
        {
            parts.Add("search=" + Uri.EscapeDataString(search));
        }

        return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
    }

    public static Task<Answer> ListAsync(MustaskApiFactory factory, WorkItemsWorld world, string query, string? token = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, WorkItemsTestData.List(world) + query, token: token ?? OrganizationsTokens.SignedIn());

    public static string OptionsPath(WorkItemsWorld world) => WorkItemsTestData.List(world) + "/assignee-options";

    public static Task<Answer> OptionsAsync(MustaskApiFactory factory, WorkItemsWorld world, string query = "", string? token = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, OptionsPath(world) + query, token: token ?? OrganizationsTokens.SignedIn());

    /// <summary>The identifiers of a list answer's rows, in the order answered.</summary>
    public static string[] Identifiers(Answer answer) =>
        answer.Body.GetProperty("data").EnumerateArray().Select(row => row.GetProperty("identifier").GetString()!).ToArray();

    public static int Count(Answer answer) => answer.Body.GetProperty("count").GetInt32();

    /// <summary>The identifiers of the data's items by key, in the order given.</summary>
    public static string[] Of(SearchWorld data, params string[] keys) => keys.Select(data.Id).ToArray();

    /// <summary>A person of an options answer as the contract's five fields.</summary>
    public static (string Id, string? Name, string? Email, bool Active, bool Member) Option(JsonElement option) => (
        option.GetProperty("id").GetString()!,
        option.GetProperty("displayName").ValueKind == JsonValueKind.Null ? null : option.GetProperty("displayName").GetString(),
        option.GetProperty("email").ValueKind == JsonValueKind.Null ? null : option.GetProperty("email").GetString(),
        option.GetProperty("isActive").GetBoolean(),
        option.GetProperty("isMember").GetBoolean());

    public static List<(string Id, string? Name, string? Email, bool Active, bool Member)> Options(Answer answer) =>
        answer.Body.EnumerateArray().Select(Option).ToList();
}
