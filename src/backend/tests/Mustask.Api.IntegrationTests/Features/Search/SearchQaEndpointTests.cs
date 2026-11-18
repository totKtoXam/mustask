using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Search;

/// <summary>
/// feat-071 QA additions: what the developer's suite left to the gaps of the contract — the shape of every row kind, the expansion of
/// every group, the literals in emails, one-piece multi-word text, trimming at the 100 boundary, a malformed body, and a sweep that no
/// visible row names a hidden parent (US-4, BR-GS-5).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SearchQaEndpointTests(MustaskApiFactory factory)
{
    private sealed record Org(Guid Id, MembershipTestData.TestMember Ann, Area Logistics);

    private async Task<Org> OrgAsync()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, founded: false);
        var ann = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Admin);
        var logistics = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Логистика");

        return new Org(organization.Id, ann, logistics);
    }

    private Task<Answer> SearchAsync(Org org, string? query, string? expand = null) =>
        SearchTestKit.SearchAsync(factory, org.Id, org.Ann.Token, query, expand);

    private static IEnumerable<JsonElement> RowsOf(Answer answer, string kind) =>
        answer.Body.GetProperty("groups").EnumerateArray().Where(group => group.GetProperty("kind").GetString() == kind)
            .SelectMany(group => group.GetProperty("rows").EnumerateArray());

    private static string[] Fields(JsonElement row) => row.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();

    // api-contract "Row fields": one flat DTO, the fields of a kind exactly as the table says; a field a kind lacks is omitted.
    [Fact]
    public async Task EveryRowKind_CarriesExactlyTheFieldsOfTheContract()
    {
        var org = await OrgAsync();
        var alpha = await SearchTestKit.ProjectAsync(factory, org.Id, org.Logistics, "Зюзя", "ZYZ");
        await OrganizationsTestData.AddSubAreaAsync(factory, org.Id, alpha.Root.Id, "Зюзя-сборка");
        await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, "Зюзя-команда");
        await OrganizationsTestData.AddDepartmentAsync(factory, org.Id, "Зюзя-отдел");
        await SearchTestKit.MemberAsync(factory, org.Id, "Зюзя", "Иванов", "zyzya@example.test");

        var answer = await SearchAsync(org, "зюзя");

        Assert.Equal(
            ["color", "departmentId", "departmentName", "id", "key", "name", "similar"], Fields(RowsOf(answer, "projects").Single()));
        Assert.Equal(["color", "departmentId", "departmentName", "id", "name", "similar"], Fields(RowsOf(answer, "teams").Single()));
        Assert.Equal(["color", "id", "name", "similar"], Fields(RowsOf(answer, "departments").Single()));
        Assert.Equal(["departmentName", "id", "name", "projectId", "projectName", "similar"], Fields(RowsOf(answer, "areas").Single()));
        Assert.Equal(["displayName", "email", "id", "inactive", "similar"], Fields(RowsOf(answer, "users").Single()));
    }

    // US-2 · "At most 5 rows a group and «Показать ещё»", for every group, not only projects: 7 matches → 5 and hasMore; the expansion
    // of that one group gives 7, no more, and nothing of the other groups.
    [Theory]
    [InlineData("teams")]
    [InlineData("departments")]
    [InlineData("areas")]
    [InlineData("users")]
    public async Task EveryGroup_ShowsFiveAndExpandsAlone(string kind)
    {
        var org = await OrgAsync();
        var host = await SearchTestKit.ProjectAsync(factory, org.Id, org.Logistics, "Хозяин", "HST");

        for (var index = 1; index <= 7; index++)
        {
            var name = $"Ёлка {index}";
            await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, name);
            await OrganizationsTestData.AddDepartmentAsync(factory, org.Id, name);
            await OrganizationsTestData.AddSubAreaAsync(factory, org.Id, host.Root.Id, name);
            await SearchTestKit.MemberAsync(factory, org.Id, "Ёлка", $"Номер{index}", $"yolka{index}@example.test");
        }

        var first = SearchTestKit.Groups(await SearchAsync(org, "елка")).Single(group => group.Kind == kind);
        var expanded = Assert.Single(SearchTestKit.Groups(await SearchAsync(org, "елка", kind)));

        Assert.Equal((5, true), (first.Names.Count, first.HasMore));
        Assert.Equal(kind, expanded.Kind);
        Assert.Equal((7, false), (expanded.Names.Count, expanded.HasMore));
    }

    // US-2 · "Users by name and email" with BR-GS-3 literals and the «ё» fold on the projection's side: «_» in an email is not a wildcard,
    // «петр» finds «Пётр» and «ПЁТР» finds it as well.
    [Fact]
    public async Task Users_EmailLiteralsAndYoFold()
    {
        var org = await OrgAsync();
        var petr = await SearchTestKit.MemberAsync(factory, org.Id, "Пётр", "Сидоров", "p_s@example.test");
        await SearchTestKit.MemberAsync(factory, org.Id, "Павел", "Соколов", "pxs@example.test");

        Assert.Equal(["Пётр Сидоров"], SearchTestKit.Names(await SearchAsync(org, "p_s@"), "users"));
        Assert.Contains("Пётр Сидоров", SearchTestKit.Names(await SearchAsync(org, "петр сид"), "users"));
        Assert.Contains("Пётр Сидоров", SearchTestKit.Names(await SearchAsync(org, "ПЁТР"), "users"));
        Assert.Empty(RowsOf(await SearchAsync(org, "%@"), "users"));
        Assert.NotNull(petr);
    }

    // BR-GS-7's "one piece of text" read for names too: several words are not an AND of words.
    [Fact]
    public async Task SeveralWords_AreOnePieceOfText_NotAnAndOfWords()
    {
        var org = await OrgAsync();
        await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, "Северный ветер");
        await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, "Ветер северный");

        // The reversed name may come back only as a typo-tier row (BR-GS-17), never as a match of the words.
        var group = SearchTestKit.Groups(await SearchAsync(org, "северный ве")).Single();

        Assert.Equal("Северный ветер", group.Names[0]);
        Assert.False(group.Similar[0]);
        Assert.All(group.Similar.Skip(1), similar => Assert.True(similar));
    }

    // US-2 · "The query is trimmed and bounded": the 100 counts after trimming, so 100 characters inside spaces pass and 101 do not.
    [Fact]
    public async Task TheBoundary_IsJudgedAfterTrimming()
    {
        var org = await OrgAsync();

        Assert.Equal(HttpStatusCode.OK, (await SearchAsync(org, "  " + new string('ъ', 100) + "  ")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await SearchAsync(org, "  " + new string('ъ', 101) + "  ")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await SearchAsync(org, " а ")).Status);
        Assert.Equal(HttpStatusCode.OK, (await SearchAsync(org, " аб ")).Status);
    }

    // A body that is not the contract's JSON, or a query that is not text, is a 400 and never echoes what was sent.
    [Theory]
    [InlineData("{\"query\": 5}")]
    [InlineData("{\"query\": \"жгучийсекрет\", ")]
    [InlineData("not json жгучийсекрет")]
    [InlineData("")]
    public async Task AMalformedBody_Is400_WithoutEchoingIt(string body)
    {
        var org = await OrgAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, SearchTestKit.Path(org.Id))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.WithBearer(org.Ann.Token);

        using var response = await factory.CreateClient().SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("жгучийсекрет", text, StringComparison.Ordinal);
    }

    // US-4 · "No totals" / BR-GS-5 as a sweep: whatever Olga types, no row of hers names the department or the project she may not see,
    // and every name she sees in a parent field is one she sees.
    [Fact]
    public async Task NoVisibleRow_NamesAHiddenParent()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, founded: false);
        var ann = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Admin);
        var olga = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);
        var logistics = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Логистика");
        var finance = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        var alpha = await SearchTestKit.ProjectAsync(factory, organization.Id, logistics, "Альфа", "ALP", visible: false);
        var bravo = await SearchTestKit.ProjectAsync(factory, organization.Id, finance, "Браво", "BRV", visible: false);
        var venus = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, logistics.Id, "Венера");
        await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, finance.Id, "Юпитер");
        await OrganizationsTestData.LinkAsync(factory, alpha.Project.Id, venus.Id);
        await OrganizationsTestData.AddSubAreaAsync(factory, organization.Id, alpha.Root.Id, "Сборка");
        await OrganizationsTestData.AddSubAreaAsync(factory, organization.Id, bravo.Root.Id, "Сборка Б");
        await MembershipTestData.AddTeamMemberAsync(factory, organization.Id, venus.Id, olga.Id);

        var seen = new List<string>();

        foreach (var text in new[] { "ал", "сб", "ве", "ло", "фин", "бра", "юпи", "а", "ра", "ь", "ка", "brv", "alp" })
        {
            var answer = await SearchTestKit.SearchAsync(factory, organization.Id, olga.Token, text);

            if (answer.Status != HttpStatusCode.OK)
            {
                continue;
            }

            foreach (var row in answer.Body.GetProperty("groups").EnumerateArray().SelectMany(group => group.GetProperty("rows").EnumerateArray()))
            {
                seen.AddRange(row.EnumerateObject().Where(property => property.Value.ValueKind == JsonValueKind.String)
                    .Select(property => property.Value.GetString()!));
            }
        }

        Assert.NotEmpty(seen);
        Assert.DoesNotContain(seen, value => value is "Финансы" or "Браво" or "BRV" or "Юпитер" or "Сборка Б");
        Assert.NotNull(ann);
    }
}
