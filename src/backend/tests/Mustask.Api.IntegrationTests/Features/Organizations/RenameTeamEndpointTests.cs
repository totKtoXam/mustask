using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/name</c>
/// (api-contract § 3.4) through the real host and a real PostgreSQL (plan-backend
/// § 14.3, lane L-18). Every test builds its own organization
/// (<see cref="OrganizationsTestData"/>): the database is shared and never
/// emptied. Reads of the result go through the repositories, not through another
/// lane's endpoint.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class RenameTeamEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> RenameAsync(Guid organizationId, string projectId, string teamId, object body, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"/api/organizations/{organizationId}/projects/{Uri.EscapeDataString(projectId)}/teams/{Uri.EscapeDataString(teamId)}/name",
            body,
            token ?? OrganizationsTokens.SignedIn());

    private Task<Answer> RenameAsync(OrganizationStructure structure, Guid teamId, object body) =>
        RenameAsync(structure.OrganizationId, structure.ProjectId.ToString(), teamId.ToString(), body);

    /// <summary>The team's stored name, whatever its status.</summary>
    private Task<string> StoredNameAsync(Guid projectId, Guid teamId) =>
        OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
            (await services.GetRequiredService<ITeamRepository>().LockForShareAsync(projectId, teamId, ct))?.Name
            ?? throw new InvalidOperationException("The team is not a team of the project."));

    // mod-017 US-4 · "A team is renamed": «Платформа» becomes «Платформа данных», trimmed. The answer is the `Team` of api-contract
    // § 1.3 — no first-team flag, no default area — and the new name is what is stored, so it is shown wherever the team is
    // listed or offered. Replaces Rename_FreeName_Returns200_TeamWithItsDefaultArea_NewNameStored (D-16).
    [Fact]
    public async Task Rename_FreeName_Returns200_TheTeam_NewNameStored()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var teamId = structure.Platform.Id;

        var answer = await RenameAsync(structure, teamId, new { name = "  Платформа данных " });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(["defaultMark", "id", "isMember", "name", "projectId"], PropertyNames(answer.Body));
        Assert.Equal(teamId, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal(structure.ProjectId, answer.Body.GetProperty("projectId").GetGuid());
        Assert.Equal("Платформа данных", answer.Body.GetProperty("name").GetString());
        Assert.False(answer.Body.GetProperty("isMember").GetBoolean());
        Assert.Equal(JsonValueKind.Null, answer.Body.GetProperty("defaultMark").ValueKind);

        Assert.Equal("Платформа данных", await StoredNameAsync(structure.ProjectId, teamId));
    }

    // mod-017 BR-OT-4 · the answer carries the team's default mark, as every team response does.
    [Fact]
    public async Task Rename_NamedTeam_AnswersNamed()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.SetDefaultTeamAsync(factory, structure.OrganizationId, structure.ProjectId, structure.Platform.Id);

        var answer = await RenameAsync(structure, structure.Platform.Id, new { name = "Платформа данных" });

        Assert.Equal("named", answer.Body.GetProperty("defaultMark").GetString());
    }

    // mod-017 US-4 · "Adding or renaming a team touches no area" (BR-OT-10): the areas keep their names and places. Replaces
    // Rename_KeepsAreaNamesOwnershipAndDefault (D-16).
    [Fact]
    public async Task Rename_TouchesNoArea()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var api = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "API");
        var gateway = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, api.Id, "Шлюз");
        var before = (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).PreOrder().ToList();

        var answer = await RenameAsync(structure, structure.Platform.Id, new { name = "Платформа данных" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal(before, tree.PreOrder());
        Assert.Equal("API / Шлюз", tree.PathOf(gateway.Id));
    }

    // mod-017 US-4 · "any team renamed" (BR-OT-11): the team the project came with is renamed like any other, and the root keeps
    // its own name (it is renamed with its project, § 2.5). Replaces Rename_FirstTeam_Returns200_DefaultStaysTheRoot_RootKeepsItsName.
    [Fact]
    public async Task Rename_AnyTeam_Returns200_RootKeepsItsName()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await RenameAsync(structure, structure.ProjectTeamId, new { name = "Основная команда" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("Основная команда", answer.Body.GetProperty("name").GetString());

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal("Пример проекта", tree.Root.Name);
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
        Assert.Equal("ПЛАТФОРМА", await StoredNameAsync(structure.ProjectId, teamId));
    }

    // US-4 · "A team's name is unique among the project's active teams": «аналитика » is taken by «Аналитика» of the same
    // project, whichever case and spaces. Both teams keep their names.
    [Fact]
    public async Task Rename_ToTakenName_Returns409TeamNameTaken_NameUnchanged()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var analytics = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Аналитика");

        var answer = await RenameAsync(structure, structure.Platform.Id, new { name = " аналитика " });

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.team.name_taken", answer.ErrorCode);
        Assert.Equal("Команда с таким названием уже есть в проекте", answer.Detail);
        Assert.Equal("Платформа", await StoredNameAsync(structure.ProjectId, structure.Platform.Id));
        Assert.Equal("Аналитика", await StoredNameAsync(structure.ProjectId, analytics.Id));
    }

    // US-4 · "«Платформа» is still free as a team name in another project": the rule is per project.
    [Fact]
    public async Task Rename_ToNameOfATeamInAnotherProject_Returns200()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var secondProject = await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");

        var answer = await RenameAsync(structure, structure.Platform.Id, new { name = "Второй проект" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("Второй проект", answer.Body.GetProperty("name").GetString());
        Assert.Equal("Второй проект", await StoredNameAsync(secondProject.Project.Id, secondProject.Team.Id));
    }

    // US-7 · "A deleted team's name is free": a deleted team does not hold its name (ADR-0026 rule 6).
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

    // US-1 · "A visitor who is not signed in sees nothing": the challenge comes before every other check.
    [Fact]
    public async Task Rename_NoToken_Returns401_ChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var teamId = structure.Platform.Id;

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Teams(structure.OrganizationId, structure.ProjectId)}/{teamId}/name",
            new { name = "Платформа данных" });

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal("Платформа", await StoredNameAsync(structure.ProjectId, teamId));
    }

    // US-3 · "A project id names no project of the organization" (BR-M1-20, BR-M1-22): malformed, unknown, or of another
    // organization, the project level answers before the team and the body are looked at.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task Rename_ProjectNotOfOrganization_Returns404ProjectNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var projectId = how switch
        {
            "malformed" => "PRM",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => other.ProjectId.ToString(),
        };

        var answer = await RenameAsync(structure.OrganizationId, projectId, structure.Platform.Id.ToString(), new { name = "" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal("Проект не найден.", answer.Detail);
        Assert.Equal("Платформа", await StoredNameAsync(structure.ProjectId, structure.Platform.Id));
        Assert.Equal("Платформа", await StoredNameAsync(other.ProjectId, other.Platform.Id));
    }

    // US-3 · "A project is deactivated": a write under it answers project.not_found and changes nothing (ADR-0026 rule 5).
    [Fact]
    public async Task Rename_UnderDeactivatedProject_Returns404AndChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);

        var answer = await RenameAsync(structure, structure.Platform.Id, new { name = "Платформа данных" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal("Платформа", await StoredNameAsync(structure.ProjectId, structure.Platform.Id));
    }

    // US-4 · "A team identifier that names no team of the project" (BR-M1-20, BR-M1-22) and US-7 · "A deleted team answers
    // not found": malformed, unknown, of another project or organization, or deleted. The team answers before the body's
    // name, which is bad here too (api-contract § 0.4).
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another project")]
    [InlineData("of another organization")]
    [InlineData("deleted")]
    public async Task Rename_TeamNotAnActiveTeamOfTheProject_Returns404TeamNotFound_ChangesNothing(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var secondProject = await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");
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
            "of another project" => secondProject.Team.Id.ToString(),
            "of another organization" => other.Platform.Id.ToString(),
            _ => core.Id.ToString(),
        };

        var answer = await RenameAsync(structure.OrganizationId, structure.ProjectId.ToString(), teamId, new { name = "" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.team.not_found", answer.ErrorCode);
        Assert.Equal("Команда не найдена.", answer.Detail);
        Assert.Equal("Ядро", await StoredNameAsync(structure.ProjectId, core.Id));
        Assert.Equal("Второй проект", await StoredNameAsync(secondProject.Project.Id, secondProject.Team.Id));
        Assert.Equal("Платформа", await StoredNameAsync(other.ProjectId, other.Platform.Id));
    }

    // US-4 · "an empty name, or one longer than 100 characters, is refused with «Название должно содержать от 1 до 100
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
        Assert.Equal("Платформа", await StoredNameAsync(structure.ProjectId, teamId));
    }

    // US-4 · a name of exactly 100 characters, after trimming, is the longest that passes.
    [Fact]
    public async Task Rename_NameOfOneHundredCharactersAfterTrimming_Returns200()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var hundred = new string('я', 100);

        var answer = await RenameAsync(structure, structure.Platform.Id, new { name = $" {hundred} " });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(hundred, answer.Body.GetProperty("name").GetString());
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
}
