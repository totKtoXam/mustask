using System.Net;
using Mustask.Domain.Identity;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// feat-028 US-8, "every step answers by the caller's role" and "a refusal changes nothing, the order of today's answers" (api-contract
/// § 0.1, § 0.5; ADR-0029 § 4): the nine operations on teams and their links, each by an administrator (Ann), a member (Max), a viewer
/// (Vera) and a person who is not in the organization (Olga). Until <c>mod-019-scoped-access</c> only the administrator holds the four
/// new codes and <c>team.write</c>; a member and a viewer read. Every test builds its own organization.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class TeamPermissionMatrixEndpointTests(MustaskApiFactory factory)
{
    private const string Forbidden = "organizations.access.forbidden";
    private const string OrganizationNotFound = "organizations.organization.not_found";

    /// <summary>An organization with a project and two teams linked to it, a second project with no team, a team linked to nothing and one to delete.</summary>
    private sealed record World(OrganizationStructure Structure, Mustask.Domain.Organizations.Project Bare, Mustask.Domain.Organizations.Team Free, Mustask.Domain.Organizations.Team Doomed)
    {
        public Guid OrganizationId => Structure.OrganizationId;
    }

    private async Task<World> CreateWorldAsync()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var bare = await OrganizationsTestData.CreateBareProjectAsync(factory, structure.OrganizationId, "Без команды", "BARE");
        var free = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, structure.OrganizationId, "Свободная");
        var doomed = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, structure.OrganizationId, "Обречённая");

        return new World(structure, bare, free, doomed);
    }

    /// <summary>The nine operations: the request each sends and the status the administrator gets.</summary>
    private static (HttpMethod Method, string Path, object? Body, HttpStatusCode Success) Operation(string name, World world)
    {
        var org = world.OrganizationId;
        var project = world.Structure.ProjectId;
        var platform = world.Structure.Platform.Id;

        return name switch
        {
            "create team" => (HttpMethod.Post, OrganizationsApi.OrgTeams(org), new { name = "Новая команда" }, HttpStatusCode.Created),
            "rename team" => (HttpMethod.Put, $"{Team(org, platform)}/name", new { name = "Платформа 2" }, HttpStatusCode.OK),
            "describe team" => (HttpMethod.Put, $"{Team(org, platform)}/description", new { description = "Описание" }, HttpStatusCode.OK),
            "delete team" => (HttpMethod.Delete, Team(org, world.Doomed.Id), null, HttpStatusCode.NoContent),
            "link projects to team" => (HttpMethod.Post, $"{Team(org, world.Free.Id)}/projects", new { projectIds = new[] { world.Bare.Id.ToString() } }, HttpStatusCode.NoContent),
            "link teams to project" => (HttpMethod.Post, OrganizationsApi.Teams(org, world.Bare.Id), new { teamIds = new[] { world.Free.Id.ToString() } }, HttpStatusCode.NoContent),
            "unlink team" => (HttpMethod.Post, $"{ProjectTeam(org, project, platform)}/unlink", To(null), HttpStatusCode.NoContent),
            "name default team" => (HttpMethod.Put, $"{OrganizationsApi.Project(org, project)}/default-team", new { teamId = platform.ToString() }, HttpStatusCode.NoContent),
            _ => (HttpMethod.Post, $"{OrganizationsApi.Organization(org)}/projects", new { name = "Новый проект", key = "NEWP", teamIds = new[] { world.Free.Id.ToString() } }, HttpStatusCode.Created),
        };
    }

    public static TheoryData<string> Operations =>
    [
        "create team",
        "rename team",
        "describe team",
        "delete team",
        "link projects to team",
        "link teams to project",
        "unlink team",
        "name default team",
        "create project with teams",
    ];

    /// <summary>Everything the nine operations write: the organization's teams, their links, the projects and the setting of the first.</summary>
    private async Task<string> StateAsync(World world)
    {
        var teams = (await StoredTeamsAsync(factory, world.OrganizationId))
            .OrderBy(team => team.Id)
            .Select(team => $"{team.Id}:{team.Name}:{team.Status}:{team.Description}");
        var links = new List<string>();

        foreach (var project in new[] { world.Structure.ProjectId, world.Bare.Id })
        {
            links.AddRange((await LinksOfProjectAsync(factory, project)).Select(link => $"{project}+{link.TeamId}"));
        }

        var listed = await GetAsync(factory, $"{OrganizationsApi.Organization(world.OrganizationId)}/projects");

        return $"{string.Join(";", teams)} | {string.Join(";", links)} | {listed.Body.GetArrayLength()} | {await SettingAsync(factory, world.Structure.ProjectId)}";
    }

    // feat-028 US-8 · "EveryStep_ByRole": the administrator does each step; a member and a viewer get 403 and nothing changes; a person who
    // is not in the organization gets 404 organization.not_found, never a 403, and nothing changes either.
    [Theory]
    [MemberData(nameof(Operations))]
    public async Task EveryStep_ByRole_AdminDone_MemberViewer403_NonMember404(string operation)
    {
        var world = await CreateWorldAsync();
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);
        var viewer = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        var admin = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Admin);
        var (method, path, body, success) = Operation(operation, world);
        var before = await StateAsync(world);

        foreach (var caller in new[] { member.Token, viewer.Token })
        {
            var refused = await OrganizationsApi.SendAsync(factory, method, path, body, caller);

            Assert.Equal((HttpStatusCode.Forbidden, Forbidden), (refused.Status, refused.ErrorCode));
        }

        var outsider = await OrganizationsApi.SendAsync(factory, method, path, body, OrganizationsTokens.Stranger());
        var anonymous = await OrganizationsApi.SendAsync(factory, method, path, body);

        Assert.Equal((HttpStatusCode.NotFound, OrganizationNotFound), (outsider.Status, outsider.ErrorCode));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
        Assert.Equal(before, await StateAsync(world));

        var done = await OrganizationsApi.SendAsync(factory, method, path, body, admin.Token);

        Assert.Equal(success, done.Status);
        Assert.NotEqual(before, await StateAsync(world));
    }

    // feat-028 US-8 · "Reads_AllRoles": every role in the organization reads the teams — the organization's list, a team, its projects, its
    // members, the project's linked teams; a person outside gets the organization's 404.
    [Theory]
    [InlineData(OrganizationRole.Admin)]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Reads_AllRoles_AndTheOutsiderGets404(OrganizationRole role)
    {
        var world = await CreateWorldAsync();
        var caller = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, role);
        var org = world.OrganizationId;
        var platform = world.Structure.Platform.Id;
        var reads = new[]
        {
            OrganizationsApi.OrgTeams(org),
            Team(org, platform),
            $"{Team(org, platform)}/projects",
            $"{Team(org, platform)}/members",
            OrganizationsApi.Teams(org, world.Structure.ProjectId),
        };

        foreach (var path in reads)
        {
            var read = await GetAsync(factory, path, caller.Token);
            var outsider = await GetAsync(factory, path, OrganizationsTokens.Stranger());

            Assert.True(read.Status == HttpStatusCode.OK, $"GET {path} answered {read.Status} {read.ErrorCode} to a {role}.");
            Assert.Equal((HttpStatusCode.NotFound, OrganizationNotFound), (outsider.Status, outsider.ErrorCode));
        }
    }

    // feat-028 US-8 · "Viewer_TeamWriteNamingAMissingTeam_Is403Not404": the 403 comes before the address is looked at, so a viewer learns
    // nothing about which ids exist; the administrator gets the team's 404 for the same request.
    [Fact]
    public async Task Viewer_WritesNamingAMissingTeamOrProject_Is403Not404_AdminGets404()
    {
        var world = await CreateWorldAsync();
        var viewer = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        var admin = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Admin);
        var org = world.OrganizationId;
        var missing = Guid.CreateVersion7();
        var requests = new (HttpMethod Method, string Path, object? Body, string Code)[]
        {
            (HttpMethod.Put, $"{Team(org, missing)}/name", new { name = "Х" }, "organizations.team.not_found"),
            (HttpMethod.Put, $"{Team(org, missing)}/description", new { description = "Х" }, "organizations.team.not_found"),
            (HttpMethod.Delete, Team(org, missing), null, "organizations.team.not_found"),
            (HttpMethod.Post, $"{Team(org, missing)}/projects", new { projectIds = Array.Empty<string>() }, "organizations.team.not_found"),
            (HttpMethod.Post, $"{OrganizationsApi.Teams(org, missing)}", new { teamIds = Array.Empty<string>() }, "organizations.project.not_found"),
            (HttpMethod.Post, $"{ProjectTeam(org, world.Structure.ProjectId, missing)}/unlink", To(null), "organizations.team.not_found"),
            (HttpMethod.Put, $"{OrganizationsApi.Project(org, missing)}/default-team", new { teamId = (string?)null }, "organizations.project.not_found"),
        };

        foreach (var (method, path, body, code) in requests)
        {
            var asViewer = await OrganizationsApi.SendAsync(factory, method, path, body, viewer.Token);
            var asAdmin = await OrganizationsApi.SendAsync(factory, method, path, body, admin.Token);

            Assert.True(asViewer.Status == HttpStatusCode.Forbidden, $"{method} {path} answered {asViewer.Status} {asViewer.ErrorCode} to a viewer.");
            Assert.Equal(Forbidden, asViewer.ErrorCode);
            Assert.Equal((HttpStatusCode.NotFound, code), (asAdmin.Status, asAdmin.ErrorCode));
        }
    }

    // feat-028 § 0.5 · "Order_401_Then404Org_Then403_Then404Address_Then400_Then409", the creation of a team: no token 401; a person outside
    // the organization 404 before anything; a member 403 before the body; for the administrator the body's fields all at once 400, then
    // the ids the body names 404, then the 409s with the member before the name.
    [Fact]
    public async Task Order_401_Then404Org_Then403_Then400_Then404Body_Then409_OnTheCreationOfATeam()
    {
        var world = await CreateWorldAsync();
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);
        var admin = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Admin);
        var path = OrganizationsApi.OrgTeams(world.OrganizationId);
        var everythingWrong = new { name = "  ", description = new string('я', 2001), projectIds = new[] { Guid.CreateVersion7().ToString() }, memberIds = new[] { "nobody" } };
        var before = await StateAsync(world);

        var anonymous = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, path, everythingWrong);
        var outsider = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, path, everythingWrong, OrganizationsTokens.Stranger());
        var asMember = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, path, everythingWrong, member.Token);
        var invalid = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, path, everythingWrong, admin.Token);
        var unknownProject = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, path, new { name = "Новая", projectIds = new[] { Guid.CreateVersion7().ToString() }, memberIds = new[] { "nobody" } }, admin.Token);
        var conflicts = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, path, new { name = "платформа", memberIds = new[] { "nobody" } }, admin.Token);
        var nameOnly = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, path, new { name = "платформа" }, admin.Token);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
        Assert.Equal((HttpStatusCode.NotFound, OrganizationNotFound), (outsider.Status, outsider.ErrorCode));
        Assert.Equal((HttpStatusCode.Forbidden, Forbidden), (asMember.Status, asMember.ErrorCode));
        Assert.Equal((HttpStatusCode.BadRequest, "organizations.validation_failed"), (invalid.Status, invalid.ErrorCode));
        Assert.Equal(["description", "name"], PropertyNames(invalid.Body.GetProperty("errors")));
        Assert.Equal((HttpStatusCode.NotFound, "organizations.project.not_found"), (unknownProject.Status, unknownProject.ErrorCode));
        Assert.Equal((HttpStatusCode.Conflict, "organizations.team.member_not_in_organization"), (conflicts.Status, conflicts.ErrorCode));
        Assert.Equal((HttpStatusCode.Conflict, "organizations.team.name_taken"), (nameOnly.Status, nameOnly.ErrorCode));
        Assert.Equal(before, await StateAsync(world));
    }

    // feat-028 § 0.1, D-7 · the link codes of a creation: `team.create` plus, when it links, `team.link` and `project.link` — and, when it
    // names members, `team.write`. The administrator holds them all; a member holds none of the first and is refused before the body.
    [Fact]
    public async Task Create_WithProjectsAndMembers_NeedsTheLinkCodesToo_AdminHasThem()
    {
        var world = await CreateWorldAsync();
        var admin = await MembershipTestData.NewNamedMemberAsync(factory, world.OrganizationId, OrganizationRole.Admin, "Анна", "Админова");
        var path = OrganizationsApi.OrgTeams(world.OrganizationId);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            path,
            new { name = "С проектом и людьми", projectIds = new[] { world.Bare.Id.ToString() }, memberIds = new[] { admin.Id } },
            admin.Token);

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        var teamId = answer.Body.GetProperty("id").GetGuid();
        Assert.Equal([world.Bare.Id], (await LinksOfTeamAsync(factory, teamId)).Select(link => link.ProjectId));
        Assert.Equal([admin.Id], await TeamMemberIdsAsync(factory, teamId));
    }

    // feat-028 US-3 · "Link_ChangesNoOneAccess_AndNoItem": linking a team to a project grants no one a right in it and moves no item: the
    // members' roles are as they were, and the team's members gain nothing in the organization.
    [Fact]
    public async Task Link_ChangesNoOnesAccess_AndNoItem()
    {
        var world = await CreateWorldAsync();
        var viewer = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        await MembershipTestData.AddTeamMemberAsync(factory, world.OrganizationId, world.Free.Id, viewer.Id);
        var before = await MembershipTestData.FindMembershipAsync(factory, world.OrganizationId, viewer.Id);

        var admin = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Admin);
        var linked = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            OrganizationsApi.Teams(world.OrganizationId, world.Bare.Id),
            new { teamIds = new[] { world.Free.Id.ToString() } },
            admin.Token);
        var write = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Put, $"{Team(world.OrganizationId, world.Free.Id)}/name", new { name = "Попытка" }, viewer.Token);

        Assert.Equal(HttpStatusCode.NoContent, linked.Status);
        Assert.Equal((HttpStatusCode.Forbidden, Forbidden), (write.Status, write.ErrorCode));
        var after = await MembershipTestData.FindMembershipAsync(factory, world.OrganizationId, viewer.Id);
        Assert.Equal((before!.Role, before.UserId), (after!.Role, after.UserId));
        Assert.Equal(0, await OwnedItemCountAsync(factory, world.Free.Id));
    }
}
