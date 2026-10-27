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

    // feat-028 US-2 · "A project is created ready to use" (BR-OL-2): active, listed with its name and key, a root area named after
    // the project and owned by nobody — and NO team and no team membership: a team is the organization's, and the project is linked
    // to one on purpose. Retires Create_Returns201_WithOneOrdinaryTeamAndTheRoot (mod-017 BR-OT-11, the project's first team;
    // doc-012 P-1).
    [Fact]
    public async Task Create_Returns201_WithTheRootAndNoTeam()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await CreateAsync(organization.Id, new { name = " Пример проекта ", key = "PRM" });

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        var projectId = answer.Body.GetProperty("id").GetGuid();
        Assert.Equal($"/api/organizations/{organization.Id}/projects/{projectId}", answer.Location?.OriginalString);
        Assert.Equal(["description", "id", "key", "name", "organizationId"], PropertyNames(answer.Body));
        Assert.Equal(organization.Id, answer.Body.GetProperty("organizationId").GetGuid());
        Assert.Equal("Пример проекта", answer.Body.GetProperty("name").GetString());
        Assert.Equal("PRM", answer.Body.GetProperty("key").GetString());
        Assert.Equal(JsonValueKind.Null, answer.Body.GetProperty("description").ValueKind);

        using var scope = factory.Services.CreateScope();
        var structure = scope.ServiceProvider.GetRequiredService<IOrganizationStructure>();

        var project = await structure.FindProjectAsync(organization.Id, projectId, CancellationToken.None);
        Assert.NotNull(project);
        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.True(project.IsReachable);
        Assert.Equal(organization.Id, project.OrganizationId);

        // No team, so no default; the setting names none; nothing is linked and the organization has no team at all.
        Assert.Null(project.DefaultTeamId);
        Assert.Null(await StoredDefaultSettingAsync(projectId));
        Assert.Empty(await structure.ListLinkedTeamsAsync(organization.Id, projectId, CancellationToken.None));
        Assert.Empty(await TeamTestData.LinksOfProjectAsync(factory, projectId));

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, projectId);
        Assert.Equal(0, tree.Count);

        var listed = await ListActiveProjectsAsync(organization.Id);
        Assert.Equal(new ProjectRow(projectId, organization.Id, "Пример проекта", "PRM"), Assert.Single(listed));
        Assert.Equal((1, 0, 0), await CountAsync(organization.Id));
    }

    // feat-028 US-2 · "description in the same operation": the description is kept as typed — spaces and line breaks inside it stay,
    // the empty string is none (§ 0.2) — and the answer and a read carry it.
    [Theory]
    [InlineData("  Платформа и\nинструменты ", "  Платформа и\nинструменты ")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public async Task Create_WithDescription_KeptAsTyped_EmptyIsNone(string? sent, string? stored)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await CreateAsync(organization.Id, new { name = "Пример проекта", key = "PRM", description = sent });
        var read = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"{OrganizationsApi.Organization(organization.Id)}/projects/{answer.Body.GetProperty("id").GetGuid()}",
            token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(stored, answer.Body.GetProperty("description").GetString());
        Assert.Equal(stored, read.Body.GetProperty("description").GetString());
    }

    // feat-028 US-2 · "a description longer than 2000 characters is refused with «Описание должно содержать не более 2000 символов»",
    // together with a bad name (every reason at once), and nothing is created; 2000 exactly passes.
    [Fact]
    public async Task Create_DescriptionTooLong_Returns400_WithTheNameReasonToo_CreatesNothing()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await CreateAsync(organization.Id, new { name = "  ", key = "PRM", description = new string('я', 2001) });
        var longest = await CreateAsync(organization.Id, new { name = "Ровно две тысячи", key = "TWO", description = new string('я', 2000) });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("organizations.validation_failed", answer.ErrorCode);
        var errors = answer.Body.GetProperty("errors");
        Assert.Equal(["description", "name"], FieldNames(errors));
        var reason = Assert.Single(errors.GetProperty("description").EnumerateArray());
        Assert.Equal("organizations.description_length", reason.GetProperty("code").GetString());
        Assert.Equal("Описание должно содержать не более 2000 символов", reason.GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.Created, longest.Status);
        Assert.Equal((1, 0, 0), await CountAsync(organization.Id));
    }

    // feat-028 US-2 · "teams in the same operation" (BR-OL-4, BR-OL-7): each chosen team is linked, recording the caller and the time;
    // one team is the project's default by itself, two name none; the teams' own projects keep their links.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Create_WithTeamIds_LinksThem_RecordsCallerAndTime_DefaultByInferenceOnlyForOne(int count)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var chosen = new List<Team> { structure.Platform };
        if (count == 2)
        {
            chosen.Add(await OrganizationsTestData.AddUnlinkedTeamAsync(factory, structure.OrganizationId, "Ничья"));
        }

        var started = DateTimeOffset.UtcNow;
        var answer = await CreateAsync(structure.OrganizationId, new { name = "Новый проект", key = "NEW", teamIds = chosen.Select(team => team.Id.ToString()) });
        var answered = DateTimeOffset.UtcNow;

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        var projectId = answer.Body.GetProperty("id").GetGuid();
        var links = await TeamTestData.LinksOfProjectAsync(factory, projectId);
        Assert.Equal(chosen.Select(team => team.Id).Order(), links.Select(link => link.TeamId));
        Assert.All(links, link =>
        {
            Assert.Equal(OrganizationsTestData.Creator, link.LinkedBy);
            Assert.InRange(link.LinkedAt, started, answered);
        });
        Assert.Null(await StoredDefaultSettingAsync(projectId));

        using var scope = factory.Services.CreateScope();
        var project = await scope.ServiceProvider.GetRequiredService<IOrganizationStructure>()
            .FindProjectAsync(structure.OrganizationId, projectId, CancellationToken.None);
        Assert.Equal(count == 1 ? structure.Platform.Id : null, project!.DefaultTeamId);
        Assert.Contains(structure.ProjectId, (await TeamTestData.LinksOfTeamAsync(factory, structure.Platform.Id)).Select(link => link.ProjectId));
        Assert.Equal((2, count + 1, 0), await CountAsync(structure.OrganizationId));
    }

    // feat-028 US-2 · "a team that is not an active team of this organization" (§ 2.4): malformed, unknown, of another organization or
    // deleted — alike 404 team.not_found, all or nothing: no project, no link, and the name and the key stay free.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deleted")]
    public async Task Create_TeamIdNotAnActiveTeamOfTheOrganization_Returns404TeamNotFound_CreatesNothing(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var gone = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Ядро");
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, gone.Id);
        var bad = how switch
        {
            "malformed" => "not-a-guid",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another organization" => other.Platform.Id.ToString(),
            _ => gone.Id.ToString(),
        };

        var answer = await CreateAsync(structure.OrganizationId, new { name = "Новый проект", key = "NEW", teamIds = new[] { structure.Platform.Id.ToString(), bad } });
        var retry = await CreateAsync(structure.OrganizationId, new { name = "Новый проект", key = "NEW" });

        Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (answer.Status, answer.ErrorCode));
        Assert.Equal(HttpStatusCode.Created, retry.Status);
        Assert.Empty(await TeamTestData.LinksOfProjectAsync(factory, retry.Body.GetProperty("id").GetGuid()));
        Assert.Equal(["PRM", "NEW"], (await ListActiveProjectsAsync(structure.OrganizationId)).Select(project => project.Key).OrderDescending());
    }

    // US-3 · "A project is created ready to use": it keeps who created it and when (BR-M1-21). The creator is the token's
    // subject, and the time falls between the request's start and its answer.
    [Fact]
    public async Task Create_RecordsCreator()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await MembershipTestData.NewMemberAsync(factory, organization.Id, Mustask.Domain.Identity.OrganizationRole.Admin);
        var subject = admin.Id;

        var started = DateTimeOffset.UtcNow;
        var answer = await CreateAsync(organization.Id, new { name = "Пример проекта", key = "PRM" }, admin.Token);
        var answered = DateTimeOffset.UtcNow;

        Assert.Equal(HttpStatusCode.Created, answer.Status);

        using var scope = factory.Services.CreateScope();
        var project = await scope.ServiceProvider.GetRequiredService<IOrganizationStructure>()
            .FindProjectAsync(organization.Id, answer.Body.GetProperty("id").GetGuid(), CancellationToken.None);

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
        var project = await structure.FindProjectAsync(organization.Id, created.Body.GetProperty("id").GetGuid(), CancellationToken.None);
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
        Assert.Equal((1, 1, 0), await CountAsync(organization.Id));
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
        Assert.Equal((1, 1, 0), await CountAsync(organization.Id));
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
        Assert.Equal((2, 0, 0), await CountAsync(beta.Id));
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
        Assert.Equal((1, 1, 0), await CountAsync(organization.Id));
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
        Assert.Equal((1, 1, 0), await CountAsync(alpha.Id));
        Assert.Equal((1, 0, 0), await CountAsync(beta.Id));
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
    // the pre-check's code and the key as its parameter (plan-backend § 6.3, § 8.3). Exactly one project, with its root
    // exists (no team: BR-OL-2), and the loser leaves no orphan area.
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
        Assert.Equal((1, 0, 0), await CountAsync(organization.Id));
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
        Assert.Equal((1, 0, 0), await CountAsync(organization.Id));
    }

    // feat-028 BR-OL-2 · "Creating a project creates no team membership — the creator's included": the administrator who creates a
    // project joins no team and no team is created. Retires Create_TheCreatingAdministrator_IsTheOnlyMemberOfTheProjectsTeam (mod-016
    // BR-M1-25: the creator joined the project's first team, a team that no longer comes with the project; doc-012 P-1).
    [Fact]
    public async Task Create_TheCreatingAdministrator_JoinsNoTeam_NoTeamIsCreated()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var admin = await MembershipTestData.NewNamedMemberAsync(factory, structure.OrganizationId, Mustask.Domain.Identity.OrganizationRole.Admin, "Анна", "Администратор");
        var teamsBefore = (await TeamTestData.StoredTeamsAsync(factory, structure.OrganizationId)).Count;

        var answer = await CreateAsync(structure.OrganizationId, new { name = "Новый проект", key = "NEW" }, admin.Token);

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Empty(await MembershipTestData.TeamMembershipsOfAsync(factory, admin.Id));
        Assert.Equal(teamsBefore, (await TeamTestData.StoredTeamsAsync(factory, structure.OrganizationId)).Count);
        Assert.Empty(await TeamTestData.LinksOfProjectAsync(factory, answer.Body.GetProperty("id").GetGuid()));
    }

    // feat-028 BR-OL-2 · "A creator the projection does not know" (mod-016 BR-M1-32, mod-014 BR-UP-17): the administrator whom the user
    // directory has never received still creates the project — nothing about the creation depends on their membership of a team any
    // more. Retires Create_ACreatorTheProjectionDoesNotKnow_StillJoinsTheProjectsTeam_ShownUnknown (doc-012 P-1).
    [Fact]
    public async Task Create_ACreatorTheProjectionDoesNotKnow_StillCreates_ProjectHasNoTeam()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await MembershipTestData.NewMemberAsync(factory, organization.Id, Mustask.Domain.Identity.OrganizationRole.Admin);
        Assert.Null(await UserDirectory.UserDirectoryTestData.FindAsync(factory, admin.Id));

        var answer = await CreateAsync(organization.Id, new { name = "Проект неизвестного", key = "UNK" }, admin.Token);

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Empty(await MembershipTestData.TeamMembershipsOfAsync(factory, admin.Id));
        Assert.Empty(await TeamTestData.LinksOfProjectAsync(factory, answer.Body.GetProperty("id").GetGuid()));
    }

    // mod-016 US-3 · "A creator the projection does not know yet still gets the creation memberships" (BR-M1-25, mod-014 BR-UP-17),
    // end to end: a signed-in user holding the create right whom the projection has never received creates the organization and then
    // the project in it. Both succeed; they hold the admin membership (shown «Неизвестный пользователь») and the project has no team.
    [Fact]
    public async Task Create_AnOrganizationThenAProject_ByAUserTheProjectionDoesNotKnow_BothSucceed_TheyAreShownUnknown()
    {
        var subject = OrganizationsTokens.NewSubject();
        var token = OrganizationsTokens.WithCreateRight(subject);
        Assert.Null(await UserDirectory.UserDirectoryTestData.FindAsync(factory, subject));

        var organization = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, "/api/organizations", new { name = OrganizationsTestData.UniqueName("Пример организации") }, token);
        Assert.Equal(HttpStatusCode.Created, organization.Status);
        var organizationId = organization.Body.GetProperty("id").GetGuid();

        var project = await CreateAsync(organizationId, new { name = "Пример проекта", key = "PRM" }, token);
        Assert.Equal(HttpStatusCode.Created, project.Status);

        var members = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{OrganizationsApi.Organization(organizationId)}/members", token: token);

        var member = Assert.Single(members.Body.EnumerateArray());
        Assert.Equal((subject, "admin", "unknown"), (
            member.GetProperty("user").GetProperty("id").GetString(),
            member.GetProperty("role").GetString(),
            member.GetProperty("user").GetProperty("state").GetString()));
        Assert.Equal("unknown", member.GetProperty("addedBy").GetProperty("state").GetString());
        Assert.Empty(await MembershipTestData.TeamMembershipsOfAsync(factory, subject));
        Assert.Empty(await TeamTestData.LinksOfProjectAsync(factory, project.Body.GetProperty("id").GetGuid()));
    }

    // mod-016 BR-M1-10 · team members are a subset of the organization's members, and since pass B only an administrator reaches
    // this route: a signed-in user who is not a member gets the organization's not-found, creates nothing and joins no team; a
    // member and a viewer get 403 and create nothing either.
    [Fact]
    public async Task Create_ByANonMember_Is404_AMemberOrViewerIs403_NothingIsCreated_NoTeamIsJoined()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var stranger = OrganizationsTokens.NewSubject();
        var member = await MembershipTestData.NewMemberAsync(factory, organization.Id, Mustask.Domain.Identity.OrganizationRole.Member);
        var viewer = await MembershipTestData.NewMemberAsync(factory, organization.Id, Mustask.Domain.Identity.OrganizationRole.Viewer);

        var nonMember = await CreateAsync(organization.Id, new { name = "Проект чужого", key = "STR" }, OrganizationsTokens.SignedIn(stranger));
        var asMember = await CreateAsync(organization.Id, new { name = "Проект участника", key = "MEM" }, member.Token);
        var asViewer = await CreateAsync(organization.Id, new { name = "Проект читателя", key = "VWR" }, viewer.Token);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (nonMember.Status, nonMember.ErrorCode));
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (asMember.Status, asMember.ErrorCode));
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (asViewer.Status, asViewer.ErrorCode));
        Assert.Empty(await MembershipTestData.TeamMembershipsOfAsync(factory, stranger));

        var projects = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{OrganizationsApi.Organization(organization.Id)}/projects", token: OrganizationsTokens.SignedIn());
        Assert.DoesNotContain(
            projects.Body.EnumerateArray().Select(project => project.GetProperty("key").GetString()),
            key => key is "STR" or "MEM" or "VWR");
    }

    // Retired by feat-028 (doc-012 P-1): Create_TheCreatorsMembershipEndsMidRequest_RollsBack_Returns404OrganizationNotFound stated
    // mod-016 BR-M1-25 / BR-M1-10 for the creation — the creator's team membership, written with the project's first team, needed the
    // creator's organization membership to hold, and the creation rolled back when it ended mid-request. A project has no team and
    // writes no team membership now, so nothing in the creation reads the creator's membership but the access check, which a
    // removal that commits first answers with 404 on the next request.

    /// <summary>The stored default-team setting of the project: what the administrator named, not the resolved default.</summary>
    private async Task<Guid?> StoredDefaultSettingAsync(Guid projectId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => project.DefaultTeamId)
            .SingleAsync();
    }

    private async Task<IReadOnlyList<ProjectRow>> ListActiveProjectsAsync(Guid organizationId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IProjectReadModel>()
            .ListAsync(organizationId, ProjectStatus.Active, CancellationToken.None);
    }

    /// <summary>What is stored for the organization whatever its status: its projects, its teams (the organization's, not a project's) and its areas (mod-018: a project brings none).</summary>
    private async Task<(int Projects, int Teams, int Areas)> CountAsync(Guid organizationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        var projectIds = db.Projects.AsNoTracking().Where(project => project.OrganizationId == organizationId).Select(project => project.Id);

        return (
            await projectIds.CountAsync(),
            await db.Teams.AsNoTracking().CountAsync(team => team.OrganizationId == organizationId),
            await db.Areas.AsNoTracking().CountAsync(area => area.OrganizationId == organizationId));
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);

    private static IEnumerable<string> FieldNames(JsonElement errors) => PropertyNames(errors);
}
