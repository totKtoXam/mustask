using System.Net;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// Backend QA of mod-021 (doc-016 BR-TD-1, BR-TD-4 team part, BR-TD-5, BR-TD-20), written from the spec and the contract before the
/// developer's tests were read: the gaps they left. Deletion precedence, the colour rule across deletions, moves and kinds, the refusals
/// of a move, the retired routes, and the races that decide a team's place. Every test builds its own organization.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class TeamPlacesQaTests(MustaskApiFactory factory)
{
    private static string Move(Guid organizationId, Guid teamId) => $"{OrganizationsApi.OrgTeams(organizationId)}/{teamId}/department";

    private Task<Answer> SendAsync(HttpMethod method, string path, object? body, string? token = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(factory, method, path, body, token ?? OrganizationsTokens.SignedIn(), gate);

    private async Task<string> CreateTeamColourAsync(Guid organizationId, Guid departmentId, string name)
    {
        var answer = await SendAsync(HttpMethod.Post, OrganizationsApi.OrgTeams(organizationId), new { name, departmentId = departmentId.ToString() });
        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return answer.Body.GetProperty("color").GetString()!;
    }

    // BR-TD-5 · the team answer comes first when a department holds a team and a project; a project alone keeps not_empty; nothing is deleted.
    [Fact]
    public async Task DeleteDepartment_WithATeamAndAProject_AnswersHasTeams_WithAProjectAlone_NotEmpty()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var both = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        var projectOnly = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Логистика");
        await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, both.Id, "JUPITER");

        foreach (var (department, key) in new[] { (both, "CCC"), (projectOnly, "DDD") })
        {
            var project = await SendAsync(
                HttpMethod.Post, $"{OrganizationsApi.Organization(organization.Id)}/projects",
                new { name = "P" + key, key, parentAreaId = department.Id.ToString() });
            Assert.Equal(HttpStatusCode.Created, project.Status);
        }

        var withTeam = await SendAsync(HttpMethod.Delete, $"{OrganizationsApi.Areas(organization.Id)}/{both.Id}", null);
        var withProject = await SendAsync(HttpMethod.Delete, $"{OrganizationsApi.Areas(organization.Id)}/{projectOnly.Id}", null);

        Assert.Equal((HttpStatusCode.Conflict, "organizations.area.has_teams"), (withTeam.Status, withTeam.ErrorCode));
        Assert.Equal((HttpStatusCode.Conflict, "organizations.area.not_empty"), (withProject.Status, withProject.ErrorCode));
        var deletion = await GetAsync(factory, $"{OrganizationsApi.Areas(organization.Id)}/{both.Id}/deletion");
        Assert.Equal(HttpStatusCode.OK, deletion.Status);
        Assert.Single(deletion.Body.GetProperty("teams").EnumerateArray());
    }

    // BR-TD-1 · deleting a team clears its department in the database (a deleted team keeps none).
    [Fact]
    public async Task DeleteTeam_ClearsItsDepartment()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, department.Id, "MARS");

        var deleted = await DeleteTeamAsync(factory, organization.Id, team.Id);

        Assert.Equal(HttpStatusCode.NoContent, deleted.Status);
        var stored = await StoredTeamAsync(factory, team.Id);
        Assert.Equal((TeamStatus.Deleted, (Guid?)null), (stored.Status, stored.DepartmentId));
    }

    // BR-TD-20 · a deleted team frees its colour: A, B, C take orange, magenta, green; B goes; the next takes magenta again.
    [Fact]
    public async Task Colour_OfADeletedTeam_IsFreed()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        await CreateTeamColourAsync(organization.Id, department.Id, "A");
        var second = await SendAsync(HttpMethod.Post, OrganizationsApi.OrgTeams(organization.Id), new { name = "B", departmentId = department.Id.ToString() });
        await CreateTeamColourAsync(organization.Id, department.Id, "C");

        await DeleteTeamAsync(factory, organization.Id, second.Body.GetProperty("id").GetGuid());

        Assert.Equal("magenta", await CreateTeamColourAsync(organization.Id, department.Id, "D"));
    }

    // BR-TD-20 · a team that moves keeps its colour and leaves its old scope: the next team there may take the colour it freed.
    [Fact]
    public async Task Colour_OfAMovedTeam_IsKept_AndFreedInTheOldDepartment()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var source = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Логистика");
        var destination = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        await CreateTeamColourAsync(organization.Id, source.Id, "MARS");
        await CreateTeamColourAsync(organization.Id, source.Id, "VENUS");
        var saturn = await SendAsync(HttpMethod.Post, OrganizationsApi.OrgTeams(organization.Id), new { name = "SATURN", departmentId = source.Id.ToString() });
        Assert.Equal("green", saturn.Body.GetProperty("color").GetString());

        var moved = await SendAsync(HttpMethod.Put, Move(organization.Id, saturn.Body.GetProperty("id").GetGuid()), new { departmentId = destination.Id.ToString() });

        Assert.Equal("green", moved.Body.GetProperty("color").GetString());
        Assert.Equal("green", await CreateTeamColourAsync(organization.Id, source.Id, "NEPTUNE"));
    }

    // BR-TD-20 · a department's own colour counts for teams and for projects, and the kinds are scoped apart.
    [Fact]
    public async Task Colour_OfProjectsAndTeams_NeverTheDepartmentsOwn_KindsScopedApart()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Логистика");
        var projectColours = new List<string>();

        foreach (var (name, key) in new[] { ("Альфа", "ALF"), ("Бета", "BET") })
        {
            var project = await SendAsync(
                HttpMethod.Post, $"{OrganizationsApi.Organization(organization.Id)}/projects",
                new { name, key, parentAreaId = department.Id.ToString() });
            projectColours.Add(project.Body.GetProperty("color").GetString()!);
        }

        Assert.Equal(["orange", "magenta"], projectColours);
        Assert.Equal("orange", await CreateTeamColourAsync(organization.Id, department.Id, "MARS"));
    }

    // BR-TD-20 · every one of the 12 entries in use: the 13th team takes the entry used by fewest, ties by palette order (blue, the department's).
    [Fact]
    public async Task Colour_WhenEveryEntryIsInUse_TakesTheLeastUsed_ByPaletteOrder()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var used = new List<string> { "blue" };

        for (var index = 1; index <= 14; index++)
        {
            var expected = ColorPalette.Choose(used);
            var colour = await CreateTeamColourAsync(organization.Id, department.Id, $"T{index:00}");
            Assert.Equal(expected, colour);
            used.Add(colour);
        }

        Assert.True(used.Distinct().Count() == 12);
    }

    // BR-TD-4 · a move that cannot happen changes nothing: deleted team, foreign team, foreign department, deleted department, sub-area.
    [Fact]
    public async Task Move_Refusals_Are404Or409_AndChangeNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var source = await OrganizationsTestData.AddDepartmentAsync(factory, structure.OrganizationId, "Разработка");
        var gone = await OrganizationsTestData.AddDepartmentAsync(factory, structure.OrganizationId, "Исчезнувший");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, structure.OrganizationId, source.Id, "MARS");
        var deletedTeam = await OrganizationsTestData.AddTeamInAsync(factory, structure.OrganizationId, source.Id, "OLD");
        await DeleteTeamAsync(factory, structure.OrganizationId, deletedTeam.Id);
        await SendAsync(HttpMethod.Delete, $"{OrganizationsApi.Areas(structure.OrganizationId)}/{gone.Id}", null);
        var other = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var otherDepartment = await OrganizationsTestData.AddDepartmentAsync(factory, other.Id, "Чужой");
        var otherTeam = await OrganizationsTestData.AddTeamInAsync(factory, other.Id, otherDepartment.Id, "FOREIGN");
        var sub = await OrganizationsTestData.AddSubAreaAsync(factory, structure.OrganizationId, (await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId)).Id, "Отчёты");

        var deleted = await SendAsync(HttpMethod.Put, Move(structure.OrganizationId, deletedTeam.Id), new { departmentId = source.Id.ToString() });
        var foreignTeam = await SendAsync(HttpMethod.Put, Move(structure.OrganizationId, otherTeam.Id), new { departmentId = source.Id.ToString() });
        var foreignDepartment = await SendAsync(HttpMethod.Put, Move(structure.OrganizationId, team.Id), new { departmentId = otherDepartment.Id.ToString() });
        var deletedDepartment = await SendAsync(HttpMethod.Put, Move(structure.OrganizationId, team.Id), new { departmentId = gone.Id.ToString() });
        var subArea = await SendAsync(HttpMethod.Put, Move(structure.OrganizationId, team.Id), new { departmentId = sub.Id.ToString() });
        var nullBody = await SendAsync(HttpMethod.Put, Move(structure.OrganizationId, team.Id), new { departmentId = (string?)null });

        Assert.Equal("organizations.team.not_found", deleted.ErrorCode);
        Assert.Equal("organizations.team.not_found", foreignTeam.ErrorCode);
        Assert.Equal("organizations.area.not_found", foreignDepartment.ErrorCode);
        Assert.Equal("organizations.area.not_found", deletedDepartment.ErrorCode);
        Assert.Equal((HttpStatusCode.Conflict, "organizations.area.type_not_allowed"), (subArea.Status, subArea.ErrorCode));
        Assert.Equal((HttpStatusCode.BadRequest, "organizations.validation_failed"), (nullBody.Status, nullBody.ErrorCode));
        Assert.Equal(source.Id, (await StoredTeamAsync(factory, team.Id)).DepartmentId);
        Assert.Null((await StoredTeamAsync(factory, deletedTeam.Id)).DepartmentId);
        Assert.Equal(otherDepartment.Id, (await StoredTeamAsync(factory, otherTeam.Id)).DepartmentId);
    }

    // BR-TD-4 · the move is audited by nothing else: members stay, and a non-member of the organization learns only «Организация не найдена».
    [Fact]
    public async Task Move_KeepsMembers_AndAStrangerGets404()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var source = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var destination = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Продажи");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, source.Id, "MARS");
        var member = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);
        await MembershipTestData.AddTeamMemberAsync(factory, organization.Id, team.Id, member.Id);
        var stranger = await MembershipTestData.NewMemberAsync(factory, (await OrganizationsTestData.CreateOrganizationAsync(factory)).Id, OrganizationRole.Admin);

        var refused = await SendAsync(HttpMethod.Put, Move(organization.Id, team.Id), new { departmentId = destination.Id.ToString() }, stranger.Token);
        var moved = await SendAsync(HttpMethod.Put, Move(organization.Id, team.Id), new { departmentId = destination.Id.ToString() });

        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (refused.Status, refused.ErrorCode));
        Assert.Equal(HttpStatusCode.OK, moved.Status);
        Assert.Equal(new[] { member.Id }, await TeamMemberIdsAsync(factory, team.Id));
    }

    // Retired: the one route the developer's theory did not name, and the retired body fields are ignored, not honoured.
    [Fact]
    public async Task RetiredUnlinkRoute_IsGone_AndRetiredBodyFieldsAreIgnored()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var other = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Продажи");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, department.Id, "MARS");

        var unlink = await SendAsync(HttpMethod.Delete, $"{OrganizationsApi.TeamAreas(organization.Id, team.Id)}/{department.Id}", null);
        var created = await SendAsync(
            HttpMethod.Post, OrganizationsApi.OrgTeams(organization.Id),
            new { name = "VENUS", departmentId = department.Id.ToString(), areaIds = new[] { other.Id.ToString() } });
        var area = await SendAsync(HttpMethod.Post, OrganizationsApi.Areas(organization.Id), new { name = "Новый", teamIds = new[] { team.Id.ToString() } });

        Assert.Contains(unlink.Status, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal(department.Id, created.Body.GetProperty("departmentId").GetGuid());
        Assert.Equal(HttpStatusCode.Created, area.Status);
        Assert.Equal(department.Id, (await StoredTeamAsync(factory, team.Id)).DepartmentId);
        var listed = await GetAsync(factory, $"{OrganizationsApi.Areas(organization.Id)}/{area.Body.GetProperty("id").GetGuid()}/teams");
        Assert.Empty(listed.Body.EnumerateArray());
    }

    // Race · a move against the deletion of the team itself, both orders: no 5xx, a deleted team keeps no department.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MoveTeam_VersusTheDeletionOfTheTeam_BothOrders_NoServerError_NoDepartmentOnADeletedTeam(bool moveFirst)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var source = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var destination = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Продажи");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, source.Id, "SATURN");

        Task<Answer> MoveIt(CommitGate? at) => SendAsync(HttpMethod.Put, Move(organization.Id, team.Id), new { departmentId = destination.Id.ToString() }, gate: at);
        Task<Answer> Remove(CommitGate? at) => DeleteTeamAsync(factory, organization.Id, team.Id, gate: at);

        var (first, second) = moveFirst ? await RaceAsync(factory, MoveIt, Remove) : await RaceAsync(factory, Remove, MoveIt);
        var (move, delete) = moveFirst ? (first, second) : (second, first);

        Assert.DoesNotContain(new[] { move, delete }, answer => (int)answer.Status >= 500);
        Assert.Equal(HttpStatusCode.NoContent, delete.Status);
        Assert.Equal(moveFirst ? HttpStatusCode.OK : HttpStatusCode.NotFound, move.Status);
        var stored = await StoredTeamAsync(factory, team.Id);
        Assert.Equal((TeamStatus.Deleted, (Guid?)null), (stored.Status, stored.DepartmentId));
    }

    // Race · two team creations in one department at once never share a colour while another is free (the tree lock orders them).
    [Fact]
    public async Task CreateTeam_TwoAtOnce_InOneDepartment_GetDifferentColours()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");

        Task<Answer> Create(string name, CommitGate? at) =>
            SendAsync(HttpMethod.Post, OrganizationsApi.OrgTeams(organization.Id), new { name, departmentId = department.Id.ToString() }, gate: at);

        var (a, b) = await RaceAsync(factory, at => Create("A", at), at => Create("B", at));

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (a.Status, b.Status));
        Assert.NotEqual(a.Body.GetProperty("color").GetString(), b.Body.GetProperty("color").GetString());
    }

    // Race · two department creations at once never share a colour while another is free.
    [Fact]
    public async Task CreateDepartment_TwoAtOnce_GetDifferentColours()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        Task<Answer> Create(string name, CommitGate? at) => SendAsync(HttpMethod.Post, OrganizationsApi.Areas(organization.Id), new { name }, gate: at);

        var (a, b) = await RaceAsync(factory, at => Create("Первый", at), at => Create("Второй", at));

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (a.Status, b.Status));
        Assert.NotEqual(a.Body.GetProperty("color").GetString(), b.Body.GetProperty("color").GetString());
    }
}
