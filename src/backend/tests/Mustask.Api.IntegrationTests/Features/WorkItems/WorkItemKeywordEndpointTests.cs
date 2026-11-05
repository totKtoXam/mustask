using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Identity;
using static Mustask.Api.IntegrationTests.Features.WorkItems.SearchTestData;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// The keyword of the list's «Поиск» (feat-003 US-3, BR-WS-9; api-contract § 3.3), through the endpoint and a real PostgreSQL:
/// the title as one piece of text with letter case and «ё» ignored, the identifier, the number, every character literal, and
/// the keywords no title can hold. The world is the spec's (<see cref="SearchTestData"/>): ALP stands for «Платформа» (MSK).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class WorkItemKeywordEndpointTests(MustaskApiFactory factory)
{
    private async Task<string[]> FindAsync(SearchWorld data, string keyword, params string[] states)
    {
        var answer = await ListAsync(factory, data.World, Query(states: states, search: keyword));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(answer.Body.GetProperty("data").GetArrayLength(), Count(answer));

        return Identifiers(answer);
    }

    // US-3 "A part of the title finds the item, ignoring letter case and «ё»": «оплат» finds the two payment items, newest first.
    [Fact]
    public async Task Search_APartOfTheTitle_IgnoresLetterCase_AndYo_AndIsOnePieceOfText()
    {
        var data = await CreateAsync(factory);

        Assert.Equal(Of(data, "BUG2", "US1"), await FindAsync(data, "оплат"));
        Assert.Equal(Of(data, "BUG2", "US1"), await FindAsync(data, "ОПЛАТ"));
        Assert.Equal(Of(data, "US1"), await FindAsync(data, "ОПЛАТА КАРТОЙ"));
        Assert.Equal(Of(data, "US1"), await FindAsync(data, "оплата картой"));
        Assert.Equal(Of(data, "US3"), await FindAsync(data, "отчет"));
        Assert.Equal(Of(data, "US3"), await FindAsync(data, "ОТЧЁТ"));
        Assert.Empty(await FindAsync(data, "картой оплата"));
    }

    // US-3 · «ё» equals «е» on both sides: a title typed with «е» is found by «ё» and the other way round.
    [Fact]
    public async Task Search_YoAndYe_AreOneLetter_InTheTitleAndInTheKeyword()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var withYo = await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "Новогодняя ёлка");
        var withYe = await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "Белый медведь");

        foreach (var keyword in new[] { "ёлка", "елка", "ЁЛКА", "ЕЛКА", "Ёлка" })
        {
            var answer = await ListAsync(factory, world, Query(search: keyword));
            Assert.Equal([withYo], Identifiers(answer));
        }

        foreach (var keyword in new[] { "белый", "бёлый", "МЕДВЕДЬ", "медвёдь", "БЁЛЫЙ МЕДВЁДЬ" })
        {
            Assert.Equal([withYe], Identifiers(await ListAsync(factory, world, Query(search: keyword))));
        }
    }

    // US-3 "The whole identifier finds the item, in any letter case".
    [Theory]
    [InlineData("{0}")]
    [InlineData("alp-bug-42")]
    [InlineData("Alp-Bug-42")]
    [InlineData("  ALP-BUG-42  ")]
    public async Task Search_TheWholeIdentifier_FindsTheItem_InAnyLetterCase(string keyword)
    {
        var data = await CreateAsync(factory);

        Assert.Equal(Of(data, "BUG42"), await FindAsync(data, string.Format(keyword, data.Id("BUG42"))));
    }

    // US-3 "The number alone finds the item, and a title that contains it": «42» by number and by title; «4» once, and a number
    // matches whole — BUG-42 is not listed for «4».
    [Fact]
    public async Task Search_TheNumberAlone_FindsByNumberAndByTitle_EachItemOnce_AndMatchesWhole()
    {
        var data = await CreateAsync(factory);

        Assert.Equal(Of(data, "BUG42", "TASK4"), await FindAsync(data, "42"));
        Assert.Equal(Of(data, "TASK4"), await FindAsync(data, "4"));

        // «1»: US-1 by its number and US-3 by its title «…: 100%»; each once.
        Assert.Equal(Of(data, "US3", "US1"), await FindAsync(data, "1"));
    }

    // US-3 outline "A part of an identifier, or one that is not the item's, matches no identifier": each shows nothing, and no
    // title contains it either; none is an error.
    [Theory]
    [InlineData("ALP-BUG-4", "a part of the bug's identifier")]
    [InlineData("BUG-42", "the identifier without its project key")]
    [InlineData("ALP-US-42", "number 42 is a bug")]
    [InlineData("ALP-BUG-042", "not the number as the identifier writes it")]
    [InlineData("BET-US-1", "an item of another project of the organization")]
    [InlineData("OTH-US-1", "an item of another organization")]
    [InlineData("042", "not the number as the identifier writes it")]
    [InlineData("99999999999", "a number past every item's")]
    [InlineData("2147483648", "a number past int")]
    [InlineData("ALP-BUG-99999999999", "an overflowing number in an identifier")]
    public async Task Search_APartOrAnotherOnesIdentifier_MatchesNoIdentifier_AndIsNoError(string keyword, string why)
    {
        var data = await CreateAsync(factory);

        // Another project of «Альфа» (BET) and another organization (OTH) each hold an item US-1 «Оплата картой».
        await WorkItemsTestData.CreateItemAsync(factory, data.World with { ProjectId = data.Teams.OtherProjectId, Key = "BET" }, "US", "Оплата картой");
        await WorkItemsTestData.CreateItemAsync(factory, await WorkItemsTestData.CreateWorldAsync(factory, "OTH", "Чужой"), "US", "Оплата картой");

        var answer = await ListAsync(factory, data.World, Query(search: keyword));

        Assert.True(answer.Status == HttpStatusCode.OK, why);
        Assert.Empty(Identifiers(answer));
        Assert.Equal(0, Count(answer));
    }

    // BR-WS-9 · another project's identifier "still match[es] a title that contains them", and never finds that project's item.
    [Fact]
    public async Task Search_AnotherProjectsIdentifier_FindsNothingOfThatProject_ButAMatchingTitle()
    {
        var data = await CreateAsync(factory);
        var other = await WorkItemsTestData.AddProjectAsync(factory, data.World, "WRH", "Склад");
        var warehouse = await WorkItemsTestData.CreateItemAsync(factory, other, "US", "Оплата картой");
        var titled = await WorkItemsTestData.CreateItemAsync(factory, data.World, "TASK", "Сверить WRH-US-1 с отчётом");

        // Beside ALP's own US-1, whose number is the same "1": the identifier WRH-US-1 finds a title that holds it, not the item.
        Assert.Equal([titled], Identifiers(await ListAsync(factory, data.World, Query(search: warehouse))));
        Assert.DoesNotContain(warehouse, await FindAsync(data, "1"));
        Assert.Equal([warehouse], Identifiers(await ListAsync(factory, other, Query(search: warehouse))));
    }

    // US-3 "Every character of the keyword is literal": «%» finds only itself, «_» and «\» find nothing.
    [Fact]
    public async Task Search_PercentUnderscoreAndBackslash_AreLiteral()
    {
        var data = await CreateAsync(factory);

        Assert.Equal(Of(data, "US3"), await FindAsync(data, "%"));
        Assert.Equal(Of(data, "US3"), await FindAsync(data, "100%"));
        Assert.Empty(await FindAsync(data, "_"));
        Assert.Empty(await FindAsync(data, "\\"));
        Assert.Empty(await FindAsync(data, "о%т"));
        Assert.Empty(await FindAsync(data, "о_лат"));
    }

    // US-3 · a title that does hold «_» or «\» is found by exactly them.
    [Fact]
    public async Task Search_ATitleWithUnderscoreOrBackslash_IsFoundByItAlone()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var snake = await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "file_name");
        var path = await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", @"C:\temp");
        await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "fileXname");

        Assert.Equal([snake], Identifiers(await ListAsync(factory, world, Query(search: "_"))));
        Assert.Equal([snake], Identifiers(await ListAsync(factory, world, Query(search: "e_n"))));
        Assert.Equal([path], Identifiers(await ListAsync(factory, world, Query(search: @"\"))));
        Assert.Equal([path], Identifiers(await ListAsync(factory, world, Query(search: @"c:\t"))));
    }

    // US-3 "Spaces around the keyword do not count, and a blank keyword is no filter".
    [Fact]
    public async Task Search_SpacesAroundTheKeyword_DoNotCount_AndABlankOneIsNoFilter()
    {
        var data = await CreateAsync(factory);

        Assert.Equal(await FindAsync(data, "оплат"), await FindAsync(data, "   оплат   "));
        Assert.Equal(Of(data, "BUG42", "US3", "TASK4", "BUG2", "US1"), await FindAsync(data, "   "));
        Assert.Equal(Of(data, "BUG42", "US3", "TASK4", "BUG2", "US1"), await FindAsync(data, ""));
    }

    // US-3 "The field takes at most 255 characters": 255 after trimming is searched, a longer keyword lists nothing and is no error.
    [Fact]
    public async Task Search_At255Characters_FindsTheTitle_At256AndBeyond_ListsNothing_WithNoError()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var longest = new string('я', 255);
        var item = await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", longest);

        Assert.Equal([item], Identifiers(await ListAsync(factory, world, Query(search: longest))));
        Assert.Equal([item], Identifiers(await ListAsync(factory, world, Query(search: "  " + longest + "  "))));

        foreach (var tooLong in new[] { longest + "я", new string('я', 300) })
        {
            var answer = await ListAsync(factory, world, Query(search: tooLong));

            Assert.Equal(HttpStatusCode.OK, answer.Status);
            Assert.Empty(Identifiers(answer));
            Assert.Equal(0, Count(answer));
        }
    }

    // US-3 "Only the title and the identifier are searched": «Stripe» stands in a description alone.
    [Fact]
    public async Task Search_TheDescription_IsNotSearched()
    {
        var data = await CreateAsync(factory);

        Assert.Empty(await FindAsync(data, "Stripe"));
        Assert.Empty(await FindAsync(data, "stripe"));
    }

    // R-6 · a character no title can hold — a line break, U+0000 — matches nothing and is never an error (PostgreSQL text cannot carry
    // the NUL: this is the 500 the rule exists for).
    [Theory]
    [InlineData("оплат%0Aа")]
    [InlineData("оплат%0Dа")]
    [InlineData("оплат%0Bа")]
    [InlineData("оплат%0Cа")]
    [InlineData("оплат%C2%85а")]
    [InlineData("оплат%E2%80%A8а")]
    [InlineData("оплат%E2%80%A9а")]
    [InlineData("%0Aоплат%0Aа%0A")]
    [InlineData("%00")]
    [InlineData("оплат%00а")]
    [InlineData("a%00")]
    [InlineData("%00оплат")]
    public async Task Search_ALineBreakInsideOrNul_MatchesNothing_AndIsNoError(string escaped)
    {
        var data = await CreateAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, WorkItemsTestData.List(data.World) + "?search=" + escaped, token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Empty(Identifiers(answer));
        Assert.Equal(0, Count(answer));
    }

    // BR-WS-9 · a line break is white space, so one at either end is trimmed away (like a space), and a keyword of line breaks alone is
    // blank: no filter. Only a break inside the text is a character no title can hold.
    [Theory]
    [InlineData("%0A")]
    [InlineData("%0D%0A")]
    [InlineData("%C2%85")]
    [InlineData("%E2%80%A8")]
    [InlineData("%E2%80%A9")]
    public async Task Search_ALineBreakAtTheEnds_IsTrimmed_AndAloneIsBlank(string escaped)
    {
        var data = await CreateAsync(factory);
        var path = WorkItemsTestData.List(data.World);

        var blank = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, path + "?search=" + escaped, token: OrganizationsTokens.SignedIn());
        var trimmed = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, path + "?search=" + escaped + Uri.EscapeDataString("оплат") + escaped, token: OrganizationsTokens.SignedIn());

        Assert.Equal(Of(data, "BUG42", "US3", "TASK4", "BUG2", "US1"), Identifiers(blank));
        Assert.Equal(Of(data, "BUG2", "US1"), Identifiers(trimmed));
    }

    // BR-WS-9 · "Given more than once in the address, the first value counts" — the first non-blank one (BR-WS-11).
    [Fact]
    public async Task Search_GivenTwice_TheFirstNonBlankCounts()
    {
        var data = await CreateAsync(factory);
        var path = WorkItemsTestData.List(data.World);

        var firstWins = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, path + "?search=" + Uri.EscapeDataString("оплат") + "&search=Stripe", token: OrganizationsTokens.SignedIn());
        var blankFirst = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, path + "?search=&search=" + Uri.EscapeDataString("оплат"), token: OrganizationsTokens.SignedIn());
        var onlyBlank = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, path + "?search=&search=%20", token: OrganizationsTokens.SignedIn());

        Assert.Equal(Of(data, "BUG2", "US1"), Identifiers(firstWins));
        Assert.Equal(Of(data, "BUG2", "US1"), Identifiers(blankFirst));
        Assert.Equal(5, Count(onlyBlank));
    }

    // US-3 "A deleted item is found by keyword only under «Удалена»".
    [Fact]
    public async Task Search_ADeletedItem_IsFoundOnlyWhileUdalenaIsChosen()
    {
        var data = await CreateAsync(factory);

        Assert.Empty(await FindAsync(data, "Возврат"));
        Assert.Empty(await FindAsync(data, data.Id("US5")));
        Assert.Empty(await FindAsync(data, "5"));

        Assert.Equal(Of(data, "US5"), await FindAsync(data, "Возврат", "Удалена"));
        Assert.Equal(Of(data, "US5"), await FindAsync(data, data.Id("US5"), "Удалена"));
        Assert.Equal(Of(data, "US5"), await FindAsync(data, "5", "Удалена"));
        Assert.Equal(Of(data, "US5", "BUG2"), await FindAsync(data, "оплат", "Удалена", "Разрешена"));

        var answer = await ListAsync(factory, data.World, Query(states: ["Удалена"], search: "Возврат"));
        var row = answer.Body.GetProperty("data")[0];
        Assert.Equal("Удалена", row.GetProperty("state").GetProperty("name").GetString());
    }

    // US-3 "All four filters combine, and each clear widens the list".
    [Fact]
    public async Task Search_AllFourFilters_Combine_AndEachClearWidens()
    {
        var data = await CreateAsync(factory);

        // The spec's MSK-BUG-43 «Оплата не проходит» («Новая», Mia, MARS), changed last.
        var added = await CreateItemAsync(factory, data.Teams, "BUG", "Оплата не проходит", data.Teams.Mars);
        await WorkItemsTestData.SaveAsync(factory, data.World, added.Identifier, new { assignee = new { userId = data.People.Mia } });
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_items SET changed_at = now() WHERE id = {added.Id}");

        string[] all = ["Новая", "Активна"];
        var mia = new[] { data.People.Mia };
        var mars = new[] { data.Teams.Mars.ToString() };

        async Task<string[]> Read(string[]? states, string[]? assignees, string[]? teams, string? search) =>
            Identifiers(await ListAsync(factory, data.World, Query(states, assignees, search, teams)));

        Assert.Equal([added.Identifier, data.Id("US1")], await Read(all, mia, mars, "оплат"));
        Assert.Equal([added.Identifier, data.Id("US1")], await Read(all, mia, null, "оплат"));
        Assert.Equal([added.Identifier, data.Id("US1")], await Read(null, mia, mars, "оплат"));
        Assert.Equal([added.Identifier, data.Id("BUG2"), data.Id("US1")], await Read(null, null, null, "оплат"));
        Assert.Equal([added.Identifier, data.Id("BUG42"), data.Id("US1")], await Read(all, mia, null, null));
    }

    // US-3 "A reader searches the same way".
    [Fact]
    public async Task Search_AReader_SearchesTheSameWay()
    {
        var data = await CreateAsync(factory);
        var reader = OrganizationsTokens.SignedIn(data.People.Rita);

        var answer = await ListAsync(factory, data.World, Query(search: "оплат"), reader);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(Of(data, "BUG2", "US1"), Identifiers(answer));
    }

    // BR-WS-12 · the count follows the keyword and a page past the last answers no rows and the right count.
    [Fact]
    public async Task Search_CountFollowsTheKeyword_AndAPagePastTheLastIsEmpty()
    {
        var data = await CreateAsync(factory);

        var first = await ListAsync(factory, data.World, Query(search: "оплат", page: 1));
        var past = await ListAsync(factory, data.World, Query(search: "оплат", page: 3));

        Assert.Equal(2, Count(first));
        Assert.Equal(2, Count(past));
        Assert.Empty(Identifiers(past));
    }

    // BR-WS-15 · the keyword never finds another organization's item, by identifier or by title.
    [Fact]
    public async Task Search_NeverFindsAnotherOrganizationsItem()
    {
        var data = await CreateAsync(factory);
        var other = await WorkItemsTestData.CreateWorldAsync(factory, "OTH", "Чужой");
        var foreign = await WorkItemsTestData.CreateItemAsync(factory, other, "US", "Оплата картой");

        Assert.Equal(Of(data, "US1"), await FindAsync(data, "ОПЛАТА КАРТОЙ"));
        Assert.Empty(await FindAsync(data, foreign));
        Assert.Equal([foreign], Identifiers(await ListAsync(factory, other, Query(search: "оплата"))));
    }

    // BR-WS-1 · a non-member of the organization gets the list's 404 whatever the keyword is.
    [Fact]
    public async Task Search_ANonMember_GetsTheOrganizationsNotFound()
    {
        var data = await CreateAsync(factory);

        var answer = await ListAsync(factory, data.World, Query(search: "оплат"), MembershipTestData.Outsider());

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.organization.not_found", answer.ErrorCode);
    }

    // The role's bundle holds the read code for every role: an administrator, a member and a reader all search.
    [Theory]
    [InlineData(OrganizationRole.Admin)]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Search_EveryRoleOfTheOrganization_CanSearch(OrganizationRole role)
    {
        var data = await CreateAsync(factory);
        var member = await MembershipTestData.NewMemberAsync(factory, data.World.OrganizationId, role);

        var answer = await ListAsync(factory, data.World, Query(search: "оплат"), member.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(2, Count(answer));
    }
}
