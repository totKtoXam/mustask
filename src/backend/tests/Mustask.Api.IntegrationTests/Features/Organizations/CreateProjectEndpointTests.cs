using System.Net;
using System.Text.Json;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects</c> (api-contract § 2.3)
/// through the real host and a real PostgreSQL. Every test builds its own
/// organization (<see cref="OrganizationsTestData"/>): the database is shared
/// and never emptied. Results are read through the module's read side and the
/// cross-module seam, not through other lanes' endpoints, so the lane stands
/// alone.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class CreateProjectEndpointTests(MustaskApiFactory factory)
{
    private const string NameTaken = "organizations.project.name_taken";
    private const string NameTakenByDeactivated = "organizations.project.name_taken_by_deactivated";
    private const string KeyTaken = "organizations.project.key_taken";

    private Task<Answer> CreateAsync(string organizationId, object? body, string? token = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            $"/api/organizations/{Uri.EscapeDataString(organizationId)}/projects",
            body,
            token ?? OrganizationsTokens.SignedIn(),
            gate);

    private Task<Answer> CreateAsync(Guid organizationId, object? body, string? token = null, CommitGate? gate = null) =>
        CreateAsync(organizationId.ToString(), body, token, gate);

    // US-3 · "A project is created ready to use": active, listed with its name and key; one team and a root area, both named
    // after the project, the root owned by that team for good and its default.
    [Fact]
    public async Task Create_Returns201_WithFirstTeam_RootOwnedByItAndItsDefault()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await CreateAsync(organization.Id, new { name = " Пример проекта ", key = "PRM" });

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        var projectId = answer.Body.GetProperty("id").GetGuid();
        Assert.Equal($"/api/organizations/{organization.Id}/projects/{projectId}", answer.Location?.OriginalString);
        Assert.Equal(["id", "key", "name", "organizationId"], PropertyNames(answer.Body));
        Assert.Equal(organization.Id, answer.Body.GetProperty("organizationId").GetGuid());
        Assert.Equal("Пример проекта", answer.Body.GetProperty("name").GetString());
        Assert.Equal("PRM", answer.Body.GetProperty("key").GetString());

        using var scope = factory.Services.CreateScope();
        var structure = scope.ServiceProvider.GetRequiredService<IOrganizationStructure>();

        var project = await structure.FindProjectAsync(projectId, CancellationToken.None);
        Assert.NotNull(project);
        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.True(project.IsReachable);
        Assert.Equal(organization.Id, project.OrganizationId);

        var firstTeam = await structure.FindTeamAsync(project.FirstTeamId, CancellationToken.None);
        Assert.NotNull(firstTeam);
        Assert.Equal("Пример проекта", firstTeam.Name);
        Assert.True(firstTeam.IsFirstTeam);
        Assert.Equal(TeamStatus.Active, firstTeam.Status);
        Assert.Equal(project.RootAreaId, firstTeam.DefaultAreaId);
        Assert.Equal(project.FirstTeamId, Assert.Single(await structure.ListActiveTeamsAsync(projectId, CancellationToken.None)).Id);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, projectId);
        Assert.Equal(1, tree.Count);
        Assert.Equal(project.RootAreaId, tree.Root.Id);
        Assert.Equal("Пример проекта", tree.Root.Name);
        Assert.Equal(project.FirstTeamId, tree.Root.OwnerTeamId);
        Assert.Equal(project.FirstTeamId, tree.Root.DefaultOfTeamId);
        Assert.Equal(new ResolvedOwner(project.FirstTeamId, false), tree.ResolveOwner(tree.Root.Id));

        var listed = await ListActiveProjectsAsync(organization.Id);
        Assert.Equal(new ProjectRow(projectId, organization.Id, "Пример проекта", "PRM"), Assert.Single(listed));
        Assert.Equal((1, 1, 1), await CountAsync(organization.Id));
    }

    // US-3 · "A project is created ready to use": it keeps who created it and when (BR-M1-21). The creator is the token's
    // subject, and the time falls between the request's start and its answer.
    [Fact]
    public async Task Create_RecordsCreator()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var subject = OrganizationsTokens.NewSubject();

        var started = DateTimeOffset.UtcNow;
        var answer = await CreateAsync(organization.Id, new { name = "Пример проекта", key = "PRM" }, OrganizationsTokens.SignedIn(subject));
        var answered = DateTimeOffset.UtcNow;

        Assert.Equal(HttpStatusCode.Created, answer.Status);

        using var scope = factory.Services.CreateScope();
        var project = await scope.ServiceProvider.GetRequiredService<IOrganizationStructure>()
            .FindProjectAsync(answer.Body.GetProperty("id").GetGuid(), CancellationToken.None);

        Assert.NotNull(project);
        Assert.Equal(subject, project.CreatedBy);
        Assert.InRange(project.CreatedAt, started, answered);
    }

    // US-3 · "A project is created ready to use": it uses the organization's process (BR-M1-3). A project has no process of
    // its own: through the seam its organization's process id equals the one mod-001's process read answers.
    [Fact]
    public async Task Create_UsesTheOrganizationsProcess()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var created = await CreateAsync(organization.Id, new { name = "Пример проекта", key = "PRM" });
        var process = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"{OrganizationsApi.Organization(organization.Id)}/process",
            token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal(HttpStatusCode.OK, process.Status);

        using var scope = factory.Services.CreateScope();
        var structure = scope.ServiceProvider.GetRequiredService<IOrganizationStructure>();
        var project = await structure.FindProjectAsync(created.Body.GetProperty("id").GetGuid(), CancellationToken.None);
        Assert.NotNull(project);
        var owner = await structure.FindOrganizationAsync(project.OrganizationId, CancellationToken.None);

        Assert.NotNull(owner);
        Assert.Equal(organization.ProcessId, owner.ProcessId);
        Assert.Equal(process.Body.GetProperty("id").GetGuid(), owner.ProcessId.Value);
    }

    // US-1 · "A visitor who is not signed in sees nothing": the anonymous request is refused before anything else.
    [Fact]
    public async Task Create_NoToken_Returns401_CreatesNothing()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            $"{OrganizationsApi.Organization(organization.Id)}/projects",
            new { name = "Пример проекта", key = "PRM" });

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal((0, 0, 0), await CountAsync(organization.Id));
    }

    // US-2 · "A deactivated organization takes no change; an unknown one answers not found" (BR-M1-20): the organization
    // level answers before the body is looked at (the body is wrong too), and nothing is created.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("deactivated")]
    public async Task Create_OrganizationNotActive_Returns404OrganizationNotFound_CreatesNothing(string how)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(
            factory, status: how == "deactivated" ? OrganizationStatus.Deactivated : OrganizationStatus.Active);
        var organizationId = how switch
        {
            "malformed" => "PRM",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => organization.Id.ToString(),
        };

        var answer = await CreateAsync(organizationId, new { name = "", key = "prm" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.organization.not_found", answer.ErrorCode);
        Assert.Equal("Организация не найдена.", answer.Detail);
        Assert.Equal((0, 0, 0), await CountAsync(organization.Id));
    }

    // US-3 · "The name is unique in the organization ... an empty name, or one longer than 100 characters, is refused with
    // «Название должно содержать от 1 до 100 символов»": the name field only, and no project is created.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("101")]
    public async Task Create_InvalidName_Returns400NameLength_CreatesNothing(string? name)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await CreateAsync(organization.Id, new { name = name == "101" ? new string('я', 101) : name, key = "PRM" });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("organizations.validation_failed", answer.ErrorCode);
        var errors = answer.Body.GetProperty("errors");
        Assert.Equal(["name"], FieldNames(errors));
        var reason = Assert.Single(errors.GetProperty("name").EnumerateArray());
        Assert.Equal("organizations.name_length", reason.GetProperty("code").GetString());
        Assert.Equal("Название должно содержать от 1 до 100 символов", reason.GetProperty("detail").GetString());
        Assert.Equal((0, 0, 0), await CountAsync(organization.Id));
    }

    // US-3 · "The key is required and well-formed": an empty key is refused with «Укажите ключ проекта», and `prm`, `1PRM`,
    // `P`, `PROJECTKEY01`, and `РМК` typed in Cyrillic each with the «Ключ проекта: от 2 до 10 ...» text; no project is created.
    [Theory]
    [InlineData(null, "organizations.project.key_required", "Укажите ключ проекта")]
    [InlineData("", "organizations.project.key_required", "Укажите ключ проекта")]
    [InlineData("prm", "organizations.project.key_invalid", "Ключ проекта: от 2 до 10 латинских заглавных букв и цифр, начиная с буквы")]
    [InlineData("1PRM", "organizations.project.key_invalid", "Ключ проекта: от 2 до 10 латинских заглавных букв и цифр, начиная с буквы")]
    [InlineData("P", "organizations.project.key_invalid", "Ключ проекта: от 2 до 10 латинских заглавных букв и цифр, начиная с буквы")]
    [InlineData("PROJECTKEY01", "organizations.project.key_invalid", "Ключ проекта: от 2 до 10 латинских заглавных букв и цифр, начиная с буквы")]
    [InlineData("РМК", "organizations.project.key_invalid", "Ключ проекта: от 2 до 10 латинских заглавных букв и цифр, начиная с буквы")]
    [InlineData(" PRM ", "organizations.project.key_invalid", "Ключ проекта: от 2 до 10 латинских заглавных букв и цифр, начиная с буквы")]
    public async Task Create_KeyMissingOrMalformed_Returns400WithTheKeyReason(string? key, string code, string text)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await CreateAsync(organization.Id, new { name = "Пример проекта", key });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("organizations.validation_failed", answer.ErrorCode);
        var errors = answer.Body.GetProperty("errors");
        Assert.Equal(["key"], FieldNames(errors));
        var reason = Assert.Single(errors.GetProperty("key").EnumerateArray());
        Assert.Equal(code, reason.GetProperty("code").GetString());
        Assert.Equal(text, reason.GetProperty("detail").GetString());
        Assert.Equal((0, 0, 0), await CountAsync(organization.Id));
    }

    // US-3 · "The key is required and well-formed": a request with no key field at all is a missing key, not a binding error.
    [Fact]
    public async Task Create_KeyAbsentFromTheBody_Returns400KeyRequired()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await CreateAsync(organization.Id, new { name = "Пример проекта" });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("organizations.validation_failed", answer.ErrorCode);
        var reason = Assert.Single(answer.Body.GetProperty("errors").GetProperty("key").EnumerateArray());
        Assert.Equal("organizations.project.key_required", reason.GetProperty("code").GetString());
    }

    // api-contract § 0.5: every field reason at once, so a bad name and a bad key come back together.
    [Fact]
    public async Task Create_NameAndKeyBothBad_Returns400WithBothReasons()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await CreateAsync(organization.Id, new { name = "   ", key = "prm" });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("organizations.validation_failed", answer.ErrorCode);
        var errors = answer.Body.GetProperty("errors");
        Assert.Equal(["key", "name"], FieldNames(errors));
        Assert.Equal(
            "organizations.name_length",
            Assert.Single(errors.GetProperty("name").EnumerateArray()).GetProperty("code").GetString());
        Assert.Equal(
            "organizations.project.key_invalid",
            Assert.Single(errors.GetProperty("key").EnumerateArray()).GetProperty("code").GetString());
        Assert.Equal((0, 0, 0), await CountAsync(organization.Id));
    }

    // US-3 · "The name is unique in the organization": «пример проекта » against the active «Пример проекта» is refused with
    // «Проект с таким названием уже есть в организации», and nothing is created.
    [Fact]
    public async Task Create_NameTakenAnyCase_Returns409NameTaken()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Пример проекта", "PRM");

        var answer = await CreateAsync(organization.Id, new { name = " пример ПРОЕКТА ", key = "OTH" });

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal(NameTaken, answer.ErrorCode);
        Assert.Equal("Проект с таким названием уже есть в организации", answer.Detail);
        Assert.Equal((1, 1, 1), await CountAsync(organization.Id));
    }

    // US-3 · "A deactivated project's name stays reserved": the same name against a deactivated project is refused with
    // «... уже есть в организации: он деактивирован» (BR-M1-1), and nothing is created.
    [Fact]
    public async Task Create_NameOfDeactivated_Returns409NameTakenByDeactivated()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Пример проекта", "PRM", ProjectStatus.Deactivated);

        var answer = await CreateAsync(organization.Id, new { name = "Пример проекта", key = "OTH" });

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal(NameTakenByDeactivated, answer.ErrorCode);
        Assert.Equal("Проект с таким названием уже есть в организации: он деактивирован", answer.Detail);
        Assert.Equal((1, 1, 1), await CountAsync(organization.Id));
    }

    // US-3 · "The name is unique in the organization ... the name is still free in «Бета»" (BR-M1-1): the same name, active or
    // deactivated, in another organization holds nothing against this one.
    [Fact]
    public async Task Create_SameNameInAnotherOrganization_Succeeds()
    {
        var alpha = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var beta = await OrganizationsTestData.CreateOrganizationAsync(factory);
        await OrganizationsTestData.CreateProjectAsync(factory, alpha.Id, "Пример проекта", "PRM");
        await OrganizationsTestData.CreateProjectAsync(factory, alpha.Id, "Архивный", "OLD", ProjectStatus.Deactivated);

        var active = await CreateAsync(beta.Id, new { name = "Пример проекта", key = "BETA" });
        var deactivated = await CreateAsync(beta.Id, new { name = "Архивный", key = "BETB" });

        Assert.Equal(HttpStatusCode.Created, active.Status);
        Assert.Equal(HttpStatusCode.Created, deactivated.Status);
        Assert.Equal((2, 2, 2), await CountAsync(beta.Id));
    }

    // US-3 · "The key is unique in the organization, deactivated projects included": `PRM` held by an active or a deactivated
    // project is refused with «Ключ «PRM» уже используется в организации», with the key as the parameter.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_KeyOfActiveOrDeactivated_Returns409KeyTaken(bool deactivated)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        await OrganizationsTestData.CreateProjectAsync(
            factory, organization.Id, "Пример проекта", "PRM", deactivated ? ProjectStatus.Deactivated : ProjectStatus.Active);

        var answer = await CreateAsync(organization.Id, new { name = "Другой проект", key = "PRM" });

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal(KeyTaken, answer.ErrorCode);
        Assert.Equal("Ключ «PRM» уже используется в организации", answer.Detail);
        Assert.Equal("PRM", answer.Body.GetProperty("params").GetProperty("key").GetProperty("value").GetString());
        Assert.Equal((1, 1, 1), await CountAsync(organization.Id));
    }

    // US-3 · "The key is unique in the organization ... the key `PRM` is still free in the organization «Бета»" (BR-M1-1).
    [Fact]
    public async Task Create_SameKeyInAnotherOrganization_Succeeds()
    {
        var alpha = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var beta = await OrganizationsTestData.CreateOrganizationAsync(factory);
        await OrganizationsTestData.CreateProjectAsync(factory, alpha.Id, "Пример проекта", "PRM");

        var answer = await CreateAsync(beta.Id, new { name = "Другой проект", key = "PRM" });

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal("PRM", answer.Body.GetProperty("key").GetString());
        Assert.Equal((1, 1, 1), await CountAsync(alpha.Id));
        Assert.Equal((1, 1, 1), await CountAsync(beta.Id));
    }

    // api-contract § 2.3: "When both the name and the key are taken, the name's code is answered."
    [Fact]
    public async Task Create_NameAndKeyBothTaken_Returns409NameTaken()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Пример проекта", "PRM");

        var answer = await CreateAsync(organization.Id, new { name = "Пример проекта", key = "PRM" });

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal(NameTaken, answer.ErrorCode);
    }

    // US-3 · "Two projects with one key at the same moment" (NFR Concurrency). Request A is held at its commit with its rows
    // written; B passes both pre-checks (A is not committed), then blocks on the key index until A commits, and loses with
    // the pre-check's code and the key as its parameter (plan-backend § 6.3, § 8.3). Exactly one project, with its team and
    // its root, exists, and the loser leaves no orphan team or area.
    [Fact]
    public async Task Create_TwoConcurrentSameKey_OneCreated_OtherRefused_NothingOrphaned()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        using var gate = factory.CommitGates.Open();

        var first = CreateAsync(organization.Id, new { name = "Первый проект", key = "RACE" }, gate: gate);
        await Task.WhenAny(gate.Reached, first);
        Assert.False(first.IsCompleted, "The first creation finished before reaching its commit gate.");

        var second = CreateAsync(organization.Id, new { name = "Второй проект", key = "RACE" });
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "The second creation did not wait for the first one's uncommitted row.");

        gate.Release();
        var (a, b) = (await first, await second);

        Assert.Equal(HttpStatusCode.Created, a.Status);
        Assert.Equal(HttpStatusCode.Conflict, b.Status);
        Assert.Equal(KeyTaken, b.ErrorCode);
        Assert.Equal("RACE", b.Body.GetProperty("params").GetProperty("key").GetProperty("value").GetString());
        Assert.Equal("Ключ «RACE» уже используется в организации", b.Detail);

        var project = Assert.Single(await ListActiveProjectsAsync(organization.Id));
        Assert.Equal(a.Body.GetProperty("id").GetGuid(), project.Id);
        Assert.Equal((1, 1, 1), await CountAsync(organization.Id));
    }

    // US-3 · "The name is unique in the organization" under the same moment: the loser blocks on the name index, then answers
    // the plain name code ("or a concurrent creation took it first", api-contract § 2.3).
    [Fact]
    public async Task Create_TwoConcurrentSameName_OneCreated_OtherRefused_NothingOrphaned()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        using var gate = factory.CommitGates.Open();

        var first = CreateAsync(organization.Id, new { name = "Гоночный проект", key = "RACEA" }, gate: gate);
        await Task.WhenAny(gate.Reached, first);
        Assert.False(first.IsCompleted, "The first creation finished before reaching its commit gate.");

        var second = CreateAsync(organization.Id, new { name = " гоночный ПРОЕКТ ", key = "RACEB" });
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "The second creation did not wait for the first one's uncommitted row.");

        gate.Release();
        var (a, b) = (await first, await second);

        Assert.Equal(HttpStatusCode.Created, a.Status);
        Assert.Equal(HttpStatusCode.Conflict, b.Status);
        Assert.Equal(NameTaken, b.ErrorCode);
        Assert.Equal((1, 1, 1), await CountAsync(organization.Id));
    }

    private async Task<IReadOnlyList<ProjectRow>> ListActiveProjectsAsync(Guid organizationId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IProjectReadModel>()
            .ListAsync(organizationId, ProjectStatus.Active, CancellationToken.None);
    }

    /// <summary>What is stored for the organization whatever its status: its projects, and the teams and areas of those projects.</summary>
    private async Task<(int Projects, int Teams, int Areas)> CountAsync(Guid organizationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        var projectIds = db.Projects.AsNoTracking().Where(project => project.OrganizationId == organizationId).Select(project => project.Id);

        return (
            await projectIds.CountAsync(),
            await db.Teams.AsNoTracking().CountAsync(team => projectIds.Contains(team.ProjectId)),
            await db.Areas.AsNoTracking().CountAsync(area => projectIds.Contains(area.ProjectId)));
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);

    private static IEnumerable<string> FieldNames(JsonElement errors) => PropertyNames(errors);
}
