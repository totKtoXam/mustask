using System.Net;
using System.Text.Json;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/areas/{areaId}/name</c>
/// (api-contract § 4.3) through the real host and a real PostgreSQL (lane L-22),
/// written after the reference use case's Api tests (<c>CreateAreaEndpointTests</c>).
/// Every test builds its own organization (<see cref="OrganizationsTestData"/>):
/// the database is shared and never emptied.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class RenameAreaEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> RenameAsync(Guid organizationId, string projectId, string areaId, object body, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"/api/organizations/{organizationId}/projects/{Uri.EscapeDataString(projectId)}/areas/{Uri.EscapeDataString(areaId)}/name",
            body,
            token ?? OrganizationsTokens.SignedIn());

    private Task<Answer> RenameAsync(OrganizationStructure structure, string areaId, object body) =>
        RenameAsync(structure.OrganizationId, structure.ProjectId.ToString(), areaId, body);

    private Task<Answer> RenameAsync(OrganizationStructure structure, Guid areaId, object body) =>
        RenameAsync(structure, areaId.ToString(), body);

    // US-5 · "A child area is renamed": «Отчёты / Квартальные» with children of its own becomes «Отчёты / Годовые», with its
    // children unchanged; the area has no owner to keep (mod-017 BR-OT-10). Renamed (D-16): Rename_KeepsChildrenAndOwner ->
    // Rename_KeepsChildren.
    [Fact]
    public async Task Rename_KeepsChildren()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Квартальные");
        var firstQuarter = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, quarterly.Id, "Первый квартал");
        await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, quarterly.Id, "Второй квартал");

        var answer = await RenameAsync(structure, quarterly.Id, new { name = " Годовые " });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(["id", "name", "parentId", "projectId"], PropertyNames(answer.Body));
        Assert.Equal(quarterly.Id, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal(structure.ProjectId, answer.Body.GetProperty("projectId").GetGuid());
        Assert.Equal(reports.Id, answer.Body.GetProperty("parentId").GetGuid());
        Assert.Equal("Годовые", answer.Body.GetProperty("name").GetString());

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal("Отчёты / Годовые", tree.PathOf(quarterly.Id));
        Assert.Equal("Отчёты / Годовые / Первый квартал", tree.PathOf(firstQuarter.Id));
        Assert.Equal(reports.Id, tree[quarterly.Id].ParentId);
        Assert.Equal(
            ["Второй квартал", "Первый квартал"],
            tree.ChildrenOf(quarterly.Id).Select(node => node.Name).Order(StringComparer.Ordinal));
    }

    // US-5 · "Sibling names are unique, compared ignoring case and spaces": the area's own name in another case is not taken by
    // a sibling, so a change of case is allowed.
    [Fact]
    public async Task Rename_OwnNameInAnotherCase_Returns200()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Квартальные");

        var answer = await RenameAsync(structure, quarterly.Id, new { name = "  КВАРТАЛЬНЫЕ " });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("КВАРТАЛЬНЫЕ", answer.Body.GetProperty("name").GetString());
        Assert.Equal("КВАРТАЛЬНЫЕ", (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId))[quarterly.Id].Name);
    }

    // US-1 · "A visitor who is not signed in sees nothing".
    [Fact]
    public async Task Rename_NoToken_Returns401_ChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Квартальные");

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Areas(structure.OrganizationId, structure.ProjectId)}/{quarterly.Id}/name",
            new { name = "Годовые" });

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal("Квартальные", (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId))[quarterly.Id].Name);
    }

    // US-3 · "A project id names no project of the organization" (BR-M1-20, BR-M1-22): malformed, unknown, or of another
    // organization (which holds the area, but not under that project's address).
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task Rename_ProjectNotOfOrganization_Returns404ProjectNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Квартальные");
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var otherChild = await OrganizationsTestData.AddAreaAsync(factory, other.ProjectId, other.RootId, "Чужая область");
        var projectId = how switch
        {
            "malformed" => "PRM",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => other.ProjectId.ToString(),
        };

        var answer = await RenameAsync(structure.OrganizationId, projectId, quarterly.Id.ToString(), new { name = "Годовые" });
        var foreignArea = await RenameAsync(structure.OrganizationId, projectId, otherChild.Id.ToString(), new { name = "Годовые" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal("Проект не найден.", answer.Detail);
        Assert.Equal("organizations.project.not_found", foreignArea.ErrorCode);
        Assert.Equal("Квартальные", (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId))[quarterly.Id].Name);
        Assert.Equal("Чужая область", (await OrganizationsTestData.ReadTreeAsync(factory, other.ProjectId))[otherChild.Id].Name);
    }

    // US-3 · "A project is deactivated": a write under it answers project.not_found and changes nothing (ADR-0026 rule 5).
    [Fact]
    public async Task Rename_UnderDeactivatedProject_Returns404AndChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Квартальные");
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);

        var answer = await RenameAsync(structure, quarterly.Id, new { name = "Годовые" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal("Квартальные", (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId))[quarterly.Id].Name);
    }

    // BR-M1-22 · an area id that is malformed, unknown, or of another project or organization (a root included) answers
    // area.not_found, and no tree changes.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another project")]
    [InlineData("root of another project")]
    [InlineData("of another organization")]
    public async Task Rename_AreaNotOfProject_Returns404AreaNotFound_NoTreeChanges(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Квартальные");
        var secondProject = await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");
        var secondChild = await OrganizationsTestData.AddAreaAsync(factory, secondProject.Project.Id, secondProject.Root.Id, "Чужая область");
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var otherChild = await OrganizationsTestData.AddAreaAsync(factory, other.ProjectId, other.RootId, "Чужая область");
        var areaId = how switch
        {
            "malformed" => quarterly.Id.ToString("N"),
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another project" => secondChild.Id.ToString(),
            "root of another project" => secondProject.Root.Id.ToString(),
            _ => otherChild.Id.ToString(),
        };

        var answer = await RenameAsync(structure, areaId, new { name = "Годовые" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.area.not_found", answer.ErrorCode);
        Assert.Equal("Область не найдена.", answer.Detail);
        Assert.Equal("Квартальные", (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId))[quarterly.Id].Name);

        var secondTree = await OrganizationsTestData.ReadTreeAsync(factory, secondProject.Project.Id);
        Assert.Equal("Чужая область", secondTree[secondChild.Id].Name);
        Assert.Equal("Второй проект", secondTree.Root.Name);
        Assert.Equal("Чужая область", (await OrganizationsTestData.ReadTreeAsync(factory, other.ProjectId))[otherChild.Id].Name);
    }

    // api-contract § 0.4: the address answers before the body's values, so an unknown area with an empty name is a 404.
    [Fact]
    public async Task Rename_UnknownAreaAndInvalidName_Returns404AreaNotFound_BeforeTheBody()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await RenameAsync(structure, Guid.CreateVersion7(), new { name = "" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.area.not_found", answer.ErrorCode);
    }

    // US-5 · "The root comes with the project and follows it": a direct rename of the root is refused and changes nothing
    // (BR-M1-11). The rule about the addressed area answers before the body's values (api-contract § 0.4 step 2 before 3).
    [Theory]
    [InlineData("Новое название")]
    [InlineData("")]
    public async Task Rename_Root_Returns409RootIsFixed_ChangesNothing(string name)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await RenameAsync(structure, structure.RootId, new { name });

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.area.root_is_fixed", answer.ErrorCode);
        Assert.Equal("Корневая область меняется только вместе с проектом", answer.Detail);
        Assert.Equal("Пример проекта", (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Root.Name);
    }

    // US-5 · "an empty name, or one longer than 100 characters, is refused with «Название должно содержать от 1 до 100
    // символов»"; the area keeps its name.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("101")]
    public async Task Rename_InvalidName_Returns400NameLength_ChangesNothing(string? name)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Квартальные");

        var answer = await RenameAsync(structure, quarterly.Id, new { name = name == "101" ? new string('я', 101) : name });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("organizations.validation_failed", answer.ErrorCode);
        var reason = Assert.Single(answer.Body.GetProperty("errors").GetProperty("name").EnumerateArray());
        Assert.Equal("organizations.name_length", reason.GetProperty("code").GetString());
        Assert.Equal("Название должно содержать от 1 до 100 символов", reason.GetProperty("detail").GetString());
        Assert.Equal("Квартальные", (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId))[quarterly.Id].Name);
    }

    // api-contract § 0.2: a field that is not a JSON string fails binding with the platform's 400.
    [Fact]
    public async Task Rename_NameIsANumber_Returns400BadRequest()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Квартальные");

        var answer = await RenameAsync(structure, quarterly.Id, new { name = 42 });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("http.bad_request", answer.ErrorCode);
    }

    // US-5 · "Sibling names are unique, compared ignoring case and spaces ... or a child of «Отчёты» is renamed to it":
    // refused, the name unchanged. «Месячные» is free under «Отчёты» although «Архив» has it, and the root's name is
    // nobody's sibling name.
    [Fact]
    public async Task Rename_ToSiblingName_Returns409AreaNameTaken_SameNameUnderAnotherParentIsFree()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Квартальные");
        await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Годовые");
        var archive = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Архив");
        await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, archive.Id, "Месячные");

        var taken = await RenameAsync(structure, quarterly.Id, new { name = " годовые " });

        Assert.Equal(HttpStatusCode.Conflict, taken.Status);
        Assert.Equal("organizations.area.name_taken", taken.ErrorCode);
        Assert.Equal("Область с таким названием уже есть на этом уровне", taken.Detail);
        Assert.Equal("Квартальные", (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId))[quarterly.Id].Name);

        var elsewhere = await RenameAsync(structure, quarterly.Id, new { name = "Месячные" });
        var rootsName = await RenameAsync(structure, quarterly.Id, new { name = "Пример проекта" });

        Assert.Equal(HttpStatusCode.OK, elsewhere.Status);
        Assert.Equal(HttpStatusCode.OK, rootsName.Status);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal(["Годовые", "Пример проекта"], tree.ChildrenOf(reports.Id).Select(node => node.Name));
        Assert.Equal(["Месячные"], tree.ChildrenOf(archive.Id).Select(node => node.Name));
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
}
