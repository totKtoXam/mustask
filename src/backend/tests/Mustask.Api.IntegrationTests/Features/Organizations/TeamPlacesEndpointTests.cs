using System.Net;
using System.Text.Json;
using Mustask.Domain.Identity;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// mod-021 api-contract through the real host and a real PostgreSQL: a team stands in one department (<c>POST /teams</c> with
/// <c>departmentId</c>, <c>PUT /teams/{id}/department</c>), the department refuses its deletion while it holds a team, and the
/// five department–team link routes are gone. Every test builds its own organization.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class TeamPlacesEndpointTests(MustaskApiFactory factory)
{
    private static string Move(Guid organizationId, Guid teamId) => $"{OrganizationsApi.OrgTeams(organizationId)}/{teamId}/department";

    private static string FieldReason(Answer answer, string field) =>
        answer.Body.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString()!;

    private Task<Answer> SendAsync(HttpMethod method, string path, object? body, string? token = null) =>
        OrganizationsApi.SendAsync(factory, method, path, body, token ?? OrganizationsTokens.SignedIn());

    // US-1 · a team is created in a department: the answer names the place and the colour the palette gives the first team of a blue department.
    [Fact]
    public async Task Create_InADepartment_StandsThere_WithTheFirstColour()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");

        var answer = await SendAsync(HttpMethod.Post, OrganizationsApi.OrgTeams(organization.Id), new { name = "MARS", departmentId = department.Id.ToString() });

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(["color", "departmentId", "departmentName", "description", "id", "name"], PropertyNames(answer.Body));
        Assert.Equal(department.Id, answer.Body.GetProperty("departmentId").GetGuid());
        Assert.Equal("Разработка", answer.Body.GetProperty("departmentName").GetString());
        Assert.Equal("orange", answer.Body.GetProperty("color").GetString());
        var stored = await StoredTeamAsync(factory, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal((department.Id, "orange"), (stored.DepartmentId, stored.Color));
    }

    // US-1 · the second and the third team of a department take the colours furthest from those in use.
    [Fact]
    public async Task Create_SeveralInOneDepartment_TakeTheNextColours()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var colours = new List<string>();

        foreach (var name in new[] { "A", "B", "C" })
        {
            var answer = await SendAsync(HttpMethod.Post, OrganizationsApi.OrgTeams(organization.Id), new { name, departmentId = department.Id.ToString() });
            colours.Add(answer.Body.GetProperty("color").GetString()!);
        }

        Assert.Equal(["orange", "magenta", "green"], colours);
    }

    // US-1 · a department is required, and the reason is on its field next to the other fields' reasons.
    [Fact]
    public async Task Create_WithoutADepartment_Is400_OnTheDepartmentField()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await SendAsync(HttpMethod.Post, OrganizationsApi.OrgTeams(organization.Id), new { name = "" });

        Assert.Equal((HttpStatusCode.BadRequest, "organizations.validation_failed"), (answer.Status, answer.ErrorCode));
        Assert.Equal("organizations.team.department_required", FieldReason(answer, "departmentId"));
        Assert.True(answer.Body.GetProperty("errors").TryGetProperty("name", out _));
        Assert.Empty(await StoredTeamsAsync(factory, organization.Id));
    }

    // US-1 · an unknown, foreign or deleted department is 404; a project node or a sub-area is 409.
    [Fact]
    public async Task Create_InAnUnknownOrForeignDepartment_Is404_InANodeOrSubArea_Is409()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var foreign = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var foreignDepartment = await OrganizationsTestData.AddDepartmentAsync(factory, foreign.Id, "Чужой");

        foreach (var id in new[] { Guid.CreateVersion7(), foreignDepartment.Id })
        {
            var answer = await SendAsync(HttpMethod.Post, OrganizationsApi.OrgTeams(structure.OrganizationId), new { name = "MARS", departmentId = id.ToString() });
            Assert.Equal((HttpStatusCode.NotFound, "organizations.area.not_found"), (answer.Status, answer.ErrorCode));
        }

        var node = await SendAsync(HttpMethod.Post, OrganizationsApi.OrgTeams(structure.OrganizationId), new { name = "MARS", departmentId = structure.RootId.ToString() });
        Assert.Equal((HttpStatusCode.Conflict, "organizations.area.type_not_allowed"), (node.Status, node.ErrorCode));
        Assert.Equal(2, (await StoredTeamsAsync(factory, structure.OrganizationId)).Count);
    }

    // US-3 · an unlinked team moves; its colour stays.
    [Fact]
    public async Task Move_UnlinkedTeam_ChangesPlace_KeepsColour()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var source = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var destination = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Продажи");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, source.Id, "MARS", "green");

        var answer = await SendAsync(HttpMethod.Put, Move(organization.Id, team.Id), new { departmentId = destination.Id.ToString() });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(["color", "departmentId", "departmentName", "description", "id", "name"], PropertyNames(answer.Body));
        Assert.Equal((destination.Id, "Продажи", "green"), (
            answer.Body.GetProperty("departmentId").GetGuid(), answer.Body.GetProperty("departmentName").GetString(), answer.Body.GetProperty("color").GetString()));
        var stored = await StoredTeamAsync(factory, team.Id);
        Assert.Equal((destination.Id, "green"), (stored.DepartmentId, stored.Color));
    }

    // US-3 · "a linked team does not move": 409 team.has_links, nothing changes; the same department is a no-op even then.
    [Fact]
    public async Task Move_LinkedTeam_Is409HasLinks_TheSameDepartmentIsANoOp()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var destination = await OrganizationsTestData.AddDepartmentAsync(factory, structure.OrganizationId, "Продажи");
        var team = structure.Platform;
        var before = (await StoredTeamAsync(factory, team.Id)).DepartmentId;

        var refused = await SendAsync(HttpMethod.Put, Move(structure.OrganizationId, team.Id), new { departmentId = destination.Id.ToString() });
        var same = await SendAsync(HttpMethod.Put, Move(structure.OrganizationId, team.Id), new { departmentId = before.ToString() });

        Assert.Equal((HttpStatusCode.Conflict, "organizations.team.has_links"), (refused.Status, refused.ErrorCode));
        Assert.Equal(HttpStatusCode.OK, same.Status);
        Assert.Equal(before, (await StoredTeamAsync(factory, team.Id)).DepartmentId);
    }

    [Fact]
    public async Task Move_BadRequests_AreTheContractsAnswers()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var source = await OrganizationsTestData.AddDepartmentAsync(factory, structure.OrganizationId, "Разработка");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, structure.OrganizationId, source.Id, "MARS");
        var path = Move(structure.OrganizationId, team.Id);

        var missing = await SendAsync(HttpMethod.Put, path, new { });
        var unknown = await SendAsync(HttpMethod.Put, path, new { departmentId = Guid.CreateVersion7().ToString() });
        var malformed = await SendAsync(HttpMethod.Put, path, new { departmentId = "not-an-id" });
        var node = await SendAsync(HttpMethod.Put, path, new { departmentId = structure.RootId.ToString() });
        var noTeam = await SendAsync(HttpMethod.Put, Move(structure.OrganizationId, Guid.CreateVersion7()), new { departmentId = source.Id.ToString() });

        Assert.Equal(HttpStatusCode.BadRequest, missing.Status);
        Assert.Equal("organizations.team.department_required", FieldReason(missing, "departmentId"));
        Assert.Equal("organizations.area.not_found", unknown.ErrorCode);
        Assert.Equal("organizations.area.not_found", malformed.ErrorCode);
        Assert.Equal((HttpStatusCode.Conflict, "organizations.area.type_not_allowed"), (node.Status, node.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (noTeam.Status, noTeam.ErrorCode));
        Assert.Equal(source.Id, (await StoredTeamAsync(factory, team.Id)).DepartmentId);
    }

    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Move_MemberAndViewer_Return403_NoToken_401(OrganizationRole role)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var source = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var destination = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Продажи");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, source.Id, "MARS");
        var caller = await MembershipTestData.NewMemberAsync(factory, organization.Id, role);
        var body = new { departmentId = destination.Id.ToString() };

        var forbidden = await SendAsync(HttpMethod.Put, Move(organization.Id, team.Id), body, caller.Token);
        var anonymous = await OrganizationsApi.SendAsync(factory, HttpMethod.Put, Move(organization.Id, team.Id), body);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
        Assert.Equal(source.Id, (await StoredTeamAsync(factory, team.Id)).DepartmentId);
    }

    // US-4 · a department holding an active team refuses its deletion, and the refusal wins over not_empty; a deleted team never blocks.
    [Fact]
    public async Task DeleteDepartment_HoldingATeam_Is409HasTeams_AfterTheTeamIsDeleted_204()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, department.Id, "MARS");
        var path = $"{OrganizationsApi.Areas(organization.Id)}/{department.Id}";

        var refused = await SendAsync(HttpMethod.Delete, path, null);
        await DeleteTeamAsync(factory, organization.Id, team.Id);
        var allowed = await SendAsync(HttpMethod.Delete, path, null);

        Assert.Equal((HttpStatusCode.Conflict, "organizations.area.has_teams"), (refused.Status, refused.ErrorCode));
        Assert.Equal(HttpStatusCode.NoContent, allowed.Status);
    }

    // US-2 · the five department–team link routes are gone: a path that does not exist, whoever asks, with a body that was once valid.
    [Theory]
    [InlineData("GET", "area-teams")]
    [InlineData("POST", "area-teams")]
    [InlineData("DELETE", "area-teams")]
    [InlineData("GET", "team-areas")]
    [InlineData("POST", "team-areas")]
    public async Task RetiredLinkRoutes_AnswerAsPathsThatDoNotExist(string method, string which)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, department.Id, "MARS");
        var path = which == "area-teams"
            ? method == "DELETE" ? $"{OrganizationsApi.Areas(organization.Id)}/{department.Id}/teams/{team.Id}" : $"{OrganizationsApi.Areas(organization.Id)}/{department.Id}/teams"
            : OrganizationsApi.TeamAreas(organization.Id, team.Id);
        object? body = method == "POST" ? (which == "area-teams" ? new { teamIds = new[] { team.Id.ToString() } } : new { areaIds = new[] { department.Id.ToString() } }) : null;

        var answer = await SendAsync(new HttpMethod(method), path, body);

        // GET .../areas/{id}/teams is the department's team list and stays (api-contract § GET /areas/{areaId}/teams).
        if (method == "GET" && which == "area-teams")
        {
            Assert.Equal(HttpStatusCode.OK, answer.Status);
            Assert.Equal(team.Id, answer.Body[0].GetProperty("id").GetGuid());
            Assert.Equal(JsonValueKind.String, answer.Body[0].GetProperty("color").ValueKind);
            Assert.False(answer.Body[0].TryGetProperty("canUnlink", out _));
        }
        else
        {
            Assert.Contains(answer.Status, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
            Assert.Equal(department.Id, (await StoredTeamAsync(factory, team.Id)).DepartmentId);
        }
    }

    // BR-TD-20 · departments, and projects with no department, are coloured in order by the palette; a sub-area has no colour of its own.
    [Fact]
    public async Task Create_DepartmentsAndProjects_TakeTheNextPaletteColours_ASubAreaNone()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var colours = new List<string?>();

        foreach (var name in new[] { "Разработка", "Продажи", "Финансы" })
        {
            var department = await SendAsync(HttpMethod.Post, OrganizationsApi.Areas(organization.Id), new { name });
            colours.Add(department.Body.GetProperty("color").GetString());
        }

        var projectColours = new List<string?>();

        foreach (var (name, key) in new[] { ("Альфа", "ALF"), ("Бета", "BET") })
        {
            var project = await SendAsync(HttpMethod.Post, $"{OrganizationsApi.Organization(organization.Id)}/projects", new { name, key });
            projectColours.Add(project.Body.GetProperty("color").GetString());
        }

        var node = await OrganizationsTestData.NodeOfAsync(factory, (await OrganizationsTestData.CreateProjectAsync(factory, organization.Id)).Project.Id);
        var sub = await SendAsync(HttpMethod.Post, OrganizationsApi.Areas(organization.Id), new { name = "Отчёты", parentId = node.Id.ToString() });

        Assert.Equal(["blue", "orange", "magenta"], colours);
        Assert.Equal(["blue", "orange"], projectColours);
        Assert.Equal(JsonValueKind.Null, sub.Body.GetProperty("color").ValueKind);
    }
}
