using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/name</c>
/// (api-contract § 2.5) through the real host and a real PostgreSQL (plan-backend
/// § 14.3, lane L-12). Every test builds its own organization
/// (<see cref="OrganizationsTestData"/>): the database is shared and never
/// emptied. What a rename left behind is read back through the repositories,
/// whatever the project's status, so no test depends on another lane's read.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class RenameProjectEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> RenameAsync(Guid organizationId, string projectId, object? body, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"/api/organizations/{organizationId}/projects/{Uri.EscapeDataString(projectId)}/name",
            body,
            token ?? OrganizationsTokens.SignedIn());

    private Task<Answer> RenameAsync(OrganizationStructure structure, string? name) =>
        RenameAsync(structure.OrganizationId, structure.ProjectId.ToString(), new { name });

    private Task<Project> ReadProjectAsync(Guid organizationId, Guid projectId) =>
        OrganizationsTestData.InUnitOfWorkAsync(
            factory,
            async (services, ct) => await services.GetRequiredService<IProjectRepository>().LockAsync(organizationId, projectId, ct)
                ?? throw new InvalidOperationException("The project is gone."));

    private Task<string?> TeamNameAsync(Guid teamId) =>
        OrganizationsTestData.InUnitOfWorkAsync(
            factory,
            (services, ct) => services.GetRequiredService<ITeamRepository>().FindNameAsync(teamId, ct));

    // feat-028 US-2 · "rename keeps description and links": the rename writes the name and the root's name; the project's description
    // (kept as typed), its links with who made them and when, and the setting that names a team stay as they were.
    [Fact]
    public async Task Rename_KeepsDescriptionAndLinks()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.SetDefaultTeamAsync(factory, structure.OrganizationId, structure.ProjectId, structure.Platform.Id);
        await Mustask.Api.IntegrationTests.Features.WorkItems.WorkItemsTestData.ExecuteAsync(
            factory, $"UPDATE projects SET description = {" Платформа\nи инструменты "} WHERE id = {structure.ProjectId}");
        var linksBefore = (await TeamTestData.LinksOfProjectAsync(factory, structure.ProjectId))
            .Select(link => (link.TeamId, link.LinkedBy, link.LinkedAt))
            .ToList();

        var answer = await RenameAsync(structure, "Платформа данных");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(" Платформа\nи инструменты ", answer.Body.GetProperty("description").GetString());
        Assert.Equal(
            linksBefore,
            (await TeamTestData.LinksOfProjectAsync(factory, structure.ProjectId)).Select(link => (link.TeamId, link.LinkedBy, link.LinkedAt)).ToList());
        Assert.Equal(structure.Platform.Id, await TeamTestData.SettingAsync(factory, structure.ProjectId));
    }

    // US-3 · "Renaming a project renames its root area and keeps its key": «Пример проекта» (PRM) becomes «Платформа данных».
    // The root takes the name, the key and the teams (the project's own team keeps «Пример проекта») do not
    // change, and neither does the rest of the tree.
    [Fact]
    public async Task Rename_RenamesRoot_KeepsKeyTeamsAndTree()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Квартальные");
        var before = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);

        var answer = await RenameAsync(structure, " Платформа данных ");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(["description", "id", "key", "name", "organizationId"], PropertyNames(answer.Body));
        Assert.Equal(structure.ProjectId, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal(structure.OrganizationId, answer.Body.GetProperty("organizationId").GetGuid());
        Assert.Equal("Платформа данных", answer.Body.GetProperty("name").GetString());
        Assert.Equal("PRM", answer.Body.GetProperty("key").GetString());

        var project = await ReadProjectAsync(structure.OrganizationId, structure.ProjectId);
        Assert.Equal("Платформа данных", project.Name);
        Assert.Equal("PRM", project.Key);
        Assert.Equal(ProjectStatus.Active, project.Status);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal("Платформа данных", tree.Root.Name);
        Assert.Equal(structure.RootId, tree.Root.Id);
        Assert.Equal(before.Count, tree.Count);
        Assert.Equal("Платформа данных / Отчёты / Квартальные", tree.PathOf(quarterly.Id));
        Assert.Equal(
            before.PreOrder().Where(node => node.Id != structure.RootId),
            tree.PreOrder().Where(node => node.Id != structure.RootId));

        Assert.Equal("Пример проекта", await TeamNameAsync(structure.ProjectTeamId));
        Assert.Equal("Платформа", await TeamNameAsync(structure.Platform.Id));
    }

    // US-3 · "The name is unique in the organization" and api-contract § 0.6: a name never conflicts with the object's own
    // current name, so «ПРИМЕР ПРОЕКТА» renames «Пример проекта» to another case of itself.
    [Fact]
    public async Task Rename_OwnNameInAnotherCase_Returns200_RootFollows()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await RenameAsync(structure, "  ПРИМЕР ПРОЕКТА");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("ПРИМЕР ПРОЕКТА", answer.Body.GetProperty("name").GetString());
        Assert.Equal("ПРИМЕР ПРОЕКТА", (await ReadProjectAsync(structure.OrganizationId, structure.ProjectId)).Name);
        Assert.Equal("ПРИМЕР ПРОЕКТА", (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Root.Name);
    }

    // US-3 · "Renaming a project ... keeps its key" and "no action anywhere changes the key" (BR-M1-1; QA pass 1): a rename
    // whose body also carries a well-formed, free key renames the project and ignores the key.
    [Fact]
    public async Task Rename_BodyAlsoCarryingAKey_RenamesOnly_TheKeyStays()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await RenameAsync(
            structure.OrganizationId, structure.ProjectId.ToString(), new { name = "Платформа данных", key = "NEWKEY" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("PRM", answer.Body.GetProperty("key").GetString());

        var project = await ReadProjectAsync(structure.OrganizationId, structure.ProjectId);
        Assert.Equal(("Платформа данных", "PRM"), (project.Name, project.Key));
    }

    // US-1 · "A visitor who is not signed in sees nothing".
    [Fact]
    public async Task Rename_NoToken_Returns401_ChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Project(structure.OrganizationId, structure.ProjectId)}/name",
            new { name = "Платформа данных" });

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal("Пример проекта", (await ReadProjectAsync(structure.OrganizationId, structure.ProjectId)).Name);
        Assert.Equal("Пример проекта", (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Root.Name);
    }

    // US-3 · "A project identifier that names no project of the organization" (BR-M1-20, BR-M1-22): malformed, unknown, or of
    // another organization, the project level answers before the body is looked at, and nothing changes.
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

        var answer = await RenameAsync(structure.OrganizationId, projectId, new { name = "" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal("Проект не найден.", answer.Detail);
        Assert.Equal("Пример проекта", (await ReadProjectAsync(other.OrganizationId, other.ProjectId)).Name);
        Assert.Equal("Пример проекта", (await OrganizationsTestData.ReadTreeAsync(factory, other.ProjectId)).Root.Name);
    }

    // US-3 · "A project is deactivated": a write under it answers project.not_found and changes nothing (ADR-0026 rule 5),
    // the name and the root's included.
    [Fact]
    public async Task Rename_UnderDeactivatedProject_Returns404AndChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);

        var answer = await RenameAsync(structure, "Платформа данных");

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        var project = await ReadProjectAsync(structure.OrganizationId, structure.ProjectId);
        Assert.Equal("Пример проекта", project.Name);
        Assert.Equal(ProjectStatus.Deactivated, project.Status);
        Assert.Equal("Пример проекта", (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Root.Name);
    }

    // US-3 · "The name is unique ... an empty name, or one longer than 100 characters, is refused with «Название должно
    // содержать от 1 до 100 символов»": 400 with the one reason on `name`, and nothing changes.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("101")]
    public async Task Rename_InvalidName_Returns400NameLength_ChangesNothing(string? name)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await RenameAsync(structure, name == "101" ? new string('я', 101) : name);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("organizations.validation_failed", answer.ErrorCode);
        var reason = Assert.Single(answer.Body.GetProperty("errors").GetProperty("name").EnumerateArray());
        Assert.Equal("organizations.name_length", reason.GetProperty("code").GetString());
        Assert.Equal("Название должно содержать от 1 до 100 символов", reason.GetProperty("detail").GetString());
        Assert.Equal("Пример проекта", (await ReadProjectAsync(structure.OrganizationId, structure.ProjectId)).Name);
        Assert.Equal("Пример проекта", (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Root.Name);
    }

    // US-3 · "The name is unique in the organization": «альфа » is refused for the project whose neighbour is «Альфа», in any
    // case and with surrounding spaces, and nothing changes. The name is still free in another organization.
    [Fact]
    public async Task Rename_ToTakenName_Returns409NameTaken_ChangesNothing_FreeInAnotherOrganization()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Альфа", "ALF");
        var other = await OrganizationsTestData.CreateStructureAsync(factory);

        var taken = await RenameAsync(structure, " альфа ");
        var elsewhere = await RenameAsync(other, "Альфа");

        Assert.Equal(HttpStatusCode.Conflict, taken.Status);
        Assert.Equal("organizations.project.name_taken", taken.ErrorCode);
        Assert.Equal("Проект с таким названием уже есть в организации", taken.Detail);
        Assert.Equal("Пример проекта", (await ReadProjectAsync(structure.OrganizationId, structure.ProjectId)).Name);
        Assert.Equal("Пример проекта", (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Root.Name);

        Assert.Equal(HttpStatusCode.OK, elsewhere.Status);
        Assert.Equal("Альфа", (await ReadProjectAsync(other.OrganizationId, other.ProjectId)).Name);
        Assert.Equal("Альфа", (await OrganizationsTestData.ReadTreeAsync(factory, other.ProjectId)).Root.Name);
    }

    // US-3 · "A deactivated project's name stays reserved": the holder is deactivated, so the 409 has its own code and
    // text, and nothing changes.
    [Fact]
    public async Task Rename_ToNameOfDeactivated_Returns409NameTakenByDeactivated_ChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Архив", "ARC", ProjectStatus.Deactivated);

        var answer = await RenameAsync(structure, "архив ");

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.project.name_taken_by_deactivated", answer.ErrorCode);
        Assert.Equal("Проект с таким названием уже есть в организации: он деактивирован", answer.Detail);
        Assert.Equal("Пример проекта", (await ReadProjectAsync(structure.OrganizationId, structure.ProjectId)).Name);
        Assert.Equal("Пример проекта", (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Root.Name);
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
}
