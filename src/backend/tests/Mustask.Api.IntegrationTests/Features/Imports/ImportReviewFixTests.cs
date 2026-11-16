using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;

namespace Mustask.Api.IntegrationTests.Features.Imports;

/// <summary>
/// Code review round 1 of mod-024 (R1-1, R1-2, R1-4) through the real host and a real PostgreSQL: the dry run finds existing items by the
/// keys it resolves (the identifier after its chain, the parent), the probe routes apply the template limits, and an upload that cannot be
/// stored answers a refusal or a cut name, never a 500. The data is made up.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ImportReviewFixTests(MustaskApiFactory factory)
{
    private static string Imports(Guid organizationId, string tail) => $"/api/organizations/{organizationId}/imports{tail}";

    private Task<Answer> SendAsync(HttpMethod method, string path, object? body, string? token) =>
        OrganizationsApi.SendAsync(factory, method, path, body, token);

    private static byte[] Csv(params string[] lines) => Encoding.UTF8.GetBytes(string.Join("\n", lines));

    private async Task<Answer> UploadAsync(Guid organizationId, string fileName, byte[] content, string token)
    {
        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(part, "file", fileName);

        return await PostFormAsync(organizationId, form, token);
    }

    private async Task<Answer> PostFormAsync(Guid organizationId, HttpContent content, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Imports(organizationId, "/files")) { Content = content };
        request.WithBearer(token);
        using var response = await factory.CreateClient().SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();

        return new Answer(response.StatusCode, response.Headers.Location, response.Content.Headers.ContentType?.MediaType,
            string.IsNullOrEmpty(text) ? default : JsonDocument.Parse(text).RootElement.Clone());
    }

    private static object Template(WorkItemsWorld world, string name, params object[] extra) => new
    {
        name,
        settings = new
        {
            source = new
            {
                format = "csv", headerRow = 1, firstDataRow = 2, encoding = "utf-8", delimiter = ";",
                headers = new[] { "Заголовок", "Ключ", "Родитель" },
            },
            mappings = Mappings(world, extra),
        },
    };

    private static object[] Mappings(WorkItemsWorld world, object[] extra) =>
    [
        new { id = Guid.NewGuid(), field = "project", source = new { kind = "constant", value = world.ProjectId.ToString() } },
        new { id = Guid.NewGuid(), field = "type", source = new { kind = "constant", value = world.Types["TASK"].Id.ToString() } },
        new { id = Guid.NewGuid(), field = "title", source = new { kind = "column", column = "Заголовок" } },
        .. extra,
    ];

    private async Task<JsonElement> DryRunAsync(WorkItemsWorld world, string admin, object template, params string[] csv)
    {
        var created = await SendAsync(HttpMethod.Post, Imports(world.OrganizationId, "/templates"), template, admin);
        Assert.Equal(HttpStatusCode.Created, created.Status);
        var uploaded = await UploadAsync(world.OrganizationId, "реестр.csv", Csv(csv), admin);
        Assert.Equal(HttpStatusCode.Created, uploaded.Status);

        var run = await SendAsync(
            HttpMethod.Post,
            Imports(world.OrganizationId, $"/templates/{created.Body.GetProperty("id").GetGuid()}/dry-runs"),
            new { fileId = uploaded.Body.GetProperty("fileId").GetGuid() },
            admin);
        Assert.Equal(HttpStatusCode.Created, run.Status);

        return run.Body;
    }

    private static object IdentifierColumn(params object[] chain) =>
        new { id = Guid.NewGuid(), field = "identifier", source = new { kind = "column", column = "Ключ" }, chain };

    [Fact]
    public async Task DryRun_UpdatesAnExistingItem_ByTheIdentifierColumn()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var identifier = await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "Старое название");

        var run = await DryRunAsync(world, OrganizationsTokens.SignedIn(), Template(world, "По ключу", IdentifierColumn()),
            "Заголовок;Ключ;Родитель", $"Новое название;{identifier};");

        Assert.Equal(1, run.GetProperty("totals").GetProperty("updated").GetInt32());
        Assert.Equal(0, run.GetProperty("totals").GetProperty("error").GetInt32());
    }

    [Fact]
    public async Task DryRun_FindsTheItem_ByTheIdentifierAfterItsConverterChain()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var identifier = await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "Старое название");
        var extract = new { converter = "extract", @params = new { pattern = "[A-Z0-9]+-[A-Z0-9]+-[0-9]+" } };

        var run = await DryRunAsync(world, OrganizationsTokens.SignedIn(), Template(world, "Ключ из ссылки", IdentifierColumn(extract)),
            "Заголовок;Ключ;Родитель", $"Новое название;https://tracker.example.test/browse/{identifier}?tab=1;");

        Assert.Equal(1, run.GetProperty("totals").GetProperty("updated").GetInt32());
        Assert.Equal(0, run.GetProperty("totals").GetProperty("error").GetInt32());
    }

    [Fact]
    public async Task DryRun_FindsTheParent_AnItemOfTheOrganizationAbsentFromTheIdentifierColumn()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var parent = await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "Родитель");
        var parentMapping = new { id = Guid.NewGuid(), field = "parent", source = new { kind = "column", column = "Родитель" } };

        var run = await DryRunAsync(world, OrganizationsTokens.SignedIn(), Template(world, "С родителем", parentMapping),
            "Заголовок;Ключ;Родитель", $"Дочерняя;;{parent}", "Сирота;;MSK-TASK-9999");

        var rows = run.GetProperty("rows").EnumerateArray().ToDictionary(r => r.GetProperty("row").GetInt32());
        Assert.Equal("created", rows[2].GetProperty("result").GetString());
        Assert.DoesNotContain(rows[2].GetProperty("reasons").EnumerateArray(), r => r.GetProperty("code").GetString() == "imports.row.identifier_not_found");
        Assert.Equal("error", rows[3].GetProperty("result").GetString());
        Assert.Contains(rows[3].GetProperty("reasons").EnumerateArray(), r => r.GetProperty("code").GetString() == "imports.row.identifier_not_found");
    }

    [Fact]
    public async Task Probe_RefusesAnOverLongChain_AndAnOverLargeLookup_WithTheValidationShape()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var admin = OrganizationsTokens.SignedIn();
        var uploaded = await UploadAsync(world.OrganizationId, "значения.csv", Csv("Тип", "Баг"), admin);
        var path = Imports(world.OrganizationId, $"/files/{uploaded.Body.GetProperty("fileId").GetString()}");
        object Probe(params object[] chain) => new
        {
            source = new { headerRow = 1, delimiter = ";", encoding = "utf-8" },
            mapping = new { id = Guid.NewGuid(), field = "type", source = new { kind = "column", column = "Тип" }, chain },
        };

        var longChain = Enumerable.Range(0, 11).Select(_ => (object)new { converter = "trim" }).ToArray();
        var entries = Enumerable.Range(0, 1_001).Select(i => new { from = $"v{i}", to = "x" }).ToArray();
        var bigLookup = new { converter = "lookup", @params = new { entries, policy = "empty" } };
        var badPattern = new { converter = "extract", @params = new { pattern = "(" } };

        foreach (var route in new[] { "/values", "/chain-sample" })
        {
            var tooLong = await SendAsync(HttpMethod.Post, path + route, Probe(longChain), admin);
            var tooBig = await SendAsync(HttpMethod.Post, path + route, Probe(bigLookup), admin);
            var invalid = await SendAsync(HttpMethod.Post, path + route, Probe(badPattern), admin);

            Assert.Equal(HttpStatusCode.BadRequest, tooLong.Status);
            Assert.Equal("imports.validation_failed", tooLong.ErrorCode);
            Assert.Contains("mapping.chain", tooLong.Body.GetProperty("errors").EnumerateObject().Select(p => p.Name));
            Assert.Equal(HttpStatusCode.BadRequest, tooBig.Status);
            Assert.Contains("mapping.chain", tooBig.Body.GetProperty("errors").EnumerateObject().Select(p => p.Name));
            Assert.Equal(HttpStatusCode.BadRequest, invalid.Status);
        }
    }

    [Fact]
    public async Task Upload_CutsALongFileName_KeepingTheExtension()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);

        var answer = await UploadAsync(world.OrganizationId, new string('я', 400) + ".csv", Csv("Заголовок", "Задача"), OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        var name = answer.Body.GetProperty("fileName").GetString()!;
        Assert.Equal(260, name.Length);
        Assert.EndsWith(".csv", name);
    }

    [Fact]
    public async Task Upload_AMalformedMultipartBody_IsRefusedAsUnreadable_NotA500()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        using var content = new StringContent("--nope\r\nthis is not a multipart body", Encoding.UTF8);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=\"expected-boundary\"");

        var answer = await PostFormAsync(world.OrganizationId, content, OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("imports.file.refused", answer.ErrorCode);
    }
}
