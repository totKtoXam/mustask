using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Imports;

/// <summary>
/// The import constructor's routes through the real host and a real PostgreSQL (mod-024 api-contract): the rights and the served flags,
/// the template life, the uploaded file and the dry run. Every test builds its own organization; the data is made up.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ImportEndpointTests(MustaskApiFactory factory)
{
    private sealed record World(WorkItemsWorld Items, string Admin, MembershipTestData.TestMember Viewer, MembershipTestData.TestMember Manager)
    {
        public Guid OrganizationId => Items.OrganizationId;
    }

    private static string Imports(Guid organizationId, string tail) => $"/api/organizations/{organizationId}/imports{tail}";

    private Task<Answer> SendAsync(HttpMethod method, string path, object? body, string? token) =>
        OrganizationsApi.SendAsync(factory, method, path, body, token);

    private async Task<World> WorldAsync()
    {
        var items = await WorkItemsTestData.CreateWorldAsync(factory);
        var viewer = await MembershipTestData.NewMemberAsync(factory, items.OrganizationId, OrganizationRole.Viewer);
        var manager = await MembershipTestData.NewMemberAsync(factory, items.OrganizationId, OrganizationRole.Viewer);
        await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            await services.GetRequiredService<IGrantRepository>().AddAsync(
                ResourceScope.Project, items.OrganizationId, items.ProjectId, manager.Id, GrantRole.Manager, "seed", OrganizationsTestData.Now, ct);

            return true;
        });

        return new World(items, OrganizationsTokens.SignedIn(), viewer, manager);
    }

    private static object Body(string name, Guid projectId, Guid typeId) => new
    {
        name,
        settings = new
        {
            source = new { format = "csv", headerRow = 1, firstDataRow = 2, encoding = "utf-8", delimiter = ";", headers = new[] { "Заголовок" } },
            mappings = new object[]
            {
                new { id = Guid.NewGuid(), field = "project", source = new { kind = "constant", value = projectId.ToString() } },
                new { id = Guid.NewGuid(), field = "type", source = new { kind = "constant", value = typeId.ToString() } },
                new { id = Guid.NewGuid(), field = "title", source = new { kind = "column", column = "Заголовок" } },
            },
        },
    };

    private object Body(World world, string name) =>
        Body(name, world.Items.ProjectId, world.Items.Types["TASK"].Id);

    private async Task<Guid> CreateAsync(World world, string name, string? token = null)
    {
        var answer = await SendAsync(HttpMethod.Post, Imports(world.OrganizationId, "/templates"), Body(world, name), token ?? world.Admin);
        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return answer.Body.GetProperty("id").GetGuid();
    }

    private async Task<Answer> UploadAsync(Guid organizationId, string fileName, byte[] content, string token)
    {
        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(part, "file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, Imports(organizationId, "/files")) { Content = form };
        request.WithBearer(token);
        using var response = await factory.CreateClient().SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();

        return new Answer(response.StatusCode, response.Headers.Location, response.Content.Headers.ContentType?.MediaType,
            string.IsNullOrEmpty(text) ? default : JsonDocument.Parse(text).RootElement.Clone());
    }

    private static byte[] Csv(params string[] lines) => Encoding.UTF8.GetBytes(string.Join("\n", lines));

    [Fact]
    public async Task Catalogue_IsServedToAMember_AndHiddenFromAStranger()
    {
        var world = await WorldAsync();
        var path = Imports(world.OrganizationId, "/catalogue");

        var member = await SendAsync(HttpMethod.Get, path, null, world.Viewer.Token);
        var stranger = await SendAsync(HttpMethod.Get, path, null, OrganizationsTokens.Stranger());
        var anonymous = await SendAsync(HttpMethod.Get, path, null, null);

        Assert.Equal(HttpStatusCode.OK, member.Status);
        Assert.Contains(member.Body.GetProperty("fields").EnumerateArray(), f => f.GetProperty("id").GetString() == "title");
        Assert.Equal(HttpStatusCode.NotFound, stranger.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
    }

    [Fact]
    public async Task Template_LivesItsLife_CreateReadSaveCopyDelete()
    {
        var world = await WorldAsync();
        var id = await CreateAsync(world, "Реестр задач");
        var item = Imports(world.OrganizationId, $"/templates/{id}");

        var read = await SendAsync(HttpMethod.Get, item, null, world.Admin);
        var saved = await SendAsync(HttpMethod.Put, item, Body(world, "Реестр задач 2"), world.Admin);
        var copy = await SendAsync(HttpMethod.Post, item + "/copy", null, world.Admin);
        var removed = await SendAsync(HttpMethod.Delete, item, null, world.Admin);
        var gone = await SendAsync(HttpMethod.Get, item, null, world.Admin);

        Assert.Equal(1, read.Body.GetProperty("version").GetInt32());
        Assert.True(read.Body.GetProperty("canEdit").GetBoolean());
        Assert.Equal(2, saved.Body.GetProperty("version").GetInt32());
        Assert.Equal(HttpStatusCode.Created, copy.Status);
        Assert.Equal("Копия Реестр задач 2", copy.Body.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.NoContent, removed.Status);
        Assert.Equal("imports.template.not_found", gone.ErrorCode);
    }

    [Fact]
    public async Task Template_RefusesAnInvalidBody_AndATakenName()
    {
        var world = await WorldAsync();
        await CreateAsync(world, "Занятое имя");
        var path = Imports(world.OrganizationId, "/templates");

        var empty = await SendAsync(HttpMethod.Post, path, new { name = "", settings = new { } }, world.Admin);
        var taken = await SendAsync(HttpMethod.Post, path, Body(world, "занятое ИМЯ"), world.Admin);

        Assert.Equal(HttpStatusCode.BadRequest, empty.Status);
        Assert.Equal("imports.validation_failed", empty.ErrorCode);
        Assert.Equal(HttpStatusCode.Conflict, taken.Status);
        Assert.Equal("imports.template.name_taken", taken.ErrorCode);
    }

    [Fact]
    public async Task Rights_AViewerReadsTheList_ButMayNotWrite()
    {
        var world = await WorldAsync();
        var id = await CreateAsync(world, "Только чтение");

        var list = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, "/templates"), null, world.Viewer.Token);
        var columns = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, $"/templates/{id}/columns"), null, world.Viewer.Token);
        var create = await SendAsync(HttpMethod.Post, Imports(world.OrganizationId, "/templates"), Body(world, "Чужое"), world.Viewer.Token);
        var read = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, $"/templates/{id}"), null, world.Viewer.Token);

        Assert.False(list.Body.GetProperty("canCreate").GetBoolean());
        Assert.False(list.Body.GetProperty("canRun").GetBoolean());
        Assert.Equal(["Заголовок"], columns.Body.GetProperty("columns").EnumerateArray().Select(c => c.GetString()));
        Assert.Equal("imports.access_forbidden", create.ErrorCode);
        Assert.Equal(HttpStatusCode.Forbidden, read.Status);
    }

    [Fact]
    public async Task Rights_AProjectManager_ChangesOnlyTheirOwnTemplates()
    {
        var world = await WorldAsync();
        var others = await CreateAsync(world, "Шаблон администратора");
        var own = await CreateAsync(world, "Шаблон менеджера", world.Manager.Token);

        var list = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, "/templates?sort=name"), null, world.Manager.Token);
        var editOthers = await SendAsync(HttpMethod.Put, Imports(world.OrganizationId, $"/templates/{others}"), Body(world, "Взлом"), world.Manager.Token);
        var deleteOthers = await SendAsync(HttpMethod.Delete, Imports(world.OrganizationId, $"/templates/{others}"), null, world.Manager.Token);
        var copyOthers = await SendAsync(HttpMethod.Post, Imports(world.OrganizationId, $"/templates/{others}/copy"), null, world.Manager.Token);
        var editOwn = await SendAsync(HttpMethod.Put, Imports(world.OrganizationId, $"/templates/{own}"), Body(world, "Мой шаблон"), world.Manager.Token);

        Assert.True(list.Body.GetProperty("canCreate").GetBoolean());
        var rows = list.Body.GetProperty("items").EnumerateArray().ToDictionary(r => r.GetProperty("id").GetGuid());
        Assert.False(rows[others].GetProperty("canEdit").GetBoolean());
        Assert.True(rows[others].GetProperty("canCopy").GetBoolean());
        Assert.True(rows[own].GetProperty("canEdit").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, editOthers.Status);
        Assert.Equal(HttpStatusCode.Forbidden, deleteOthers.Status);
        Assert.Equal(HttpStatusCode.Created, copyOthers.Status);
        Assert.Equal(HttpStatusCode.OK, editOwn.Status);
    }

    [Fact]
    public async Task List_FiltersByNameAndAuthor_AndRefusesABadSort()
    {
        var world = await WorldAsync();
        await CreateAsync(world, "Альфа импорт");
        await CreateAsync(world, "Бета импорт", world.Manager.Token);

        var byName = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, "/templates?name=АЛЬФА"), null, world.Admin);
        var mine = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, "/templates?author=mine"), null, world.Manager.Token);
        var bad = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, "/templates?sort=oops"), null, world.Admin);

        Assert.Equal(1, byName.Body.GetProperty("total").GetInt32());
        Assert.Equal("Бета импорт", Assert.Single(mine.Body.GetProperty("items").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, bad.Status);
    }

    [Fact]
    public async Task File_IsUploadedRead_AndKeptPrivateToItsOwner()
    {
        var world = await WorldAsync();
        var csv = Csv("Заголовок;Важность", "Подготовить отчёт;1");

        var uploaded = await UploadAsync(world.OrganizationId, "реестр.csv", csv, world.Admin);
        var fileId = uploaded.Body.GetProperty("fileId").GetString();
        var read = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, $"/files/{fileId}/columns?delimiter=%3B"), null, world.Admin);
        var foreign = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, $"/files/{fileId}/columns"), null, world.Manager.Token);
        var viewer = await UploadAsync(world.OrganizationId, "реестр.csv", csv, world.Viewer.Token);

        Assert.Equal(HttpStatusCode.Created, uploaded.Status);
        Assert.Equal("title", uploaded.Body.GetProperty("read").GetProperty("columns")[0].GetProperty("proposedField").GetString());
        Assert.Equal(1, read.Body.GetProperty("dataRowCount").GetInt32());
        Assert.Equal("imports.file.not_found", foreign.ErrorCode);
        Assert.Equal(HttpStatusCode.Forbidden, viewer.Status);
    }

    [Fact]
    public async Task File_ThatCannotBeRead_IsRefusedWhole()
    {
        var world = await WorldAsync();

        var answer = await UploadAsync(world.OrganizationId, "сломан.xlsx", Encoding.UTF8.GetBytes("not a workbook"), world.Admin);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("imports.file.refused", answer.ErrorCode);
    }

    [Fact]
    public async Task Values_AndChainSample_ReadTheStoredFile()
    {
        var world = await WorldAsync();
        var uploaded = await UploadAsync(world.OrganizationId, "значения.csv", Csv("Тип", "Баг", "баг ", "Задача"), world.Admin);
        var fileId = uploaded.Body.GetProperty("fileId").GetString();
        var body = new
        {
            source = new { headerRow = 1, delimiter = ";", encoding = "utf-8" },
            mapping = new { id = Guid.NewGuid(), field = "type", source = new { kind = "column", column = "Тип" }, chain = new[] { new { converter = "trim" } } },
        };

        var values = await SendAsync(HttpMethod.Post, Imports(world.OrganizationId, $"/files/{fileId}/values"), body, world.Admin);
        var sample = await SendAsync(HttpMethod.Post, Imports(world.OrganizationId, $"/files/{fileId}/chain-sample"), body, world.Admin);

        Assert.Equal(2, values.Body.GetProperty("distinctCount").GetInt32());
        Assert.Equal(2, values.Body.GetProperty("values").EnumerateArray().First(v => v.GetProperty("value").GetString() == "Баг").GetProperty("rows").GetInt32());
        Assert.Equal(3, sample.Body.GetProperty("rows").GetArrayLength());
    }

    [Fact]
    public async Task DryRun_ChecksTheRows_ReportsThem_AndGoesStaleWhenTheTemplateIsSaved()
    {
        var world = await WorldAsync();
        var templateId = await CreateAsync(world, "Проверка файла");
        var uploaded = await UploadAsync(world.OrganizationId, "задачи.csv", Csv("Заголовок", "Первая задача", "", "=cmd|calc"), world.Admin);
        var fileId = uploaded.Body.GetProperty("fileId").GetGuid();

        var run = await SendAsync(HttpMethod.Post, Imports(world.OrganizationId, $"/templates/{templateId}/dry-runs"), new { fileId }, world.Admin);
        var runId = run.Body.GetProperty("id").GetGuid();
        var fresh = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, $"/dry-runs/{runId}?result=created"), null, world.Admin);
        var report = await ReportAsync(world, runId);
        await SendAsync(HttpMethod.Put, Imports(world.OrganizationId, $"/templates/{templateId}"), Body(world, "Проверка файла"), world.Admin);
        var stale = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, $"/dry-runs/{runId}"), null, world.Admin);

        Assert.Equal(HttpStatusCode.Created, run.Status);
        Assert.Equal(2, run.Body.GetProperty("totals").GetProperty("created").GetInt32());
        Assert.Equal(1, run.Body.GetProperty("totals").GetProperty("skipped").GetInt32());
        Assert.True(run.Body.GetProperty("importable").GetBoolean());
        Assert.Equal(2, fresh.Body.GetProperty("rowsShown").GetInt32());
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", report.Content.Headers.ContentType?.MediaType);
        Assert.True(stale.Body.GetProperty("stale").GetBoolean());
        Assert.False(stale.Body.GetProperty("importable").GetBoolean());
    }

    [Fact]
    public async Task DryRun_RefusesAFileWithoutAMappedColumn_AndAViewerCannotRun()
    {
        var world = await WorldAsync();
        var templateId = await CreateAsync(world, "Нужен заголовок");
        var uploaded = await UploadAsync(world.OrganizationId, "другое.csv", Csv("Название", "Что-то"), world.Admin);
        var fileId = uploaded.Body.GetProperty("fileId").GetGuid();
        var path = Imports(world.OrganizationId, $"/templates/{templateId}/dry-runs");

        var missing = await SendAsync(HttpMethod.Post, path, new { fileId }, world.Admin);
        var viewer = await SendAsync(HttpMethod.Post, path, new { fileId }, world.Viewer.Token);
        var unknown = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, $"/dry-runs/{Guid.NewGuid()}"), null, world.Admin);

        Assert.Equal("imports.file.refused", missing.ErrorCode);
        Assert.Equal(HttpStatusCode.Forbidden, viewer.Status);
        Assert.Equal("imports.dry_run.not_found", unknown.ErrorCode);
    }

    private async Task<HttpResponseMessage> ReportAsync(World world, Guid runId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Imports(world.OrganizationId, $"/dry-runs/{runId}/report"));
        request.WithBearer(world.Admin);

        return await factory.CreateClient().SendAsync(request);
    }
}
