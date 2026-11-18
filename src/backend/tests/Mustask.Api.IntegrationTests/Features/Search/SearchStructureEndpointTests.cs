using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Search;

/// <summary>
/// feat-071 · feat-065 US-2: typing finds departments, teams, projects, sub-areas and users of the organization, grouped and ranked;
/// every scenario of the story's read side. The organization administrator is the caller unless a test says otherwise (sight is
/// <see cref="SearchSightEndpointTests"/>).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SearchStructureEndpointTests(MustaskApiFactory factory)
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

    // US-2 · "Grouped results": «ал» → «Проекты» holds «Альфа» (ALP) with the department «Логистика»; empty groups are absent.
    [Fact]
    public async Task Grouped_AProjectIsFoundByNameWithItsKeyAndDepartment_AndEmptyGroupsAreAbsent()
    {
        var org = await OrgAsync();
        var alpha = await SearchTestKit.ProjectAsync(factory, org.Id, org.Logistics, "Альфа", "ALP");

        var answer = await SearchAsync(org, "ал");

        var group = Assert.Single(SearchTestKit.Groups(answer));
        Assert.Equal(("projects", false), (group.Kind, group.HasMore));
        Assert.Equal(["Альфа"], group.Names);

        var row = answer.Body.GetProperty("groups")[0].GetProperty("rows")[0];
        Assert.Equal("ALP", row.GetProperty("key").GetString());
        Assert.Equal("Логистика", row.GetProperty("departmentName").GetString());
        Assert.Equal(org.Logistics.Id, row.GetProperty("departmentId").GetGuid());
        Assert.Equal(alpha.Project.Id, row.GetProperty("id").GetGuid());
        Assert.Equal("teal", row.GetProperty("color").GetString());
        Assert.False(row.GetProperty("similar").GetBoolean());
    }

    // US-2 · "Match by project key": «chr» finds «Чарли» (CHR), case ignored.
    [Fact]
    public async Task MatchByProjectKey_IgnoresCase()
    {
        var org = await OrgAsync();
        await SearchTestKit.ProjectAsync(factory, org.Id, org.Logistics, "Чарли", "CHR");

        Assert.Equal(["Чарли"], SearchTestKit.Names(await SearchAsync(org, "chr"), "projects"));
        Assert.Equal(["Чарли"], SearchTestKit.Names(await SearchAsync(org, "CHR"), "projects"));
    }

    // US-2 · "Case and «ё»": «ЗВЕЗДЫ» finds the team «Звёзды».
    [Fact]
    public async Task CaseAndYo_AreIgnored()
    {
        var org = await OrgAsync();
        await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, "Звёзды");

        Assert.Equal(["Звёзды"], SearchTestKit.Names(await SearchAsync(org, "ЗВЕЗДЫ"), "teams"));
        Assert.Equal(["Звёзды"], SearchTestKit.Names(await SearchAsync(org, "звёзды"), "teams"));
    }

    // US-2 · "Literal characters": «%», «_» and «\» are not wildcards; only a name that contains them matches.
    [Fact]
    public async Task LiteralCharacters_FindOnlyThemselves()
    {
        var org = await OrgAsync();
        await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, "Скидка 50%");
        await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, "Скидка 500");
        await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, "a_b");
        await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, "axb");
        await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, "путь\\файл");
        await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, "путь файл");

        Assert.Equal(["Скидка 50%"], SearchTestKit.Names(await SearchAsync(org, "0%"), "teams"));
        Assert.Equal(["a_b"], SearchTestKit.Names(await SearchAsync(org, "a_b"), "teams"));
        Assert.Equal(["путь\\файл"], SearchTestKit.Names(await SearchAsync(org, "ь\\ф"), "teams"));
        Assert.Empty(SearchTestKit.Groups(await SearchAsync(org, "%%")));
    }

    // US-2 · "Users by name and email": «орл» finds «Макс Орлов» with his email; «max@exa» finds the same row.
    [Fact]
    public async Task Users_AreFoundByNameAndByEmail_WithTheirEmail()
    {
        var org = await OrgAsync();
        var max = await SearchTestKit.MemberAsync(factory, org.Id, "Макс", "Орлов", "max@example.test");

        var byName = await SearchAsync(org, "орл");
        var byEmail = await SearchAsync(org, "max@exa");

        Assert.Contains("Макс Орлов", SearchTestKit.Names(byName, "users"));
        Assert.Contains("Макс Орлов", SearchTestKit.Names(byEmail, "users"));

        var row = byEmail.Body.GetProperty("groups").EnumerateArray().Single(group => group.GetProperty("kind").GetString() == "users")
            .GetProperty("rows").EnumerateArray().Single(candidate => candidate.GetProperty("id").GetString() == max.Id);
        Assert.Equal("max@example.test", row.GetProperty("email").GetString());
        Assert.False(row.GetProperty("inactive").GetBoolean());
    }

    // US-2 · "Former and disabled members": a person who is not a member is not found; a disabled member is found and flagged inactive.
    [Fact]
    public async Task FormerMembersAreNotFound_AndDisabledMembersAreFlagged()
    {
        var org = await OrgAsync();
        var former = await SearchTestKit.MemberAsync(factory, org.Id, "Пётр", "Орлов", "petr@example.test");
        await MembershipTestData.RemoveMemberAsync(factory, org.Id, former.Id);
        var disabled = await SearchTestKit.MemberAsync(factory, org.Id, "Ирина", "Орлова", "irina@example.test", enabled: false);

        var answer = await SearchAsync(org, "орлов");

        var users = answer.Body.GetProperty("groups").EnumerateArray().Single(group => group.GetProperty("kind").GetString() == "users")
            .GetProperty("rows").EnumerateArray().ToList();
        var row = Assert.Single(users);
        Assert.Equal("Ирина Орлова", row.GetProperty("displayName").GetString());
        Assert.Equal(disabled.Id, row.GetProperty("id").GetString());
        Assert.True(row.GetProperty("inactive").GetBoolean());
    }

    // US-2 · "Ranking within a group": «Орион» (starts with), then «Корионов» and «Мориарти» (contain) by name; an exact name first.
    [Fact]
    public async Task Ranking_ExactThenPrefixThenContains_ByName()
    {
        var org = await OrgAsync();

        foreach (var name in new[] { "Мориарти", "Корионов", "Орион", "Ори" })
        {
            await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, name);
        }

        Assert.Equal(["Ори", "Орион", "Корионов", "Мориарти"], SearchTestKit.Names(await SearchAsync(org, "ори"), "teams"));
    }

    // US-2 · "Ranking within a group": a word of the name starting with the query ranks above a mere containing.
    [Fact]
    public async Task Ranking_AWordPrefixBeatsContains()
    {
        var org = await OrgAsync();

        foreach (var name in new[] { "Аорион", "Северный орион" })
        {
            await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, name);
        }

        Assert.Equal(["Северный орион", "Аорион"], SearchTestKit.Names(await SearchAsync(org, "ори"), "teams"));
    }

    // US-2 · "Group order follows the best match": an exact department comes before a team that merely starts with the text.
    [Fact]
    public async Task GroupOrder_FollowsTheBestRow_ThenTheFixedOrder()
    {
        var org = await OrgAsync();
        var supply = await OrganizationsTestData.AddDepartmentAsync(factory, org.Id, "Снабжение");
        await OrganizationsTestData.AddTeamInAsync(factory, org.Id, supply.Id, "Снабжение-2");

        Assert.Equal(["departments", "teams"], SearchTestKit.Groups(await SearchAsync(org, "снабжение")).Select(group => group.Kind));

        // Equal tiers keep the fixed order: teams before departments.
        var other = await OrganizationsTestData.AddDepartmentAsync(factory, org.Id, "Закупки-1");
        await OrganizationsTestData.AddTeamInAsync(factory, org.Id, other.Id, "Закупки-2");

        Assert.Equal(["teams", "departments"], SearchTestKit.Groups(await SearchAsync(org, "закупки-")).Select(group => group.Kind));
    }

    // US-2 · "At most 5 rows a group and «Показать ещё»": 12 projects → 5 rows and hasMore; the expansion gives 12 and no more.
    [Fact]
    public async Task FiveRowsAGroup_AndAnExpansionOfTheOneGroup()
    {
        var org = await OrgAsync();

        for (var index = 1; index <= 12; index++)
        {
            await SearchTestKit.ProjectAsync(factory, org.Id, org.Logistics, $"Проект {index:D2}", $"PR{index:D2}");
        }

        await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, "Про-команда");

        var first = SearchTestKit.Groups(await SearchAsync(org, "про"));
        var projects = first.Single(group => group.Kind == "projects");
        Assert.Equal(5, projects.Names.Count);
        Assert.True(projects.HasMore);
        Assert.False(first.Single(group => group.Kind == "teams").HasMore);

        var expanded = SearchTestKit.Groups(await SearchAsync(org, "про", "projects"));
        var only = Assert.Single(expanded);
        Assert.Equal(12, only.Names.Count);
        Assert.False(only.HasMore);
    }

    // US-2 · "At most 5 rows a group": an expansion stops at 20 and says nothing of more.
    [Fact]
    public async Task AnExpansion_StopsAtTwentyRows()
    {
        var org = await OrgAsync();

        for (var index = 1; index <= 25; index++)
        {
            await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, $"Команда {index:D2}");
        }

        var only = Assert.Single(SearchTestKit.Groups(await SearchAsync(org, "команда", "teams")));

        Assert.Equal(20, only.Names.Count);
        Assert.False(only.HasMore);
    }

    // US-2 · "Sub-areas": «сборка» finds the sub-area with its project and department.
    [Fact]
    public async Task SubAreas_AreFoundWithTheirProjectAndDepartment()
    {
        var org = await OrgAsync();
        var alpha = await SearchTestKit.ProjectAsync(factory, org.Id, org.Logistics, "Альфа", "ALP");
        await OrganizationsTestData.AddSubAreaAsync(factory, org.Id, alpha.Root.Id, "Сборка");

        var answer = await SearchAsync(org, "сборка");

        var group = Assert.Single(SearchTestKit.Groups(answer));
        Assert.Equal("areas", group.Kind);
        Assert.Equal(["Сборка"], group.Names);
        var row = answer.Body.GetProperty("groups")[0].GetProperty("rows")[0];
        Assert.Equal("Альфа", row.GetProperty("projectName").GetString());
        Assert.Equal(alpha.Project.Id, row.GetProperty("projectId").GetGuid());
        Assert.Equal("Логистика", row.GetProperty("departmentName").GetString());
    }

    // US-2 · "Deactivated and deleted objects": a deactivated project, its sub-area and a deleted team are not found (BR-GS-6).
    [Fact]
    public async Task DeactivatedAndDeletedObjects_AreNotFound()
    {
        var org = await OrgAsync();
        var old = await SearchTestKit.ProjectAsync(factory, org.Id, org.Logistics, "Старый", "OLD");
        await OrganizationsTestData.AddSubAreaAsync(factory, org.Id, old.Root.Id, "Архив");
        var gone = await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, "Бывшая");
        var live = await SearchTestKit.ProjectAsync(factory, org.Id, org.Logistics, "Живой", "LIV");
        await OrganizationsTestData.LinkAsync(factory, live.Project.Id, gone.Id);

        await OrganizationsTestData.SetProjectStatusAsync(factory, org.Id, old.Project.Id, ProjectStatus.Deactivated);
        await OrganizationsTestData.DeleteTeamAsync(factory, live.Project.Id, gone.Id);

        foreach (var text in new[] { "старый", "архив", "бывшая", "OLD" })
        {
            Assert.Empty(SearchTestKit.Groups(await SearchAsync(org, text)));
        }
    }

    // US-2 · "Too short": one character (and an empty text) is refused with the reason and no echo of the text.
    [Theory]
    [InlineData("а")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task TooShort_Is400WithTheFieldAndReason(string? query)
    {
        var org = await OrgAsync();

        var answer = await SearchAsync(org, query);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("search.validation_failed", answer.ErrorCode);
        Assert.Equal("search.query_too_short", answer.Body.GetProperty("errors").GetProperty("query")[0].GetProperty("code").GetString());
    }

    // US-2 · "The query is trimmed and bounded": 100 characters pass, 101 are refused, with the reason and no echo of the text.
    [Fact]
    public async Task TooLong_Is400_AndTheBoundaryPasses()
    {
        var org = await OrgAsync();
        var text = new string('ъ', 101);

        var refused = await SearchAsync(org, text);

        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);
        Assert.Equal("search.query_too_long", refused.Body.GetProperty("errors").GetProperty("query")[0].GetProperty("code").GetString());
        Assert.DoesNotContain(text, refused.Body.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await SearchAsync(org, new string('ъ', 100))).Status);
    }

    // feat-071 api-contract: an unknown group to expand is 400 on field «expand».
    [Fact]
    public async Task AnUnknownGroup_Is400OnExpand()
    {
        var org = await OrgAsync();

        var answer = await SearchAsync(org, "ал", "workitems");

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("search.invalid_group", answer.Body.GetProperty("errors").GetProperty("expand")[0].GetProperty("code").GetString());
    }

    // US-2 · "No match": nothing visible matches → an empty list of groups.
    [Fact]
    public async Task NoMatch_IsAnEmptyListOfGroups()
    {
        var org = await OrgAsync();
        await SearchTestKit.ProjectAsync(factory, org.Id, org.Logistics, "Альфа", "ALP");

        var answer = await SearchAsync(org, "яблоко");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(0, answer.Body.GetProperty("groups").GetArrayLength());
    }

    // US-2 · "The query is trimmed and bounded": « ал » searches «ал».
    [Fact]
    public async Task TheQuery_IsTrimmed()
    {
        var org = await OrgAsync();
        await SearchTestKit.ProjectAsync(factory, org.Id, org.Logistics, "Альфа", "ALP");

        Assert.Equal(["Альфа"], SearchTestKit.Names(await SearchAsync(org, "  ал  "), "projects"));
    }

    // US-2 · "Typo tolerance as the last tier": «логстика» finds the team and the project, marked similar, below every other row.
    [Fact]
    public async Task TypoTolerance_FindsNearMissesAsSimilarRows_AfterEveryOtherRow()
    {
        var org = await OrgAsync();
        await SearchTestKit.ProjectAsync(factory, org.Id, org.Logistics, "Логистика-Север", "LGS");
        await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, "Логистика");
        await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, "Блогстика");

        var groups = SearchTestKit.Groups(await SearchAsync(org, "логстика"));

        Assert.Equal(["departments", "projects", "teams"], groups.Select(group => group.Kind).Order());
        Assert.All(groups.Where(group => group.Kind != "teams"), group => Assert.All(group.Similar, similar => Assert.True(similar)));

        // A containing row is never below a similar one: «Блогстика» contains the query and comes first.
        var teams = groups.Single(group => group.Kind == "teams");
        Assert.Equal("Блогстика", teams.Names[0]);
        Assert.Equal([false, true], teams.Similar);
    }

    // US-2 · "Typo tolerance needs 4 characters and a short group": 3 characters add no similar rows.
    [Fact]
    public async Task TypoTolerance_NeedsFourCharacters()
    {
        var org = await OrgAsync();
        await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, "Логистика");

        Assert.Empty(SearchTestKit.Groups(await SearchAsync(org, "лгс")));
        Assert.NotEmpty(SearchTestKit.Groups(await SearchAsync(org, "логстика")));
    }

    // US-2 · "Typo tolerance needs 4 characters and a short group": a group that already holds 5 exact-tier rows adds no similar ones.
    [Fact]
    public async Task TypoTolerance_AddsNothingToAFullGroup()
    {
        var org = await OrgAsync();

        for (var index = 1; index <= 5; index++)
        {
            await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, $"Логистик {index}");
        }

        await OrganizationsTestData.AddTeamInAsync(factory, org.Id, org.Logistics.Id, "Логистка");

        var teams = SearchTestKit.Groups(await SearchAsync(org, "логистик")).Single(group => group.Kind == "teams");

        Assert.Equal(5, teams.Names.Count);
        Assert.All(teams.Similar, similar => Assert.False(similar));
        Assert.False(teams.HasMore);
    }

    // US-2 · "Typo tolerance respects sight and users": a user «Анна Смирнова» is found for «смирнва» by a caller who may see the group.
    [Fact]
    public async Task TypoTolerance_AppliesToUsers()
    {
        var org = await OrgAsync();
        await SearchTestKit.MemberAsync(factory, org.Id, "Анна", "Смирнова", "anna@example.test");

        var users = SearchTestKit.Groups(await SearchAsync(org, "смирнва")).Single(group => group.Kind == "users");

        Assert.Equal(["Анна Смирнова"], users.Names);
        Assert.Equal([true], users.Similar);
    }
}
