using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// feat-028 QA: what the developer's own suites do not pin, through the real host and a real PostgreSQL — the comparison of Cyrillic
/// names, descriptions at their edges, a rename racing a creation, an unlink over items of every state and level, and a mixed
/// concurrent load over one project (US-1, US-3, US-4, US-5; spec § NFR Concurrency). Every test builds its own world.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class TeamQaEndpointTests(MustaskApiFactory factory)
{
    private const string NameTaken = "organizations.team.name_taken";

    private Task<Answer> SendAsync(HttpMethod method, string path, object? body = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(factory, method, path, body, OrganizationsTokens.SignedIn(), gate);

    private async Task<Item> ItemAsync(OwningTeamWorld world, string typeKey, string title, Guid? team)
    {
        var answer = await SendAsync(
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new
            {
                requestId = Guid.NewGuid().ToString(),
                typeId = world.Items.Types[typeKey].Id.ToString(),
                title,
                team = new { teamId = team?.ToString() },
            });
        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
    }

    private static string[] Rows(Answer answer) =>
        answer.Body.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("identifier").GetString()!).ToArray();

    // US-1 / BR-OL-3 · "compared ignoring letter case and surrounding spaces": the comparison holds for Cyrillic letters (the panel's own
    // language), on a creation and on a rename; the refused rename leaves the name it had.
    [Fact]
    public async Task TeamName_IsComparedIgnoringCyrillicLetterCase_OnCreationAndOnRename()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var first = await CreateTeamAsync(factory, organization, new { name = "Марс-Ёлка" });
        var second = await CreateTeamAsync(factory, organization, new { name = "Венера" });
        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (first.Status, second.Status));

        var created = await CreateTeamAsync(factory, organization, new { name = "  мАРС-ёлКА " });
        var renamed = await SendAsync(HttpMethod.Put, $"{Team(organization, second.Body.GetProperty("id").GetGuid())}/name", new { name = "МАРС-ЁЛКА" });

        Assert.Equal((HttpStatusCode.Conflict, NameTaken), (created.Status, created.ErrorCode));
        Assert.Equal((HttpStatusCode.Conflict, NameTaken), (renamed.Status, renamed.ErrorCode));
        Assert.Equal("Венера", (await StoredTeamAsync(factory, second.Body.GetProperty("id").GetGuid())).Name);
        Assert.Single(
            await StoredTeamsAsync(factory, organization),
            team => team.Status == Mustask.Domain.Organizations.TeamStatus.Active && team.NormalizedName == "МАРС-ЁЛКА");
    }

    // US-1, US-2 / BR-OL-3 · "kept as typed; an empty one is no description": a text of spaces only is not empty and is stored as sent;
    // the limit counts UTF-16 code units, so 1 000 characters outside the basic plane fit and 1 001 do not (team and project alike).
    [Fact]
    public async Task Description_OfSpacesOnly_IsKeptAsTyped_AndTheLimitCountsUtf16CodeUnits()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var project = structure.Project.Project.Id;
        var team = structure.Platform.Id;
        var teamPath = $"{Team(organization, team)}/description";
        var projectPath = $"/api/organizations/{organization}/projects/{project}/description";
        var emoji = char.ConvertFromUtf32(0x1F600);

        var teamSpaces = await SendAsync(HttpMethod.Put, teamPath, new { description = "   " });
        var projectSpaces = await SendAsync(HttpMethod.Put, projectPath, new { description = " \n " });

        Assert.Equal("   ", teamSpaces.Body.GetProperty("description").GetString());
        Assert.Equal(" \n ", projectSpaces.Body.GetProperty("description").GetString());
        Assert.Equal(" \n ", await ProjectDescriptionAsync(factory, project));

        var fits = await SendAsync(HttpMethod.Put, teamPath, new { description = string.Concat(Enumerable.Repeat(emoji, 1000)) });
        var tooLong = await SendAsync(HttpMethod.Put, teamPath, new { description = string.Concat(Enumerable.Repeat(emoji, 1001)) });
        var projectTooLong = await SendAsync(HttpMethod.Put, projectPath, new { description = string.Concat(Enumerable.Repeat(emoji, 1001)) });

        Assert.Equal(HttpStatusCode.OK, fits.Status);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.Status);
        Assert.Equal("organizations.description_length", ReasonOf(tooLong, "description"));
        Assert.Equal(HttpStatusCode.BadRequest, projectTooLong.Status);
        Assert.Equal("organizations.description_length", ReasonOf(projectTooLong, "description"));
        Assert.Equal(" \n ", await ProjectDescriptionAsync(factory, project));
    }

    // review-code CR-4 / review-security S-1, the race that pins the fix: the first team of a team-less project is linked, unlinked and
    // linked again while items are created for it, for nobody (the default is resolved) and for the team by name; no request may answer
    // with a server error. It is a race and not a script because the window it covers lies between two statements of ONE request, which
    // no commit gate can reach (a gate holds a request at its commit): QA ran it as a hypothesis and found nothing in 80 rounds, CI
    // caught it once, and 150 rounds on a developer machine failed in about one run of two before the fix, every time the same way —
    // the unlink answered 500 when its final DELETE of the link met an item bound to it (SQLSTATE 23001, the owner key's RESTRICT).
    // The window is now reached deterministically, without a gate, in Mustask.Infrastructure.Tests
    // Organizations/LinkJudgementWindowTests (an interceptor commits the link between the lock statement and the next one); this
    // test stays as the end-to-end guard that the request pairs the window used to break stay free of server errors.
    [Fact]
    public async Task TheFirstTeamOfATeamlessProject_LinkedAndUnlinkedWhileItemsAreCreated_NeverAServerError()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id, "Без команд", "NOT");
        var items = new WorkItemsWorld(organization.Id, project.Project.Id, project.Root.Id, "NOT", await WorkItemsTestData.TypesAsync(factory, organization.Id));
        var team = (await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "Первая")).Id;
        var story = items.Types["US"].Id.ToString();
        var failures = new List<string>();

        for (var round = 0; round < 6; round++)
        {
            var requests = new List<(string Name, Task<Answer> Task)>();

            for (var index = 0; index < 3; index++)
            {
                requests.Add(("link", LinkTeamsAsync(factory, organization.Id, project.Project.Id, new { teamIds = new[] { team.ToString() } })));
                requests.Add(("unlink to nobody", UnlinkAsync(factory, organization.Id, project.Project.Id, team, To(null))));
                requests.Add((
                    "create with the default",
                    SendAsync(HttpMethod.Post, WorkItemsTestData.List(items), new { requestId = Guid.NewGuid().ToString(), typeId = story, title = $"Умолчание {round}-{index}" })));
                requests.Add((
                    "create for the team",
                    SendAsync(
                        HttpMethod.Post,
                        WorkItemsTestData.List(items),
                        new { requestId = Guid.NewGuid().ToString(), typeId = story, title = $"Для команды {round}-{index}", team = new { teamId = team.ToString() } })));
            }

            var answers = await Task.WhenAll(requests.Select(async request => (request.Name, Answer: await request.Task)));
            failures.AddRange(
                answers.Where(answer => (int)answer.Answer.Status >= 500)
                    .Select(answer => $"round {round}: {answer.Name} answered {(int)answer.Answer.Status} {answer.Answer.ErrorCode}"));
        }

        Assert.Empty(failures);
    }

    // api-contract § 0.2 · "a list field is an array of id strings; a number or an object there fails binding with 400 http.bad_request":
    // the four routes that take a list answer a malformed one with the platform's 400, never a 5xx, and create or link nothing.
    [Theory]
    [InlineData("team", "memberIds", "[1]")]
    [InlineData("team", "projectIds", "\"not-a-list\"")]
    [InlineData("team", "projectIds", "{}")]
    [InlineData("project", "teamIds", "[{}]")]
    [InlineData("link-teams", "teamIds", "[5]")]
    [InlineData("link-projects", "projectIds", "[true]")]
    public async Task AListFieldOfTheWrongJsonShape_Returns400BadRequest_ChangesNothing(string route, string field, string json)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var (method, path, extra) = route switch
        {
            "team" => (HttpMethod.Post, OrganizationsApi.OrgTeams(organization), "\"name\":\"Новая\","),
            "project" => (HttpMethod.Post, $"/api/organizations/{organization}/projects", "\"name\":\"Новый\",\"key\":\"NEW\","),
            "link-teams" => (HttpMethod.Post, OrganizationsApi.Teams(organization, structure.ProjectId), string.Empty),
            _ => (HttpMethod.Post, $"{Team(organization, structure.Platform.Id)}/projects", string.Empty),
        };
        var teamsBefore = (await StoredTeamsAsync(factory, organization)).Count;
        var linksBefore = (await LinksOfProjectAsync(factory, structure.ProjectId)).Count;

        var answer = await SendAsync(method, path, JsonDocument.Parse($"{{{extra}\"{field}\":{json}}}").RootElement);

        Assert.Equal((HttpStatusCode.BadRequest, "http.bad_request"), (answer.Status, answer.ErrorCode));
        Assert.Equal(teamsBefore, (await StoredTeamsAsync(factory, organization)).Count);
        Assert.Equal(linksBefore, (await LinksOfProjectAsync(factory, structure.ProjectId)).Count);
    }

    // US-2 · "a project without a team works through the organization's roles" (BR-OL-10, BR-MX-10), the state this line creates: in a
    // project with no team a member creates two stories «Без команды», both stand on the project backlog and board, the member reorders
    // the backlog, a viewer reads both and reorders nothing.
    [Fact]
    public async Task ATeamlessProject_Works_ForAMember_AndReadsForAViewer()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id, "Без команд", "NOT");
        var items = new WorkItemsWorld(organization.Id, project.Project.Id, project.Root.Id, "NOT", await WorkItemsTestData.TypesAsync(factory, organization.Id));
        var world = new BoardsWorld(items, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty);
        var max = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);
        var vera = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Viewer);

        var first = await WorkItemsTestData.CreateAsync(factory, items, "US", "Первая", token: max.Token);
        var second = await WorkItemsTestData.CreateAsync(factory, items, "US", "Вторая", token: max.Token);
        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (first.Status, second.Status));
        var firstItem = new Item(first.Body.GetProperty("id").GetGuid(), first.Body.GetProperty("identifier").GetString()!);
        var secondItem = new Item(second.Body.GetProperty("id").GetGuid(), second.Body.GetProperty("identifier").GetString()!);

        Assert.Null((await StoredAsync(factory, items.ProjectId, firstItem.Identifier)).TeamId);
        Assert.Null((await StoredAsync(factory, items.ProjectId, secondItem.Identifier)).TeamId);
        Assert.Empty(await LinksOfProjectAsync(factory, items.ProjectId));
        Assert.Equal(
            [firstItem.Identifier, secondItem.Identifier],
            (await ProjectBoardsTestData.RowsAsync(factory, world)).Order(StringComparer.Ordinal));

        var moved = await ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Bottom(firstItem), token: max.Token);
        var refused = await ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Bottom(secondItem), token: vera.Token);
        var viewerRead = await ProjectBoardsTestData.GetBacklogAsync(factory, world, token: vera.Token);
        var board = await ProjectBoardsTestData.GetBoardAsync(factory, world, token: vera.Token);

        Assert.Equal(HttpStatusCode.NoContent, moved.Status);
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (refused.Status, refused.ErrorCode));
        Assert.Equal(
            [secondItem.Identifier, firstItem.Identifier],
            viewerRead.Body.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("identifier").GetString()!));
        Assert.Equal(
            [firstItem.Identifier, secondItem.Identifier],
            ProjectBoardsTestData.AllCards(board.Body).Order(StringComparer.Ordinal));
    }

    // US-8 · "GET …/access lists the four new codes for «Администратор» and for no one else": the four codes of ADR-0029 and the
    // narrowed `team.write` are served to the administrator alone, and a member and a viewer hold none of them.
    [Theory]
    [InlineData(OrganizationRole.Admin, true)]
    [InlineData(OrganizationRole.Member, false)]
    [InlineData(OrganizationRole.Viewer, false)]
    public async Task Access_ListsTheFourDeliveryCodes_ForTheAdministratorAlone(OrganizationRole role, bool listed)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, organization.Id, role);

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, $"/api/organizations/{organization.Id}/access", token: caller.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var served = answer.Body.GetProperty("permissions").EnumerateArray().Select(code => code.GetString()!).ToList();
        foreach (var code in new[]
                 {
                     "organizations.team.create",
                     "organizations.team.delete",
                     "organizations.team.link",
                     "organizations.project.link",
                     "organizations.team.write",
                     "organizations.project.write",
                 })
        {
            Assert.Equal(listed, served.Contains(code));
        }

        Assert.Contains("organizations.team.read", served);
    }

    // US-1 · "a rename racing a creation of its name": the creation holds the name uncommitted, the rename passes its pre-check and
    // waits on the team index, then loses with the pre-check's code; the team keeps its name.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Rename_VersusCreationOfTheSameName_OneWins_TheOtherIsNameTaken_NoServerError(bool creationFirst)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var venus = (await CreateTeamAsync(factory, organization, new { name = "Венера" })).Body.GetProperty("id").GetGuid();
        Task<Answer> Create(CommitGate? gate) => CreateTeamAsync(factory, organization, new { name = "Гонка" }, gate: gate);
        Task<Answer> Rename(CommitGate? gate) => SendAsync(HttpMethod.Put, $"{Team(organization, venus)}/name", new { name = "ГОНКА" }, gate);

        var (first, second) = creationFirst
            ? await RaceAsync(factory, Create, Rename)
            : await RaceAsync(factory, Rename, Create);

        var statuses = new[] { first, second };
        Assert.DoesNotContain(statuses, answer => (int)answer.Status >= 500);
        Assert.Equal(HttpStatusCode.Conflict, second.Status);
        Assert.Equal(NameTaken, second.ErrorCode);
        Assert.Single(await StoredTeamsAsync(factory, organization), team => team.NormalizedName == "ГОНКА");
    }

    // US-1 · "two renames to one name at once": one is saved, the other refused, no server error.
    [Fact]
    public async Task Rename_TwoTeamsToTheSameName_AtOnce_OneWins_TheOtherIsNameTaken()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var first = (await CreateTeamAsync(factory, organization, new { name = "Первая" })).Body.GetProperty("id").GetGuid();
        var second = (await CreateTeamAsync(factory, organization, new { name = "Вторая" })).Body.GetProperty("id").GetGuid();

        var (a, b) = await RaceAsync(
            factory,
            gate => SendAsync(HttpMethod.Put, $"{Team(organization, first)}/name", new { name = "Общая" }, gate),
            _ => SendAsync(HttpMethod.Put, $"{Team(organization, second)}/name", new { name = "общая" }));

        Assert.Equal(HttpStatusCode.OK, a.Status);
        Assert.Equal((HttpStatusCode.Conflict, NameTaken), (b.Status, b.ErrorCode));
        Assert.Equal("Вторая", (await StoredTeamAsync(factory, second)).Name);
    }

    // US-4 / BR-OL-8 · "in any state, Removed included" and "its place on the unlinked team's backlog is deleted … of its level": MARS owns
    // a placed story, a story in «Удалена» that still holds a place, and a placed feature; VENUS has a placed story and a placed feature.
    // After the unlink naming VENUS all three belong to it and show the operation's time; no place of MARS is left; on VENUS's stories
    // backlog the moved live story stands below the placed one and the removed one is on no backlog; on its features backlog the moved
    // feature stands below the placed one.
    [Fact]
    public async Task Unlink_ReassignsItemsOfEveryStateAndLevel_UnplacesLiveOnesBelowTheDestinationsPlaced()
    {
        var world = await CreateWorldAsync(factory);
        var story = await ItemAsync(world, "US", "Живая", world.Mars);
        var removed = await ItemAsync(world, "US", "Удалённая", world.Mars);
        var feature = await ItemAsync(world, "FEAT", "Фича Марса", world.Mars);
        var venusStory = await ItemAsync(world, "US", "Венерина", world.Venus);
        var venusFeature = await ItemAsync(world, "FEAT", "Фича Венеры", world.Venus);
        var stories = world.Items.Types["US"].BacklogLevelId;
        var features = world.Items.Types["FEAT"].BacklogLevelId;
        await PlaceAsync(factory, story.Id, world.Mars, stories, 1);
        await PlaceAsync(factory, removed.Id, world.Mars, stories, 2);
        await PlaceAsync(factory, feature.Id, world.Mars, features, 1);
        await PlaceAsync(factory, venusStory.Id, world.Venus, stories, 1);
        await PlaceAsync(factory, venusFeature.Id, world.Venus, features, 1);
        await LinksTestData.DeleteAsync(factory, world.Items, removed);

        var count = await SendAsync(HttpMethod.Get, $"{ProjectTeam(world.OrganizationId, world.ProjectId, world.Mars)}/unlink-count");
        var started = DateTimeOffset.UtcNow;
        var unlinked = await UnlinkAsync(factory, world.OrganizationId, world.ProjectId, world.Mars, To(world.Venus));
        var answered = DateTimeOffset.UtcNow;

        Assert.Equal(3, count.Body.GetProperty("count").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, unlinked.Status);
        foreach (var item in new[] { story, removed, feature })
        {
            var stored = await StoredAsync(factory, world.ProjectId, item.Identifier);
            Assert.Equal(world.Venus, stored.TeamId);
            Assert.InRange(stored.ChangedAt, started, answered);
            Assert.Equal(0, await TeamPlacesOfAsync(factory, item.Id));
        }

        Assert.Equal(0, await OwnedItemCountAsync(factory, world.Mars));
        Assert.Equal(0, await PlacesNamingAnotherTeamAsync(factory, world.ProjectId));
        var venusStories = Rows(await SendAsync(HttpMethod.Get, $"{Team(world.OrganizationId, world.Venus)}/backlogs/stories"));
        var venusFeatures = Rows(await SendAsync(HttpMethod.Get, $"{Team(world.OrganizationId, world.Venus)}/backlogs/features"));
        Assert.Equal([venusStory.Identifier, story.Identifier], venusStories);
        Assert.Equal([venusFeature.Identifier, feature.Identifier], venusFeatures);
        Assert.DoesNotContain(removed.Identifier, venusStories);
    }

    // spec § NFR Concurrency · "each end in one order and no server error": every kind of write that names a team — an unlink in both
    // directions and to nobody, a link, a deletion, a save, a creation (also beneath a parent) and a default naming — is sent at once over
    // one project, six rounds, with no commit gates, so the interleavings are the server's own. Nothing may answer with a 5xx
    // (a deadlock or a key violation would), and what is left holds the invariants of the model.
    [Fact]
    public async Task MixedTeamWrites_AtOnce_OverOneProject_NeverAnswerWithAServerError()
    {
        var world = await CreateWorldAsync(factory);
        var project = $"/api/organizations/{world.OrganizationId}/projects/{world.ProjectId}";
        await OrganizationsTestData.LinkAsync(factory, world.ProjectId, world.OtherProjectTeam);
        var saturn = await AddTeamAsync(factory, world.ProjectId, "SATURN2");
        var teams = new[] { world.Mars, world.Venus, world.OtherProjectTeam, saturn };
        var items = new List<Item>();
        for (var index = 0; index < 12; index++)
        {
            items.Add(await ItemAsync(world, "US", $"Элемент {index}", teams[index % teams.Length]));
        }

        var taskType = world.Items.Types["TASK"].Id.ToString();
        var failures = new List<string>();

        for (var round = 0; round < 6; round++)
        {
            var requests = new List<(string Name, Task<Answer> Task)>
            {
                ("unlink mars to venus", UnlinkAsync(factory, world.OrganizationId, world.ProjectId, world.Mars, To(world.Venus))),
                ("unlink venus to mars", UnlinkAsync(factory, world.OrganizationId, world.ProjectId, world.Venus, To(world.Mars))),
                ("unlink jupiter to nobody", UnlinkAsync(factory, world.OrganizationId, world.ProjectId, world.OtherProjectTeam, To(null))),
                ("unlink mars to saturn", UnlinkAsync(factory, world.OrganizationId, world.ProjectId, world.Mars, To(saturn))),
                ("link mars", LinkTeamsAsync(factory, world.OrganizationId, world.ProjectId, new { teamIds = new[] { world.Mars.ToString() } })),
                ("link venus and jupiter", LinkTeamsAsync(factory, world.OrganizationId, world.ProjectId, new { teamIds = new[] { world.Venus.ToString(), world.OtherProjectTeam.ToString() } })),
                ("link saturn from its end", LinkProjectsAsync(factory, world.OrganizationId, saturn, new { projectIds = new[] { world.ProjectId.ToString() } })),
                ("name venus default", SendAsync(HttpMethod.Put, $"{project}/default-team", new { teamId = world.Venus.ToString() })),
                ("name mars default", SendAsync(HttpMethod.Put, $"{project}/default-team", new { teamId = world.Mars.ToString() })),
                ("clear default", SendAsync(HttpMethod.Put, $"{project}/default-team", new { teamId = (string?)null })),
            };

            if (round == 1)
            {
                requests.Add(("delete saturn", DeleteTeamAsync(factory, world.OrganizationId, saturn)));
            }

            for (var index = 0; index < teams.Length; index++)
            {
                var team = teams[index];
                requests.Add(($"save item {index} to {index}", WorkItemsTestData.SaveAsync(factory, world.Items, items[index].Identifier, new { team = new { teamId = team.ToString() } })));
                requests.Add(($"save item {index + 4} to nobody", WorkItemsTestData.SaveAsync(factory, world.Items, items[index + 4].Identifier, new { team = new { teamId = (string?)null } })));
                requests.Add((
                    $"create for team {index}",
                    SendAsync(
                        HttpMethod.Post,
                        WorkItemsTestData.List(world.Items),
                        new { requestId = Guid.NewGuid().ToString(), typeId = world.Items.Types["US"].Id.ToString(), title = $"Новая {round}-{index}", team = new { teamId = team.ToString() } })));
            }

            requests.Add((
                "create a child",
                SendAsync(
                    HttpMethod.Post,
                    WorkItemsTestData.List(world.Items),
                    new { requestId = Guid.NewGuid().ToString(), typeId = taskType, title = $"Ребёнок {round}", parentId = items[8].Id.ToString() })));
            requests.Add((
                "create with the default",
                SendAsync(
                    HttpMethod.Post,
                    WorkItemsTestData.List(world.Items),
                    new { requestId = Guid.NewGuid().ToString(), typeId = world.Items.Types["US"].Id.ToString(), title = $"По умолчанию {round}" })));

            var answers = await Task.WhenAll(requests.Select(async request => (request.Name, Answer: await request.Task)));
            failures.AddRange(
                answers.Where(answer => (int)answer.Answer.Status >= 500)
                    .Select(answer => $"round {round}: {answer.Name} answered {(int)answer.Answer.Status} {answer.Answer.ErrorCode}"));

            // The next round starts from every live team linked again.
            await LinkTeamsAsync(
                factory,
                world.OrganizationId,
                world.ProjectId,
                new { teamIds = new[] { world.Mars.ToString(), world.Venus.ToString(), world.OtherProjectTeam.ToString() } });
        }

        Assert.Empty(failures);
        Assert.Equal(0, await PlacesNamingAnotherTeamAsync(factory, world.ProjectId));
        foreach (var link in await LinksOfProjectAsync(factory, world.ProjectId))
        {
            Assert.Equal(Mustask.Domain.Organizations.TeamStatus.Active, (await StoredTeamAsync(factory, link.TeamId)).Status);
        }
    }
}
