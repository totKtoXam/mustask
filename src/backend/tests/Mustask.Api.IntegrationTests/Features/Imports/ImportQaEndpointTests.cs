using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure;

namespace Mustask.Api.IntegrationTests.Features.Imports;

/// <summary>
/// Backend QA additions for the import constructor (mod-024): isolation between organizations and between users, the dry run's reach
/// (the importer's projects, the 50-row preview of the selected result, no writes), the upload's refusals and the handler-decided rights.
/// Every test builds its own organization; the data is made up.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ImportQaEndpointTests(MustaskApiFactory factory)
{
    private sealed record World(WorkItemsWorld Items, string Admin, MembershipTestData.TestMember Viewer, MembershipTestData.TestMember Manager)
    {
        public Guid OrganizationId => Items.OrganizationId;
    }

    private static string Imports(Guid organizationId, string tail) => $"/api/organizations/{organizationId}/imports{tail}";

    private Task<Answer> SendAsync(HttpMethod method, string path, object? body, string? token) =>
        OrganizationsApi.SendAsync(factory, method, path, body, token);

    private async Task<World> WorldAsync(string key = "MSK")
    {
        var items = await WorkItemsTestData.CreateWorldAsync(factory, key);
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

    private async Task<Guid> CreateAsync(World world, string name, string? token = null, Guid? projectId = null)
    {
        var answer = await SendAsync(
            HttpMethod.Post, Imports(world.OrganizationId, "/templates"), Body(name, projectId ?? world.Items.ProjectId, world.Items.Types["TASK"].Id), token ?? world.Admin);
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

    private async Task<Guid> UploadedAsync(World world, string token, params string[] lines)
    {
        var uploaded = await UploadAsync(world.OrganizationId, "задачи.csv", Csv(lines), token);
        Assert.Equal(HttpStatusCode.Created, uploaded.Status);

        return uploaded.Body.GetProperty("fileId").GetGuid();
    }

    private async Task<HttpResponseMessage> ReportAsync(Guid organizationId, Guid runId, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Imports(organizationId, $"/dry-runs/{runId}/report"));
        request.WithBearer(token);

        return await factory.CreateClient().SendAsync(request);
    }

    [Fact]
    public async Task Isolation_AnotherOrganizationsRoutesNameNothingOfThisOnes()
    {
        var a = await WorldAsync("AAA");
        var b = await WorldAsync("BBB");
        var templateId = await CreateAsync(a, "Реестр А");
        var fileId = await UploadedAsync(a, a.Admin, "Заголовок", "Задача один");
        var run = await SendAsync(HttpMethod.Post, Imports(a.OrganizationId, $"/templates/{templateId}/dry-runs"), new { fileId }, a.Admin);
        var runId = run.Body.GetProperty("id").GetGuid();

        var template = await SendAsync(HttpMethod.Get, Imports(b.OrganizationId, $"/templates/{templateId}"), null, b.Admin);
        var columns = await SendAsync(HttpMethod.Get, Imports(b.OrganizationId, $"/templates/{templateId}/columns"), null, b.Admin);
        var update = await SendAsync(HttpMethod.Put, Imports(b.OrganizationId, $"/templates/{templateId}"), Body("Чужой", b.Items.ProjectId, b.Items.Types["TASK"].Id), b.Admin);
        var delete = await SendAsync(HttpMethod.Delete, Imports(b.OrganizationId, $"/templates/{templateId}"), null, b.Admin);
        var file = await SendAsync(HttpMethod.Get, Imports(b.OrganizationId, $"/files/{fileId}/columns"), null, b.Admin);
        var dryRun = await SendAsync(HttpMethod.Get, Imports(b.OrganizationId, $"/dry-runs/{runId}"), null, b.Admin);
        var report = await ReportAsync(b.OrganizationId, runId, b.Admin);
        var crossRun = await SendAsync(HttpMethod.Post, Imports(b.OrganizationId, $"/templates/{templateId}/dry-runs"), new { fileId }, b.Admin);
        var list = await SendAsync(HttpMethod.Get, Imports(b.OrganizationId, "/templates"), null, b.Admin);

        Assert.Equal("imports.template.not_found", template.ErrorCode);
        Assert.Equal("imports.template.not_found", columns.ErrorCode);
        Assert.Equal("imports.template.not_found", update.ErrorCode);
        Assert.Equal("imports.template.not_found", delete.ErrorCode);
        Assert.Equal("imports.file.not_found", file.ErrorCode);
        Assert.Equal("imports.dry_run.not_found", dryRun.ErrorCode);
        Assert.Equal(HttpStatusCode.NotFound, report.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossRun.Status);
        Assert.Equal(0, list.Body.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Privacy_AFileAndItsDryRun_BelongToTheirAuthorOnly()
    {
        var world = await WorldAsync();
        var templateId = await CreateAsync(world, "Личный файл");
        var fileId = await UploadedAsync(world, world.Admin, "Заголовок", "Секретная задача");
        var run = await SendAsync(HttpMethod.Post, Imports(world.OrganizationId, $"/templates/{templateId}/dry-runs"), new { fileId }, world.Admin);
        var runId = run.Body.GetProperty("id").GetGuid();

        var peek = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, $"/dry-runs/{runId}"), null, world.Manager.Token);
        var report = await ReportAsync(world.OrganizationId, runId, world.Manager.Token);
        var reuse = await SendAsync(HttpMethod.Post, Imports(world.OrganizationId, $"/templates/{templateId}/dry-runs"), new { fileId }, world.Manager.Token);
        var values = await SendAsync(
            HttpMethod.Post, Imports(world.OrganizationId, $"/files/{fileId}/values"),
            new { source = new { headerRow = 1 }, mapping = new { id = Guid.NewGuid(), field = "title", source = new { kind = "column", column = "Заголовок" } } },
            world.Manager.Token);

        Assert.Equal("imports.dry_run.not_found", peek.ErrorCode);
        Assert.Equal(HttpStatusCode.NotFound, report.StatusCode);
        Assert.Equal("imports.file.not_found", reuse.ErrorCode);
        Assert.Equal("imports.file.not_found", values.ErrorCode);
    }

    [Fact]
    public async Task DryRun_WritesNoWorkItem()
    {
        var world = await WorldAsync();
        var templateId = await CreateAsync(world, "Ничего не пишет");
        var fileId = await UploadedAsync(world, world.Admin, "Заголовок", "Первая", "Вторая", "Третья");
        var before = await CountItemsAsync(world);

        var run = await SendAsync(HttpMethod.Post, Imports(world.OrganizationId, $"/templates/{templateId}/dry-runs"), new { fileId }, world.Admin);

        Assert.Equal(3, run.Body.GetProperty("totals").GetProperty("created").GetInt32());
        Assert.Equal(before, await CountItemsAsync(world));
    }

    private Task<int> CountItemsAsync(World world) =>
        OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
            await services.GetRequiredService<MustaskDbContext>().WorkItems.IgnoreQueryFilters().CountAsync(i => i.ProjectId == world.Items.ProjectId, ct));

    [Fact]
    public async Task DryRun_PreviewIsTheFirst50RowsOfTheSelectedResult_WhileTotalsCountEveryRow()
    {
        var world = await WorldAsync();
        var templateId = await CreateAsync(world, "Много строк");
        var tooLong = new string('я', 300);
        var lines = new List<string> { "Заголовок" };
        lines.AddRange(Enumerable.Range(1, 60).Select(n => $"Задача {n}"));
        lines.AddRange([tooLong, tooLong, tooLong]);
        var fileId = await UploadedAsync(world, world.Admin, [.. lines]);

        var run = await SendAsync(HttpMethod.Post, Imports(world.OrganizationId, $"/templates/{templateId}/dry-runs"), new { fileId }, world.Admin);
        var runId = run.Body.GetProperty("id").GetGuid();
        var errors = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, $"/dry-runs/{runId}?result=error"), null, world.Admin);
        var created = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, $"/dry-runs/{runId}?result=created"), null, world.Admin);
        var all = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, $"/dry-runs/{runId}"), null, world.Admin);
        var bad = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, $"/dry-runs/{runId}?result=oops"), null, world.Admin);

        var totals = run.Body.GetProperty("totals");
        Assert.Equal((60, 3, 63), (totals.GetProperty("created").GetInt32(), totals.GetProperty("error").GetInt32(), totals.GetProperty("all").GetInt32()));
        Assert.Equal(3, errors.Body.GetProperty("rowsShown").GetInt32());
        Assert.All(errors.Body.GetProperty("rows").EnumerateArray(), r => Assert.Equal("error", r.GetProperty("result").GetString()));
        Assert.Equal(50, created.Body.GetProperty("rowsShown").GetInt32());
        Assert.Equal(2, created.Body.GetProperty("rows")[0].GetProperty("row").GetInt32());
        Assert.Equal(50, all.Body.GetProperty("rowsShown").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, bad.Status);
    }

    [Fact]
    public async Task DryRun_ByAProjectManager_RefusesARowIntoAProjectTheyDoNotManage()
    {
        var world = await WorldAsync("MGR");
        var other = await WorkItemsTestData.AddProjectAsync(factory, world.Items, "OTR", "Чужой проект");
        var own = await CreateAsync(world, "В свой проект", world.Manager.Token);
        var foreign = await CreateAsync(world, "В чужой проект", world.Manager.Token, other.ProjectId);
        var fileId = await UploadedAsync(world, world.Manager.Token, "Заголовок", "Задача");

        var ownRun = await SendAsync(HttpMethod.Post, Imports(world.OrganizationId, $"/templates/{own}/dry-runs"), new { fileId }, world.Manager.Token);
        var foreignRun = await SendAsync(HttpMethod.Post, Imports(world.OrganizationId, $"/templates/{foreign}/dry-runs"), new { fileId }, world.Manager.Token);
        var adminFile = await UploadedAsync(world, world.Admin, "Заголовок", "Задача");
        var adminRun = await SendAsync(HttpMethod.Post, Imports(world.OrganizationId, $"/templates/{foreign}/dry-runs"), new { fileId = adminFile }, world.Admin);

        Assert.Equal(1, ownRun.Body.GetProperty("totals").GetProperty("created").GetInt32());
        Assert.Equal(1, foreignRun.Body.GetProperty("totals").GetProperty("error").GetInt32());
        Assert.False(foreignRun.Body.GetProperty("importable").GetBoolean());
        Assert.Equal("imports.row.project_forbidden", foreignRun.Body.GetProperty("rows")[0].GetProperty("reasons")[0].GetProperty("code").GetString());
        Assert.Equal(1, adminRun.Body.GetProperty("totals").GetProperty("created").GetInt32());
    }

    [Fact]
    public async Task DryRun_WithoutAFileId_IsBadRequest_AndAnUnknownFileIsNotFound()
    {
        var world = await WorldAsync();
        var templateId = await CreateAsync(world, "Без файла");
        var path = Imports(world.OrganizationId, $"/templates/{templateId}/dry-runs");

        var none = await SendAsync(HttpMethod.Post, path, new { }, world.Admin);
        var unknown = await SendAsync(HttpMethod.Post, path, new { fileId = Guid.NewGuid() }, world.Admin);

        Assert.Equal(HttpStatusCode.BadRequest, none.Status);
        Assert.Equal("imports.file.not_found", unknown.ErrorCode);
    }

    [Fact]
    public async Task Upload_RefusesOversizeAndUnsupportedFilesWhole()
    {
        var world = await WorldAsync();
        var overByOne = new byte[(10 * 1024 * 1024) + 1];
        Array.Fill(overByOne, (byte)'a');
        var huge = new byte[12 * 1024 * 1024];
        Array.Fill(huge, (byte)'a');

        var one = await UploadAsync(world.OrganizationId, "много.csv", overByOne, world.Admin);
        var big = await UploadAsync(world.OrganizationId, "огромно.csv", huge, world.Admin);
        var xls = await UploadAsync(world.OrganizationId, "реестр.xls", Csv("Заголовок", "А"), world.Admin);
        var xlsm = await UploadAsync(world.OrganizationId, "реестр.xlsm", Csv("Заголовок", "А"), world.Admin);

        Assert.Equal(HttpStatusCode.BadRequest, one.Status);
        Assert.Equal("imports.file.too_large", one.Body.GetProperty("errors").GetProperty("file")[0].GetProperty("code").GetString());
        Assert.Equal("imports.file.too_large", big.Body.GetProperty("errors").GetProperty("file")[0].GetProperty("code").GetString());
        Assert.Equal("imports.file.unsupported_format", xls.Body.GetProperty("errors").GetProperty("file")[0].GetProperty("code").GetString());
        Assert.Equal("imports.file.unsupported_format", xlsm.Body.GetProperty("errors").GetProperty("file")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Upload_With5001DataRows_IsRefusedWholeAndNothingIsStored()
    {
        var world = await WorldAsync();
        var lines = new List<string> { "Заголовок" };
        lines.AddRange(Enumerable.Range(1, 5001).Select(n => $"Задача {n}"));

        var answer = await UploadAsync(world.OrganizationId, "много-строк.csv", Csv([.. lines]), world.Admin);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("imports.file.too_many_rows", answer.Body.GetProperty("errors").GetProperty("file")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Rights_DecidedInTheHandler_AnswerForbiddenBeforeNotFound_ForAMemberWhoMayNotWrite()
    {
        var world = await WorldAsync();
        var unknown = Guid.NewGuid();
        var viewerToken = world.Viewer.Token;

        var get = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, $"/templates/{unknown}"), null, viewerToken);
        var put = await SendAsync(HttpMethod.Put, Imports(world.OrganizationId, $"/templates/{unknown}"), Body("Х", world.Items.ProjectId, world.Items.Types["TASK"].Id), viewerToken);
        var delete = await SendAsync(HttpMethod.Delete, Imports(world.OrganizationId, $"/templates/{unknown}"), null, viewerToken);
        var copy = await SendAsync(HttpMethod.Post, Imports(world.OrganizationId, $"/templates/{unknown}/copy"), null, viewerToken);
        var run = await SendAsync(HttpMethod.Post, Imports(world.OrganizationId, $"/templates/{unknown}/dry-runs"), new { fileId = unknown }, viewerToken);
        var file = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, $"/files/{unknown}/columns"), null, viewerToken);
        var dryRun = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, $"/dry-runs/{unknown}"), null, viewerToken);
        var adminGet = await SendAsync(HttpMethod.Get, Imports(world.OrganizationId, $"/templates/{unknown}"), null, world.Admin);

        foreach (var answer in new[] { get, put, delete, copy, run, file, dryRun })
        {
            Assert.Equal(HttpStatusCode.Forbidden, answer.Status);
            Assert.Equal("imports.access_forbidden", answer.ErrorCode);
        }

        Assert.Equal(HttpStatusCode.NotFound, adminGet.Status);
    }

    [Fact]
    public async Task Template_NameBoundaries_AndNumberedCopies()
    {
        var world = await WorldAsync();
        var path = Imports(world.OrganizationId, "/templates");
        var type = world.Items.Types["TASK"].Id;

        var hundred = await SendAsync(HttpMethod.Post, path, Body("  " + new string('ж', 100) + "  ", world.Items.ProjectId, type), world.Admin);
        var tooLong = await SendAsync(HttpMethod.Post, path, Body(new string('ж', 101), world.Items.ProjectId, type), world.Admin);
        var blank = await SendAsync(HttpMethod.Post, path, Body("   ", world.Items.ProjectId, type), world.Admin);
        var id = await CreateAsync(world, "Исходный");
        var first = await SendAsync(HttpMethod.Post, Imports(world.OrganizationId, $"/templates/{id}/copy"), null, world.Admin);
        var second = await SendAsync(HttpMethod.Post, Imports(world.OrganizationId, $"/templates/{id}/copy"), null, world.Admin);

        Assert.Equal(HttpStatusCode.Created, hundred.Status);
        Assert.Equal(100, hundred.Body.GetProperty("name").GetString()!.Length);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.Status);
        Assert.Equal(HttpStatusCode.BadRequest, blank.Status);
        Assert.Equal("Копия Исходный", first.Body.GetProperty("name").GetString());
        Assert.Equal("Копия Исходный 2", second.Body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Template_ARecodingTargetThatIsNotInThisOrganization_IsRefusedOnSave()
    {
        var world = await WorldAsync();
        var foreignProject = (await WorkItemsTestData.CreateWorldAsync(factory, "FRN")).ProjectId;

        var answer = await SendAsync(
            HttpMethod.Post, Imports(world.OrganizationId, "/templates"), Body("Чужой проект", foreignProject, world.Items.Types["TASK"].Id), world.Admin);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
    }
}
