using System.Net;
using System.Text.Json;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/default-area</c>
/// (api-contract § 3.5) through the real host and a real PostgreSQL: lane L-19
/// (plan-backend § 14.3). Every test builds its own organization
/// (<see cref="OrganizationsTestData"/>): the database is shared and never
/// emptied. The two races of US-6 run against the real re-own and delete
/// endpoints, so they need those routes' handlers (lanes L-24 and L-25) to pass.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SetTeamDefaultAreaEndpointTests(MustaskApiFactory factory)
{
    /// <summary>
    /// «Пример проекта» with its first team and root, and the team «Платформа» owning «Платформа» (its default) and the
    /// leaf «Платформа / API» explicitly (US-4).
    /// </summary>
    private sealed record Scenario(OrganizationStructure Structure, Area Api)
    {
        public Guid TeamId => Structure.Platform.Team.Id;
    }

    private async Task<Scenario> CreateScenarioAsync()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var api = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.Platform.Area.Id, "API", structure.Platform.Team.Id);

        return new Scenario(structure, api);
    }

    private Task<Answer> SetDefaultAsync(
        Guid organizationId, string projectId, string teamId, object? body, string? token = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"/api/organizations/{organizationId}/projects/{Uri.EscapeDataString(projectId)}/teams/{Uri.EscapeDataString(teamId)}/default-area",
            body,
            token ?? OrganizationsTokens.SignedIn(),
            gate);

    private Task<Answer> SetDefaultAsync(OrganizationStructure structure, Guid teamId, string? areaId, CommitGate? gate = null) =>
        SetDefaultAsync(structure.OrganizationId, structure.ProjectId.ToString(), teamId.ToString(), new { areaId }, gate: gate);

    private Task<Answer> ReownAsync(OrganizationStructure structure, Guid areaId, Guid ownerTeamId, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Areas(structure.OrganizationId, structure.ProjectId)}/{areaId}/owner",
            new { ownerTeamId = ownerTeamId.ToString() },
            OrganizationsTokens.SignedIn(),
            gate);

    private Task<Answer> DeleteAreaAsync(OrganizationStructure structure, Guid areaId, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Delete,
            $"{OrganizationsApi.Areas(structure.OrganizationId, structure.ProjectId)}/{areaId}",
            token: OrganizationsTokens.SignedIn(),
            gate: gate);

    private async Task<AreaTree> ReadTreeAsync(OrganizationStructure structure) =>
        await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);

    // US-4 · "A team's default area is chosen among the areas it owns explicitly": «Платформа / API» becomes the default;
    // the former default keeps its owner.
    [Fact]
    public async Task Set_ExplicitlyOwnedArea_Returns200()
    {
        var s = await CreateScenarioAsync();

        var answer = await SetDefaultAsync(s.Structure, s.TeamId, s.Api.Id.ToString());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(["defaultArea", "id", "isFirstTeam", "name", "projectId"], PropertyNames(answer.Body));
        Assert.Equal(s.TeamId, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal(s.Structure.ProjectId, answer.Body.GetProperty("projectId").GetGuid());
        Assert.Equal("Платформа", answer.Body.GetProperty("name").GetString());
        Assert.False(answer.Body.GetProperty("isFirstTeam").GetBoolean());

        var defaultArea = answer.Body.GetProperty("defaultArea");
        Assert.Equal(["id", "path"], PropertyNames(defaultArea));
        Assert.Equal(s.Api.Id, defaultArea.GetProperty("id").GetGuid());
        Assert.Equal("Платформа / API", defaultArea.GetProperty("path").GetString());

        var tree = await ReadTreeAsync(s.Structure);
        Assert.Equal(s.Api.Id, DefaultOf(tree, s.TeamId).Id);
        Assert.Equal(s.TeamId, tree[s.Api.Id].OwnerTeamId);
        Assert.Null(tree[s.Structure.Platform.Area.Id].DefaultOfTeamId);
        Assert.Equal(s.TeamId, tree[s.Structure.Platform.Area.Id].OwnerTeamId);
    }

    // US-4 · the default goes back to the team's first area, and the ids are accepted in either letter case (api-contract § 0.2).
    [Fact]
    public async Task Set_BackToTheFirstArea_Returns200()
    {
        var s = await CreateScenarioAsync();
        await SetDefaultAsync(s.Structure, s.TeamId, s.Api.Id.ToString());

        var answer = await SetDefaultAsync(
            s.Structure.OrganizationId,
            s.Structure.ProjectId.ToString().ToUpperInvariant(),
            s.TeamId.ToString().ToUpperInvariant(),
            new { areaId = s.Structure.Platform.Area.Id.ToString().ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("Платформа", answer.Body.GetProperty("defaultArea").GetProperty("path").GetString());
        Assert.Equal(s.Structure.Platform.Area.Id, DefaultOf(await ReadTreeAsync(s.Structure), s.TeamId).Id);
    }

    // api-contract § 3.5: "Setting the current default again answers 200 and changes nothing."
    [Fact]
    public async Task Set_CurrentDefaultAgain_Returns200_ChangesNothing()
    {
        var s = await CreateScenarioAsync();

        var answer = await SetDefaultAsync(s.Structure, s.TeamId, s.Structure.Platform.Area.Id.ToString());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(s.Structure.Platform.Area.Id, answer.Body.GetProperty("defaultArea").GetProperty("id").GetGuid());
        Assert.Equal(s.Structure.Platform.Area.Id, DefaultOf(await ReadTreeAsync(s.Structure), s.TeamId).Id);
    }

    // US-4 · "setting, by a direct request, an area the team does not own explicitly is refused with «Область по умолчанию
    // должна принадлежать самой команде», and the default is unchanged": inherited, owned by another team, or the root.
    [Theory]
    [InlineData("inherited")]
    [InlineData("of another team")]
    [InlineData("the root")]
    public async Task Set_InheritedOrOtherTeamsArea_Returns409DefaultAreaNotOwned_Unchanged(string how)
    {
        var s = await CreateScenarioAsync();
        var core = await OrganizationsTestData.AddTeamAsync(factory, s.Structure.ProjectId, "Ядро");
        var gateway = await OrganizationsTestData.AddAreaAsync(factory, s.Structure.ProjectId, s.Api.Id, "Шлюз");
        var areaId = how switch
        {
            "inherited" => gateway.Id,
            "of another team" => core.Area.Id,
            _ => s.Structure.RootId,
        };

        var answer = await SetDefaultAsync(s.Structure, s.TeamId, areaId.ToString());

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.team.default_area_not_owned", answer.ErrorCode);
        Assert.Equal("Область по умолчанию должна принадлежать самой команде", answer.Detail);

        var tree = await ReadTreeAsync(s.Structure);
        Assert.Equal(s.Structure.Platform.Area.Id, DefaultOf(tree, s.TeamId).Id);
        Assert.Equal(core.Area.Id, DefaultOf(tree, core.Team.Id).Id);
        Assert.Equal(s.Structure.RootId, DefaultOf(tree, s.Structure.FirstTeamId).Id);
    }

    // US-4 · "The first team's default is the root, for good ... a direct request to change it is refused, and nothing
    // changes". A rule about the addressed team alone, so it answers before the body's area is looked at (api-contract § 0.4).
    [Theory]
    [InlineData("its current default")]
    [InlineData("another area it owns")]
    [InlineData("unknown")]
    [InlineData("malformed")]
    [InlineData("null")]
    public async Task Set_FirstTeam_Returns409FirstTeamDefaultFixed(string how)
    {
        var s = await CreateScenarioAsync();
        var reports = await OrganizationsTestData.AddAreaAsync(factory, s.Structure.ProjectId, s.Structure.RootId, "Отчёты", s.Structure.FirstTeamId);
        var areaId = how switch
        {
            "its current default" => s.Structure.RootId.ToString(),
            "another area it owns" => reports.Id.ToString(),
            "unknown" => Guid.CreateVersion7().ToString(),
            "malformed" => "not-an-area",
            _ => null,
        };

        var answer = await SetDefaultAsync(s.Structure, s.Structure.FirstTeamId, areaId);

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.team.first_team_default_fixed", answer.ErrorCode);
        Assert.Equal("У первой команды область по умолчанию не меняется", answer.Detail);

        var tree = await ReadTreeAsync(s.Structure);
        Assert.Equal(s.Structure.RootId, DefaultOf(tree, s.Structure.FirstTeamId).Id);
        Assert.Equal(s.Structure.Platform.Area.Id, DefaultOf(tree, s.TeamId).Id);
    }

    // BR-M1-22 · "an area of the team's own project": an area of another project of this organization, or of another
    // organization, answers area.not_found, and neither project changes.
    [Theory]
    [InlineData("of another project")]
    [InlineData("of another organization")]
    public async Task Set_AreaOfAnotherProject_Returns404AreaNotFound(string how)
    {
        var s = await CreateScenarioAsync();
        var secondProject = await OrganizationsTestData.CreateProjectAsync(factory, s.Structure.OrganizationId, "Второй проект", "SEC");
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var areaId = how == "of another project" ? secondProject.Root.Id : other.Platform.Area.Id;

        var answer = await SetDefaultAsync(s.Structure, s.TeamId, areaId.ToString());

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.area.not_found", answer.ErrorCode);
        Assert.Equal("Область не найдена.", answer.Detail);
        Assert.Equal(s.Structure.Platform.Area.Id, DefaultOf(await ReadTreeAsync(s.Structure), s.TeamId).Id);
        Assert.Equal(
            secondProject.Root.Id,
            DefaultOf(await OrganizationsTestData.ReadTreeAsync(factory, secondProject.Project.Id), secondProject.FirstTeam.Id).Id);
        Assert.Equal(other.Platform.Area.Id, DefaultOf(await ReadTreeAsync(other), other.Platform.Team.Id).Id);
    }

    // BR-M1-22 · api-contract § 3.5: an areaId that is null, absent, malformed or unknown answers area.not_found.
    [Theory]
    [InlineData("null")]
    [InlineData("absent")]
    [InlineData("malformed")]
    [InlineData("unknown")]
    public async Task Set_AreaNullAbsentMalformedOrUnknown_Returns404AreaNotFound_Unchanged(string how)
    {
        var s = await CreateScenarioAsync();
        object body = how switch
        {
            "null" => new { areaId = (string?)null },
            "absent" => new { },
            "malformed" => new { areaId = s.Api.Id.ToString("N") },
            _ => new { areaId = Guid.CreateVersion7().ToString() },
        };

        var answer = await SetDefaultAsync(s.Structure.OrganizationId, s.Structure.ProjectId.ToString(), s.TeamId.ToString(), body);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.area.not_found", answer.ErrorCode);
        Assert.Equal(s.Structure.Platform.Area.Id, DefaultOf(await ReadTreeAsync(s.Structure), s.TeamId).Id);
    }

    // api-contract § 0.2: an id field that is not a JSON string fails binding with the platform's 400.
    [Fact]
    public async Task Set_AreaIdIsANumber_Returns400BadRequest()
    {
        var s = await CreateScenarioAsync();

        var answer = await SetDefaultAsync(s.Structure.OrganizationId, s.Structure.ProjectId.ToString(), s.TeamId.ToString(), new { areaId = 42 });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("http.bad_request", answer.ErrorCode);
    }

    // US-1 · "A visitor who is not signed in sees nothing".
    [Fact]
    public async Task Set_NoToken_Returns401_ChangesNothing()
    {
        var s = await CreateScenarioAsync();

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Teams(s.Structure.OrganizationId, s.Structure.ProjectId)}/{s.TeamId}/default-area",
            new { areaId = s.Api.Id.ToString() });

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal(s.Structure.Platform.Area.Id, DefaultOf(await ReadTreeAsync(s.Structure), s.TeamId).Id);
    }

    // US-3 · "A project id names no project of the organization" (BR-M1-20, BR-M1-22): malformed, unknown, or of another
    // organization, the project level answers before the team and the body are looked at.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task Set_ProjectNotOfOrganization_Returns404ProjectNotFound(string how)
    {
        var s = await CreateScenarioAsync();
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var projectId = how switch
        {
            "malformed" => "PRM",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => other.ProjectId.ToString(),
        };

        var answer = await SetDefaultAsync(s.Structure.OrganizationId, projectId, "not-a-team", new { areaId = "not-an-area" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal("Проект не найден.", answer.Detail);
        Assert.Equal(s.Structure.Platform.Area.Id, DefaultOf(await ReadTreeAsync(s.Structure), s.TeamId).Id);
    }

    // US-4 · "A team identifier that names no team of the project" and US-7 · "A deleted team answers not found" (BR-M1-20,
    // BR-M1-22): malformed, unknown, deleted, or of another project or organization. The team level answers before the body.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("deleted")]
    [InlineData("of another project")]
    [InlineData("of another organization")]
    public async Task Set_TeamNotOfProject_Returns404TeamNotFound(string how)
    {
        var s = await CreateScenarioAsync();
        var secondProject = await OrganizationsTestData.CreateProjectAsync(factory, s.Structure.OrganizationId, "Второй проект", "SEC");
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var deleted = await OrganizationsTestData.AddTeamAsync(factory, s.Structure.ProjectId, "Ядро");
        await OrganizationsTestData.DeleteTeamAsync(factory, s.Structure.ProjectId, deleted.Team.Id);
        var teamId = how switch
        {
            "malformed" => s.TeamId.ToString("N"),
            "unknown" => Guid.CreateVersion7().ToString(),
            "deleted" => deleted.Team.Id.ToString(),
            "of another project" => secondProject.FirstTeam.Id.ToString(),
            _ => other.Platform.Team.Id.ToString(),
        };

        var answer = await SetDefaultAsync(
            s.Structure.OrganizationId, s.Structure.ProjectId.ToString(), teamId, new { areaId = "not-an-area" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.team.not_found", answer.ErrorCode);
        Assert.Equal("Команда не найдена.", answer.Detail);
        Assert.Equal(s.Structure.Platform.Area.Id, DefaultOf(await ReadTreeAsync(s.Structure), s.TeamId).Id);
    }

    // US-3 · "A project is deactivated": a write under it answers project.not_found and changes nothing (ADR-0026 rule 5).
    [Fact]
    public async Task Set_UnderDeactivatedProject_Returns404AndChangesNothing()
    {
        var s = await CreateScenarioAsync();
        await OrganizationsTestData.SetProjectStatusAsync(factory, s.Structure.OrganizationId, s.Structure.ProjectId, ProjectStatus.Deactivated);

        var answer = await SetDefaultAsync(s.Structure, s.TeamId, s.Api.Id.ToString());

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal(s.Structure.Platform.Area.Id, DefaultOf(await ReadTreeAsync(s.Structure), s.TeamId).Id);
    }

    // US-6 · "A re-own or a delete, and a default change, at the same moment": «Платформа / API» is set as the default of
    // «Платформа» and re-owned to «Пример проекта» at the same moment. Both lock the area FOR UPDATE, so one refuses the
    // other with its own code, in either order (plan-backend § 8.1):
    //   default first: the re-own waits, then finds the area is the team's default (area.is_team_default);
    //   re-own first: the default change waits, then finds the area is not the team's (team.default_area_not_owned).
    // Whichever order, the team's default is an existing area it owns explicitly.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Set_ConcurrentWithReown_OneRefused_BothOrders(bool defaultFirst)
    {
        var s = await CreateScenarioAsync();
        var firstTeamId = s.Structure.FirstTeamId;

        var (first, second) = defaultFirst
            ? await RaceAsync(gate => SetDefaultAsync(s.Structure, s.TeamId, s.Api.Id.ToString(), gate), () => ReownAsync(s.Structure, s.Api.Id, firstTeamId))
            : await RaceAsync(gate => ReownAsync(s.Structure, s.Api.Id, firstTeamId, gate), () => SetDefaultAsync(s.Structure, s.TeamId, s.Api.Id.ToString()));

        var tree = await ReadTreeAsync(s.Structure);
        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.Conflict, second.Status);

        if (defaultFirst)
        {
            Assert.Equal("organizations.area.is_team_default", second.ErrorCode);
            Assert.Equal("Платформа", second.Body.GetProperty("params").GetProperty("team").GetProperty("value").GetString());
            Assert.Equal(s.Api.Id, DefaultOf(tree, s.TeamId).Id);
            Assert.Equal(s.TeamId, tree[s.Api.Id].OwnerTeamId);
        }
        else
        {
            Assert.Equal("organizations.team.default_area_not_owned", second.ErrorCode);
            Assert.Equal(s.Structure.Platform.Area.Id, DefaultOf(tree, s.TeamId).Id);
            Assert.Equal(firstTeamId, tree[s.Api.Id].OwnerTeamId);
        }

        AssertDefaultIsAnExplicitlyOwnedArea(tree, s.TeamId);
    }

    // US-6 · the same moment for a delete and a default change: «Платформа / API», a leaf, is set as the default of
    // «Платформа» and is deleted at the same moment. Both lock the area FOR UPDATE:
    //   default first: the delete waits, then finds the area is a team's default (area.is_team_default), and the area stays;
    //   delete first: the default change waits, then finds no area (area.not_found), and the old default stays.
    // Whichever order, the team's default is an existing area it owns explicitly.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Set_ConcurrentWithDelete_OneRefused_BothOrders(bool defaultFirst)
    {
        var s = await CreateScenarioAsync();

        var (first, second) = defaultFirst
            ? await RaceAsync(gate => SetDefaultAsync(s.Structure, s.TeamId, s.Api.Id.ToString(), gate), () => DeleteAreaAsync(s.Structure, s.Api.Id))
            : await RaceAsync(gate => DeleteAreaAsync(s.Structure, s.Api.Id, gate), () => SetDefaultAsync(s.Structure, s.TeamId, s.Api.Id.ToString()));

        var tree = await ReadTreeAsync(s.Structure);

        if (defaultFirst)
        {
            Assert.Equal(HttpStatusCode.OK, first.Status);
            Assert.Equal(HttpStatusCode.Conflict, second.Status);
            Assert.Equal("organizations.area.is_team_default", second.ErrorCode);
            Assert.True(tree.Contains(s.Api.Id));
            Assert.Equal(s.Api.Id, DefaultOf(tree, s.TeamId).Id);
        }
        else
        {
            Assert.Equal(HttpStatusCode.NoContent, first.Status);
            Assert.Equal(HttpStatusCode.NotFound, second.Status);
            Assert.Equal("organizations.area.not_found", second.ErrorCode);
            Assert.False(tree.Contains(s.Api.Id));
            Assert.Equal(s.Structure.Platform.Area.Id, DefaultOf(tree, s.TeamId).Id);
        }

        AssertDefaultIsAnExplicitlyOwnedArea(tree, s.TeamId);
    }

    // plan-backend § 8.1 · "Two default changes of one team": the team FOR NO KEY UPDATE serializes them, the second waits
    // for the first to commit and then replaces it. Both answer 200, and the team ends with exactly one default.
    [Fact]
    public async Task Set_TwoConcurrentDefaultChanges_Serialized_SecondReplacesFirst()
    {
        var s = await CreateScenarioAsync();
        var reports = await OrganizationsTestData.AddAreaAsync(factory, s.Structure.ProjectId, s.Structure.RootId, "Отчёты", s.TeamId);

        var (first, second) = await RaceAsync(
            gate => SetDefaultAsync(s.Structure, s.TeamId, s.Api.Id.ToString(), gate),
            () => SetDefaultAsync(s.Structure, s.TeamId, reports.Id.ToString()));

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.Equal(s.Api.Id, first.Body.GetProperty("defaultArea").GetProperty("id").GetGuid());
        Assert.Equal(reports.Id, second.Body.GetProperty("defaultArea").GetProperty("id").GetGuid());

        var tree = await ReadTreeAsync(s.Structure);
        Assert.Equal(reports.Id, DefaultOf(tree, s.TeamId).Id);
        Assert.Null(tree[s.Api.Id].DefaultOfTeamId);
        Assert.Null(tree[s.Structure.Platform.Area.Id].DefaultOfTeamId);
        AssertDefaultIsAnExplicitlyOwnedArea(tree, s.TeamId);
    }

    private async Task<(Answer First, Answer Second)> RaceAsync(Func<CommitGate, Task<Answer>> first, Func<Task<Answer>> second)
    {
        using var gate = factory.CommitGates.Open();

        var firstRequest = first(gate);
        await Task.WhenAny(gate.Reached, firstRequest);
        Assert.False(firstRequest.IsCompleted, "The first request finished before reaching its commit gate.");

        var secondRequest = second();
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, secondRequest);
        Assert.False(secondRequest.IsCompleted, "The second request did not wait for the first one's locks.");

        gate.Release();

        return (await firstRequest, await secondRequest);
    }

    private static AreaNode DefaultOf(AreaTree tree, Guid teamId) =>
        Assert.Single(tree.PreOrder(), node => node.DefaultOfTeamId == teamId);

    private static void AssertDefaultIsAnExplicitlyOwnedArea(AreaTree tree, Guid teamId) =>
        Assert.Equal(teamId, DefaultOf(tree, teamId).OwnerTeamId);

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
}
