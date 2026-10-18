using System.Net;
using System.Text.Json;
using Mustask.Domain.Organizations;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/teams/{teamId}/name</c> (feat-028 api-contract § 2.1: the route moved out of the
/// project; mod-017 § 3.4 before) through the real host and a real PostgreSQL. Every test builds its own organization
/// (<see cref="OrganizationsTestData"/>): the database is shared and never emptied. Reads of the result go to the database, not
/// through another lane's endpoint.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class RenameTeamEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> RenameAsync(Guid organizationId, string teamId, object body, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"/api/organizations/{organizationId}/teams/{Uri.EscapeDataString(teamId)}/name",
            body,
            token ?? OrganizationsTokens.SignedIn());

    private Task<Answer> RenameAsync(OrganizationStructure structure, Guid teamId, object body) =>
        RenameAsync(structure.OrganizationId, teamId.ToString(), body);

    /// <summary>The team's stored name, whatever its status.</summary>
    private async Task<string> StoredNameAsync(Guid teamId) => (await StoredTeamAsync(factory, teamId)).Name;

    // feat-028 US-1 · "A team is renamed": «Платформа» becomes «Платформа данных», trimmed. The answer is the `Team` of
    // api-contract § 1.1 — id, name, description; no project, no mark, no flag — and the new name is what is stored. Replaces
    // Rename_FreeName_Returns200_TheTeam_NewNameStored (the answer shape moved, D-16).
    [Fact]
    public async Task Rename_FreeName_Returns200_TheTeam_NewNameStored()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var teamId = structure.Platform.Id;

        var answer = await RenameAsync(structure, teamId, new { name = "  Платформа данных " });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(["description", "id", "name"], PropertyNames(answer.Body));
        Assert.Equal(teamId, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal("Платформа данных", answer.Body.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, answer.Body.GetProperty("description").ValueKind);

        Assert.Equal("Платформа данных", await StoredNameAsync(teamId));
    }

    // feat-028 US-1 · "What the team owns, its people and its links do not change": the rename touches the name and nothing else —
    // not the links (with who made them and when), not the memberships, not the description, not the project's setting that names it.
    [Fact]
    public async Task Rename_ChangesOnlyTheName_LinksMembersDescriptionAndSettingStay()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var teamId = structure.Platform.Id;
        await OrganizationsTestData.SetDefaultTeamAsync(factory, structure.OrganizationId, structure.ProjectId, teamId);
        var linksBefore = await LinksOfTeamAsync(factory, teamId);
        var membersBefore = await TeamMemberIdsAsync(factory, teamId);

        var answer = await RenameAsync(structure, teamId, new { name = "Платформа данных" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(linksBefore.Select(link => (link.ProjectId, link.LinkedBy, link.LinkedAt)), (await LinksOfTeamAsync(factory, teamId)).Select(link => (link.ProjectId, link.LinkedBy, link.LinkedAt)));
        Assert.Equal(membersBefore, await TeamMemberIdsAsync(factory, teamId));
        Assert.Equal(teamId, await SettingAsync(factory, structure.ProjectId));
    }

    // BR-M1-1 · "a name never conflicts with the object's own current name": «ПЛАТФОРМА» renames «Платформа».
    [Fact]
    public async Task Rename_OwnNameInAnotherCase_Returns200()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var teamId = structure.Platform.Id;

        var answer = await RenameAsync(structure, teamId, new { name = " ПЛАТФОРМА " });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("ПЛАТФОРМА", answer.Body.GetProperty("name").GetString());
        Assert.Equal("ПЛАТФОРМА", await StoredNameAsync(teamId));
    }

    // feat-028 BR-OL-3 · "A team's name is unique among the organization's active teams": «аналитика » is taken by «Аналитика»,
    // whichever case and spaces and whichever project it is linked to, or none. Both teams keep their names.
    [Theory]
    [InlineData("linked to this team's project")]
    [InlineData("linked to another project")]
    [InlineData("linked to no project")]
    public async Task Rename_ToTakenName_Returns409TeamNameTaken_NameUnchanged(string where)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var secondProject = await OrganizationsTestData.CreateBareProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");
        var analytics = where switch
        {
            "linked to this team's project" => await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Аналитика"),
            "linked to another project" => await OrganizationsTestData.AddTeamAsync(factory, secondProject.Project.Id, "Аналитика"),
            _ => await OrganizationsTestData.AddUnlinkedTeamAsync(factory, structure.OrganizationId, "Аналитика"),
        };

        var answer = await RenameAsync(structure, structure.Platform.Id, new { name = " аналитика " });

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.team.name_taken", answer.ErrorCode);
        Assert.Equal("Команда с таким названием уже есть в организации", answer.Detail);
        Assert.Equal("Платформа", await StoredNameAsync(structure.Platform.Id));
        Assert.Equal("Аналитика", await StoredNameAsync(analytics.Id));
    }

    // feat-028 BR-OL-3 · "Another organization may share a name": the rule is per organization, not per instance.
    [Fact]
    public async Task Rename_ToNameOfATeamInAnotherOrganization_Returns200()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateOrganizationAsync(factory);
        await OrganizationsTestData.AddUnlinkedTeamAsync(factory, other.Id, "Чужая");

        var answer = await RenameAsync(structure, structure.Platform.Id, new { name = "Чужая" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("Чужая", await StoredNameAsync(structure.Platform.Id));
    }

    // feat-028 US-5 · "A deleted team's name is free": a deleted team does not hold its name (ADR-0026 rule 6).
    [Fact]
    public async Task Rename_ToNameOfDeletedTeam_Returns200()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var core = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Ядро");
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, core.Id);

        var answer = await RenameAsync(structure, structure.Platform.Id, new { name = "ядро" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("ядро", answer.Body.GetProperty("name").GetString());
    }

    // feat-028 BR-OL-1 · a team linked to no project is renamed like any other: the team is the organization's, not a project's.
    [Fact]
    public async Task Rename_TeamLinkedToNoProject_Returns200()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var alone = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, structure.OrganizationId, "Ничья");

        var answer = await RenameAsync(structure, alone.Id, new { name = "Ничья теперь" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("Ничья теперь", await StoredNameAsync(alone.Id));
    }

    // feat-028 BR-OL-9 · a team linked to a deactivated project is renamed all the same: the project's state is not the team's.
    [Fact]
    public async Task Rename_TeamOfADeactivatedProject_Returns200()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);

        var answer = await RenameAsync(structure, structure.Platform.Id, new { name = "Платформа данных" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("Платформа данных", await StoredNameAsync(structure.Platform.Id));
    }

    // US-1 · "A visitor who is not signed in sees nothing": the challenge comes before every other check.
    [Fact]
    public async Task Rename_NoToken_Returns401_ChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var teamId = structure.Platform.Id;

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{Team(structure.OrganizationId, teamId)}/name",
            new { name = "Платформа данных" });

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal("Платформа", await StoredNameAsync(teamId));
    }

    // feat-028 api-contract § 2.1 · "The old address answers nothing": the project-nested rename route is gone (8 routes out).
    [Fact]
    public async Task Rename_AtTheRetiredProjectAddress_Returns404_ChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var teamId = structure.Platform.Id;

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{ProjectTeam(structure.OrganizationId, structure.ProjectId, teamId)}/name",
            new { name = "Платформа данных" },
            OrganizationsTokens.SignedIn());

        Assert.True(answer.Status is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, $"The retired route answered {(int)answer.Status}.");
        Assert.Equal("Платформа", await StoredNameAsync(teamId));
    }

    // US-3 · "An organization id names no organization the caller belongs to" (BR-M1-20, BR-M1-22): unknown or of another
    // organization (the caller is not a member there), it answers organization.not_found before the team and the body are looked at.
    [Theory]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task Rename_OrganizationNotTheCallers_Returns404OrganizationNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateOrganizationAsync(factory, founded: false);
        var organizationId = how == "unknown" ? Guid.CreateVersion7() : other.Id;

        var answer = await RenameAsync(organizationId, structure.Platform.Id.ToString(), new { name = "" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.organization.not_found", answer.ErrorCode);
        Assert.Equal("Платформа", await StoredNameAsync(structure.Platform.Id));
    }

    // US-1 · "A team identifier that names no team of the organization" (BR-M1-20, BR-M1-22) and US-5 · "A deleted team answers
    // not found": malformed, unknown, of another organization, or deleted. The team answers before the body's name, which is bad here
    // too (api-contract § 0.4).
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deleted")]
    public async Task Rename_TeamNotAnActiveTeamOfTheOrganization_Returns404TeamNotFound_ChangesNothing(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var core = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Ядро");

        if (how == "deleted")
        {
            await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, core.Id);
        }

        var teamId = how switch
        {
            "malformed" => core.Id.ToString("N"),
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another organization" => other.Platform.Id.ToString(),
            _ => core.Id.ToString(),
        };

        var answer = await RenameAsync(structure.OrganizationId, teamId, new { name = "" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.team.not_found", answer.ErrorCode);
        Assert.Equal("Команда не найдена.", answer.Detail);
        Assert.Equal("Ядро", await StoredNameAsync(core.Id));
        Assert.Equal("Платформа", await StoredNameAsync(other.Platform.Id));
    }

    // US-1 · "an empty name, or one longer than 100 characters, is refused with «Название должно содержать от 1 до 100
    // символов»". The team is unchanged.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("101")]
    public async Task Rename_InvalidName_Returns400NameLength_NameUnchanged(string? name)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var teamId = structure.Platform.Id;

        var answer = await RenameAsync(structure, teamId, new { name = name == "101" ? new string('я', 101) : name });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("organizations.validation_failed", answer.ErrorCode);
        var reason = Assert.Single(answer.Body.GetProperty("errors").GetProperty("name").EnumerateArray());
        Assert.Equal("organizations.name_length", reason.GetProperty("code").GetString());
        Assert.Equal("Название должно содержать от 1 до 100 символов", reason.GetProperty("detail").GetString());
        Assert.Equal("Платформа", await StoredNameAsync(teamId));
    }

    // US-1 · a name of exactly 100 characters, after trimming, is the longest that passes.
    [Fact]
    public async Task Rename_NameOfOneHundredCharactersAfterTrimming_Returns200()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var hundred = new string('я', 100);

        var answer = await RenameAsync(structure, structure.Platform.Id, new { name = $" {hundred} " });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(hundred, answer.Body.GetProperty("name").GetString());
    }
}
