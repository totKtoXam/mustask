using System.Net;
using System.Text.Json;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/areas/{areaId}/owner</c>
/// (api-contract § 4.5, plan-backend § 14.3 lane L-24) through the real host and
/// a real PostgreSQL. The races of this route belong to the lanes of the other
/// side of each pair: the default change (L-19) and the team deletion (L-20).
/// Every test builds its own organization (<see cref="OrganizationsTestData"/>):
/// the database is shared and never emptied.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SetAreaOwnerEndpointTests(MustaskApiFactory factory)
{
    private const string RootIsFixedText = "Корневая область меняется только вместе с проектом";

    private Task<Answer> SetAsync(Guid organizationId, string projectId, string areaId, object body, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"/api/organizations/{organizationId}/projects/{Uri.EscapeDataString(projectId)}/areas/{Uri.EscapeDataString(areaId)}/owner",
            body,
            token ?? OrganizationsTokens.SignedIn());

    private Task<Answer> SetAsync(OrganizationStructure structure, string areaId, object body) =>
        SetAsync(structure.OrganizationId, structure.ProjectId.ToString(), areaId, body);

    private Task<Answer> SetAsync(OrganizationStructure structure, Guid areaId, Guid? ownerTeamId) =>
        SetAsync(structure, areaId.ToString(), new { ownerTeamId = ownerTeamId?.ToString() });

    // US-6 · "An area is re-owned": «Отчёты», owned by the first team and no team's default, with the child «Квартальные»
    // that has no explicit owner, is re-owned to «Платформа»; the answer is the Area shape, the tree shows both.
    [Fact]
    public async Task Set_ReownsAndChildrenInherit_Returns200()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты", structure.FirstTeamId);
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Квартальные");

        var answer = await SetAsync(structure, reports.Id, structure.Platform.Team.Id);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(["id", "name", "ownerTeamId", "parentId", "projectId"], PropertyNames(answer.Body));
        Assert.Equal(reports.Id, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal(structure.ProjectId, answer.Body.GetProperty("projectId").GetGuid());
        Assert.Equal(structure.RootId, answer.Body.GetProperty("parentId").GetGuid());
        Assert.Equal("Отчёты", answer.Body.GetProperty("name").GetString());
        Assert.Equal(structure.Platform.Team.Id, answer.Body.GetProperty("ownerTeamId").GetGuid());

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal(new ResolvedOwner(structure.Platform.Team.Id, false), tree.ResolveOwner(reports.Id));
        Assert.Equal(new ResolvedOwner(structure.Platform.Team.Id, true), tree.ResolveOwner(quarterly.Id));
        Assert.Null(tree[reports.Id].DefaultOfTeamId);
    }

    // US-6 · "An explicit owner is cleared": «Отчёты», owned by «Платформа» and no team's default, directly under the root,
    // loses its owner whether the body says null or leaves the field out; it and its children resolve to the first team,
    // the root's owner, marked inherited.
    [Theory]
    [InlineData("null")]
    [InlineData("absent")]
    public async Task Set_Clear_ResolvesToRootOwner_Inherited(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты", structure.Platform.Team.Id);
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Квартальные");
        object body = how == "null" ? (object)new { ownerTeamId = (string?)null } : new { };

        var answer = await SetAsync(structure, reports.Id.ToString(), body);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(JsonValueKind.Null, answer.Body.GetProperty("ownerTeamId").ValueKind);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Null(tree[reports.Id].OwnerTeamId);
        Assert.Equal(new ResolvedOwner(structure.FirstTeamId, true), tree.ResolveOwner(reports.Id));
        Assert.Equal(new ResolvedOwner(structure.FirstTeamId, true), tree.ResolveOwner(quarterly.Id));
    }

    // api-contract § 4.5: "Setting the current owner again answers 200 and changes nothing"; clearing an area that has no
    // explicit owner does the same.
    [Theory]
    [InlineData("the same owner")]
    [InlineData("clearing an inheriting area")]
    public async Task Set_NothingToChange_Returns200_ChangesNothing(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var owner = how == "the same owner" ? structure.Platform.Team.Id : (Guid?)null;
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты", owner);

        var answer = await SetAsync(structure, reports.Id, owner);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var returned = answer.Body.GetProperty("ownerTeamId");
        Assert.Equal(owner, returned.ValueKind == JsonValueKind.Null ? (Guid?)null : returned.GetGuid());
        Assert.Equal(owner, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId))[reports.Id].OwnerTeamId);
    }

    // US-6 · "The root's owner is fixed": re-owning and clearing, requested directly, are refused, and the root stays owned
    // by the first team (BR-M1-11). The root answers before the body's team is looked at: «Ядро» does not exist.
    [Theory]
    [InlineData("re-own")]
    [InlineData("clear")]
    [InlineData("re-own to an unknown team")]
    public async Task Set_Root_Returns409RootIsFixed(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var ownerTeamId = how switch
        {
            "re-own" => structure.Platform.Team.Id.ToString(),
            "clear" => null,
            _ => Guid.CreateVersion7().ToString(),
        };

        var answer = await SetAsync(structure, structure.RootId.ToString(), new { ownerTeamId });

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.area.root_is_fixed", answer.ErrorCode);
        Assert.Equal(RootIsFixedText, answer.Detail);
        Assert.Equal(structure.FirstTeamId, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Root.OwnerTeamId);
    }

    // US-6 · "A team's default area stays with its team": «Платформа», the default area of the team «Платформа», is refused a
    // re-own and a clear with the team's name as the parameter (BR-M1-14), and keeps its owner and its default mark.
    [Theory]
    [InlineData("re-own")]
    [InlineData("clear")]
    public async Task Set_ReownOrClearADefaultArea_Returns409IsTeamDefault(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await SetAsync(structure, structure.Platform.Area.Id, how == "re-own" ? structure.FirstTeamId : (Guid?)null);

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.area.is_team_default", answer.ErrorCode);
        var team = answer.Body.GetProperty("params").GetProperty("team");
        Assert.Equal("Платформа", team.GetProperty("value").GetString());
        Assert.Equal("string", team.GetProperty("type").GetString());
        Assert.Equal("Область используется как область по умолчанию команды «Платформа»", answer.Detail);

        var area = (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId))[structure.Platform.Area.Id];
        Assert.Equal(structure.Platform.Team.Id, area.OwnerTeamId);
        Assert.Equal(structure.Platform.Team.Id, area.DefaultOfTeamId);
    }

    // US-6 · "A team's default area stays with its team": the owner it already has is no change, so a default area set to
    // its own team answers 200, and stays the team's default.
    [Fact]
    public async Task Set_DefaultAreaToItsOwnTeam_Returns200_DefaultKept()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await SetAsync(structure, structure.Platform.Area.Id, structure.Platform.Team.Id);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var area = (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId))[structure.Platform.Area.Id];
        Assert.Equal(structure.Platform.Team.Id, area.OwnerTeamId);
        Assert.Equal(structure.Platform.Team.Id, area.DefaultOfTeamId);
    }

    // US-6 · "…once the team's default is changed to «Отчёты», the same actions are allowed": the former default is re-owned
    // and cleared at will, and «Отчёты», the new default, is refused instead.
    [Fact]
    public async Task Set_AfterDefaultMoved_Allowed_ForTheFormerDefault_RefusedForTheNewOne()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты", structure.Platform.Team.Id);
        Assert.True(await OrganizationsTestData.SetTeamDefaultAreaAsync(factory, structure.ProjectId, structure.Platform.Team.Id, reports.Id));

        var reowned = await SetAsync(structure, structure.Platform.Area.Id, structure.FirstTeamId);
        var cleared = await SetAsync(structure, structure.Platform.Area.Id, null);
        var refused = await SetAsync(structure, reports.Id, structure.FirstTeamId);

        Assert.Equal(HttpStatusCode.OK, reowned.Status);
        Assert.Equal(structure.FirstTeamId, reowned.Body.GetProperty("ownerTeamId").GetGuid());
        Assert.Equal(HttpStatusCode.OK, cleared.Status);
        Assert.Equal(JsonValueKind.Null, cleared.Body.GetProperty("ownerTeamId").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        Assert.Equal("organizations.area.is_team_default", refused.ErrorCode);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Null(tree[structure.Platform.Area.Id].DefaultOfTeamId);
        Assert.Equal(structure.Platform.Team.Id, tree[reports.Id].OwnerTeamId);
        Assert.Equal(structure.Platform.Team.Id, tree[reports.Id].DefaultOfTeamId);
    }

    // US-6 · "An area's owner is a team of the area's own project" (BR-M1-22) and US-7 · "A deleted team answers not found":
    // a team that is malformed, unknown, deleted, or of another project or organization answers team.not_found, and the
    // area keeps its owner.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("deleted")]
    [InlineData("of another project")]
    [InlineData("of another organization")]
    public async Task Set_TeamOfAnotherProjectOrOrganizationOrDeleted_Returns404TeamNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var secondProject = await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var deleted = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Ядро");
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, deleted.Team.Id);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты", structure.FirstTeamId);
        var ownerTeamId = how switch
        {
            "malformed" => structure.Platform.Team.Id.ToString("N"),
            "unknown" => Guid.CreateVersion7().ToString(),
            "deleted" => deleted.Team.Id.ToString(),
            "of another project" => secondProject.FirstTeam.Id.ToString(),
            _ => other.Platform.Team.Id.ToString(),
        };

        var answer = await SetAsync(structure, reports.Id.ToString(), new { ownerTeamId });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.team.not_found", answer.ErrorCode);
        Assert.Equal("Команда не найдена.", answer.Detail);
        Assert.Equal(structure.FirstTeamId, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId))[reports.Id].OwnerTeamId);
    }

    // US-5 · "A node never leaves its project" / BR-M1-22: an area that is malformed, unknown, or of another project, the
    // root included, answers area.not_found, before the body's team is looked at, and the other project is untouched.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another project")]
    [InlineData("the root of another project")]
    public async Task Set_AreaNotOfProject_Returns404AreaNotFound_BeforeTheTeam(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var secondProject = await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");
        var foreign = await OrganizationsTestData.AddAreaAsync(factory, secondProject.Project.Id, secondProject.Root.Id, "Чужая", secondProject.FirstTeam.Id);
        var areaId = how switch
        {
            "malformed" => "{" + foreign.Id + "}",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another project" => foreign.Id.ToString(),
            _ => secondProject.Root.Id.ToString(),
        };

        var answer = await SetAsync(structure, areaId, new { ownerTeamId = "not-a-team" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.area.not_found", answer.ErrorCode);
        Assert.Equal("Область не найдена.", answer.Detail);

        var secondTree = await OrganizationsTestData.ReadTreeAsync(factory, secondProject.Project.Id);
        Assert.Equal(secondProject.FirstTeam.Id, secondTree[foreign.Id].OwnerTeamId);
        Assert.Equal(secondProject.FirstTeam.Id, secondTree.Root.OwnerTeamId);
    }

    // US-1 · "A visitor who is not signed in sees nothing".
    [Fact]
    public async Task Set_NoToken_Returns401_ChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты", structure.FirstTeamId);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Areas(structure.OrganizationId, structure.ProjectId)}/{reports.Id}/owner",
            new { ownerTeamId = structure.Platform.Team.Id.ToString() });

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal(structure.FirstTeamId, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId))[reports.Id].OwnerTeamId);
    }

    // US-3 · "A project id names no project of the organization" (BR-M1-20, BR-M1-22): malformed, unknown, or of another
    // organization, the project level answers before the area and the body are looked at.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task Set_ProjectNotOfOrganization_Returns404ProjectNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var projectId = how switch
        {
            "malformed" => "PRM",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => other.ProjectId.ToString(),
        };

        var answer = await SetAsync(structure.OrganizationId, projectId, other.Platform.Area.Id.ToString(), new { ownerTeamId = "not-a-team" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal("Проект не найден.", answer.Detail);
        Assert.Equal(other.Platform.Team.Id, (await OrganizationsTestData.ReadTreeAsync(factory, other.ProjectId))[other.Platform.Area.Id].OwnerTeamId);
    }

    // US-3 · "A project is deactivated": a write under it answers project.not_found and changes nothing (ADR-0026 rule 5).
    [Fact]
    public async Task Set_UnderDeactivatedProject_Returns404AndChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты", structure.FirstTeamId);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);

        var answer = await SetAsync(structure, reports.Id, structure.Platform.Team.Id);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal(structure.FirstTeamId, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId))[reports.Id].OwnerTeamId);
    }

    // api-contract § 0.2: an id field that is not a JSON string fails binding with the platform's 400.
    [Fact]
    public async Task Set_OwnerTeamIdIsANumber_Returns400BadRequest()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты", structure.FirstTeamId);

        var answer = await SetAsync(structure, reports.Id.ToString(), new { ownerTeamId = 42 });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("http.bad_request", answer.ErrorCode);
        Assert.Equal(structure.FirstTeamId, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId))[reports.Id].OwnerTeamId);
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
}
