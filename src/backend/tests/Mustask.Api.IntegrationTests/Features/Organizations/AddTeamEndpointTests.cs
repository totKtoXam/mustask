using System.Net;
using System.Text.Json;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/teams</c> (feat-028 api-contract § 2.1; US-1, US-7, US-8) through the real host and a real
/// PostgreSQL. Every test builds its own organization (<see cref="OrganizationsTestData"/>): the database is shared and never emptied.
/// A fresh structure holds the project «Пример проекта» with a team of its name and a second team «Платформа», both linked to it.
/// Rewritten from the project-nested add (mod-017 api-contract § 2.7; D-16): a team is created in the organization, with no project.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class AddTeamEndpointTests(MustaskApiFactory factory)
{
    // US-1 · "created with a name only" (BR-OL-2): the team alone — no project, no link, no member, the caller included.
    [Fact]
    public async Task Add_NameOnly_CreatesTeamAlone_NoProjectLinkOrMembership()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await CreateTeamAsync(factory, structure.OrganizationId, new { name = " MARS " });
        var teamId = answer.Body.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal($"/api/organizations/{structure.OrganizationId}/teams/{teamId}", answer.Location?.OriginalString);
        Assert.Equal(["description", "id", "name"], PropertyNames(answer.Body));
        Assert.Equal("MARS", answer.Body.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, answer.Body.GetProperty("description").ValueKind);

        var stored = await StoredTeamAsync(factory, teamId);
        Assert.Equal((structure.OrganizationId, TeamStatus.Active), (stored.OrganizationId, stored.Status));
        Assert.Empty(await LinksOfTeamAsync(factory, teamId));
        Assert.Empty(await TeamMemberIdsAsync(factory, teamId));
    }

    // US-1 · "A visitor who is not signed in sees nothing"; "created with a name only" for a member and a viewer: 403, nothing created.
    [Fact]
    public async Task Add_NoToken_Returns401()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, OrganizationsApi.OrgTeams(structure.OrganizationId), new { name = "MARS" });

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal(2, (await StoredTeamsAsync(factory, structure.OrganizationId)).Count);
    }

    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Add_MemberAndViewer_Return403_CreateNothing(OrganizationRole role)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, role);

        var answer = await CreateTeamAsync(factory, structure.OrganizationId, new { name = "MARS" }, caller.Token);

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (answer.Status, answer.ErrorCode));
        Assert.Equal(2, (await StoredTeamsAsync(factory, structure.OrganizationId)).Count);
    }

    // US-8 · a non-member of the organization gets the organization's not-found, an instance administrator included.
    [Fact]
    public async Task Add_NonMember_Is404Organization()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await CreateTeamAsync(factory, structure.OrganizationId, new { name = "MARS" }, MembershipTestData.Outsider());

        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (answer.Status, answer.ErrorCode));
    }

    // US-1 · "created with its optional parts in one operation": the description as typed, the people and the links — each link
    // records the caller and the time; the team owns nothing and the caller joined nothing (BR-OL-2).
    [Fact]
    public async Task Add_DescriptionMembersAndProjects_AreOneOperation_LinksRecordTheCallerAndTime()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var second = await OrganizationsTestData.CreateBareProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");
        var ivan = await MembershipTestData.NewNamedMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member, "Иван", "Иванов");
        var maria = await MembershipTestData.NewNamedMemberAsync(factory, structure.OrganizationId, OrganizationRole.Viewer, "Мария", "Петрова");

        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var answer = await CreateTeamAsync(
            factory,
            structure.OrganizationId,
            new
            {
                name = "MARS",
                description = "  Платформа\nи инструменты ",
                memberIds = new[] { ivan.Id, maria.Id, ivan.Id },
                projectIds = new[] { structure.ProjectId.ToString(), second.Project.Id.ToString() },
            });
        var teamId = answer.Body.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal("  Платформа\nи инструменты ", answer.Body.GetProperty("description").GetString());
        Assert.Equal(new[] { ivan.Id, maria.Id }.Order(), await TeamMemberIdsAsync(factory, teamId));
        var links = await LinksOfTeamAsync(factory, teamId);
        Assert.Equal(new[] { structure.ProjectId, second.Project.Id }.Order(), links.Select(link => link.ProjectId));
        Assert.All(links, link =>
        {
            Assert.Equal(OrganizationsTestData.Creator, link.LinkedBy);
            Assert.True(link.LinkedAt > before);
        });
    }

    // US-3 · "a project that gets its first team has it as its default by inference" (BR-OL-7), through the project's list.
    [Fact]
    public async Task Add_OnlyLinkedTeam_IsTheProjectsDefaultByInference()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id);

        var created = await CreateTeamAsync(factory, organization.Id, new { name = "MARS", projectIds = new[] { project.Project.Id.ToString() } });
        var listed = await GetAsync(factory, OrganizationsApi.Teams(organization.Id, project.Project.Id));

        Assert.Equal(HttpStatusCode.Created, created.Status);
        var team = Assert.Single(listed.Body.EnumerateArray());
        Assert.Equal("onlyTeam", team.GetProperty("defaultMark").GetString());
        Assert.Null(await SettingAsync(factory, project.Project.Id));
    }

    // US-1 · "a choice that cannot be made" (4 rows): a person of another organization 409, a project of another organization or a
    // deactivated one 404, a description over 2 000 characters 400 — nothing is created and the name stays free.
    [Theory]
    [InlineData("other-organization user")]
    [InlineData("unknown user")]
    [InlineData("foreign project")]
    [InlineData("deactivated project")]
    [InlineData("malformed project")]
    [InlineData("description over 2000")]
    public async Task Add_Refusals_CreateNothing_NameStaysFree(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var outsider = await MembershipTestData.NewNamedMemberAsync(factory, other.OrganizationId, OrganizationRole.Member, "Олег", "Чужой");
        var deactivated = await OrganizationsTestData.CreateBareProjectAsync(
            factory, structure.OrganizationId, "Старый", "OLD", ProjectStatus.Deactivated);

        var (body, status, code) = how switch
        {
            "other-organization user" => ((object)new { name = "ORION", memberIds = new[] { outsider.Id } }, HttpStatusCode.Conflict, "organizations.team.member_not_in_organization"),
            "unknown user" => (new { name = "ORION", memberIds = new[] { "never-projected" } }, HttpStatusCode.Conflict, "organizations.team.member_not_in_organization"),
            "foreign project" => (new { name = "ORION", projectIds = new[] { other.ProjectId.ToString() } }, HttpStatusCode.NotFound, "organizations.project.not_found"),
            "deactivated project" => (new { name = "ORION", projectIds = new[] { deactivated.Project.Id.ToString() } }, HttpStatusCode.NotFound, "organizations.project.not_found"),
            "malformed project" => (new { name = "ORION", projectIds = new[] { "not-an-id" } }, HttpStatusCode.NotFound, "organizations.project.not_found"),
            _ => (new { name = "ORION", description = new string('я', 2001) }, HttpStatusCode.BadRequest, "organizations.validation_failed"),
        };

        var refused = await CreateTeamAsync(factory, structure.OrganizationId, body);
        var teams = await StoredTeamsAsync(factory, structure.OrganizationId);

        Assert.Equal((status, code), (refused.Status, refused.ErrorCode));
        Assert.DoesNotContain(teams, team => team.Name == "ORION");

        var retry = await CreateTeamAsync(factory, structure.OrganizationId, new { name = "ORION" });
        Assert.Equal(HttpStatusCode.Created, retry.Status);
    }

    // US-1 · the description over 2 000 characters names its field reason.
    [Fact]
    public async Task Add_DescriptionOver2000_Returns400WithTheFieldReason()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await CreateTeamAsync(factory, structure.OrganizationId, new { name = "MARS", description = new string('я', 2001) });

        Assert.Equal("organizations.description_length", ReasonOf(answer, "description"));
        Assert.Equal(
            "Описание должно содержать не более 2000 символов",
            answer.Body.GetProperty("errors").GetProperty("description")[0].GetProperty("detail").GetString());
    }

    // US-1 · "names are unique per organization" (5 rows): the name of an active team in any letter case and with spaces is taken.
    [Theory]
    [InlineData("платформа ")]
    [InlineData(" ПЛАТФОРМА")]
    [InlineData("Платформа")]
    [InlineData("пример проекта")]
    public async Task Add_NameTakenAnyCase_Returns409TeamNameTaken_CreatesNothing(string name)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await CreateTeamAsync(factory, structure.OrganizationId, new { name });

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.team.name_taken", answer.ErrorCode);
        Assert.Equal("Команда с таким названием уже есть в организации", answer.Detail);
        Assert.Equal(2, (await StoredTeamsAsync(factory, structure.OrganizationId)).Count);
    }

    // US-1 · a name is free in another organization (a team no longer belongs to a project: the name is the organization's alone).
    [Fact]
    public async Task Add_NameInAnotherOrganization_IsFree()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await CreateTeamAsync(factory, other.Id, new { name = "Платформа" });

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(2, (await StoredTeamsAsync(factory, structure.OrganizationId)).Count);
    }

    // US-7 · "The deleted team's name is free": «Платформа» was deleted and the team is added again at once.
    [Fact]
    public async Task Add_NameOfDeletedTeam_IsFree()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, structure.Platform.Id);

        var answer = await CreateTeamAsync(factory, structure.OrganizationId, new { name = "Платформа" });
        var teamId = answer.Body.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.NotEqual(structure.Platform.Id, teamId);
        var teams = await StoredTeamsAsync(factory, structure.OrganizationId);
        Assert.Equal(TeamStatus.Deleted, teams.Single(team => team.Id == structure.Platform.Id).Status);
        Assert.Equal(TeamStatus.Active, teams.Single(team => team.Id == teamId).Status);
    }

    // US-1 · "An empty name, or one longer than 100 characters, is refused with «Название должно содержать от 1 до 100 символов»".
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("101")]
    public async Task Add_BlankOrTooLongName_Returns400NameLength(string? name)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await CreateTeamAsync(factory, structure.OrganizationId, new { name = name == "101" ? new string('я', 101) : name });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("organizations.validation_failed", answer.ErrorCode);
        Assert.Equal("organizations.name_length", ReasonOf(answer, "name"));
        Assert.Equal(2, (await StoredTeamsAsync(factory, structure.OrganizationId)).Count);
    }

    // US-1 · "two creations of one name" (NFR Concurrency): request A is held at its commit with its team row written; B passes the
    // pre-check, blocks on the team index until A commits, and loses with the pre-check's code. Exactly one team of the name exists.
    [Fact]
    public async Task Add_TwoConcurrentSameName_OneTeam()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var body = new { name = "Аналитика" };
        using var gate = factory.CommitGates.Open();

        var first = CreateTeamAsync(factory, structure.OrganizationId, body, gate: gate);
        await Task.WhenAny(gate.Reached, first);
        Assert.False(first.IsCompleted, "The first creation finished before reaching its commit gate.");

        var second = CreateTeamAsync(factory, structure.OrganizationId, body);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "The second creation did not wait for the first one's uncommitted row.");

        gate.Release();
        var (a, b) = (await first, await second);

        Assert.Equal(HttpStatusCode.Created, a.Status);
        Assert.Equal((HttpStatusCode.Conflict, "organizations.team.name_taken"), (b.Status, b.ErrorCode));
        var named = (await StoredTeamsAsync(factory, structure.OrganizationId)).Where(team => team.NormalizedName == "АНАЛИТИКА").ToList();
        Assert.Equal(a.Body.GetProperty("id").GetGuid(), Assert.Single(named).Id);
    }

    // US-7 · "an administrator rebuilds the structure": after the upgrade the organization has no team; the name is free, the team is
    // linked to the chosen projects and owns nothing.
    [Fact]
    public async Task Add_AfterTheUpgrade_NameIsFree_LinkedToTheChosenProjects_OwnsNothing()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id);

        var answer = await CreateTeamAsync(factory, organization.Id, new { name = "Platform", projectIds = new[] { project.Project.Id.ToString() } });
        var teamId = answer.Body.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal([project.Project.Id], (await LinksOfTeamAsync(factory, teamId)).Select(link => link.ProjectId));
        Assert.Equal(0, await OwnedItemCountAsync(factory, teamId));
    }
}
