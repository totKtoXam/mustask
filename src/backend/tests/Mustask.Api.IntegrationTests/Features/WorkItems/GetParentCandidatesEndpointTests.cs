using System.Net;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/parent-candidates</c>
/// (feat-001 api-contract § 3.4): the «Родитель» picker of the form and of the
/// backlog's dialog — the item's project only, the types its level allows (or
/// only the level above), never the item itself, never an item in «Удалена»; by
/// identifier or «Заголовок», most recently changed first, 20 at most.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetParentCandidatesEndpointTests(MustaskApiFactory factory)
{
    private async Task<(WorkItemsWorld World, IReadOnlyDictionary<string, Item> Items)> WorldAsync(int upTo = 8)
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);

        return (world, await LinksTestData.CreateItemsAsync(factory, world, upTo));
    }

    // US-1 · "A parent is set and saved", the picker: with nothing typed, the most recently changed items of the types
    // «Эпик», «Фича», «Пользовательская история», «Баг», MSK-US-2 itself not among them, no «Задача». A save moves an
    // item to the top.
    [Fact]
    public async Task Get_NothingTyped_TheAllowedTypes_MostRecentlyChangedFirst_NotTheItemItself()
    {
        var (world, _) = await WorldAsync();

        var offered = await LinksTestData.OfferedAsync(factory, world, "MSK-US-2");
        await WorkItemsTestData.SaveAsync(factory, world, "MSK-EPIC-1", new { description = "Изменено" });
        var afterSave = await LinksTestData.OfferedAsync(factory, world, "MSK-US-2");

        Assert.Equal(["MSK-FEAT-8", "MSK-FEAT-6", "MSK-BUG-4", "MSK-EPIC-1"], offered);
        Assert.Equal(["MSK-EPIC-1", "MSK-FEAT-8", "MSK-FEAT-6", "MSK-BUG-4"], afterSave);
    }

    // US-1 · an option carries what the picker shows: type, identifier and title, the state as secondary text.
    [Fact]
    public async Task Get_AnOption_IsAWorkItemRef()
    {
        var (world, items) = await WorldAsync();

        var answer = await LinksTestData.CandidatesAsync(factory, world, "MSK-US-2", text: "SSO");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var option = Assert.Single(answer.Body.GetProperty("items").EnumerateArray());
        Assert.Equal(items["MSK-FEAT-8"].Id, option.GetProperty("id").GetGuid());
        Assert.Equal(("MSK-FEAT-8", "Вход через SSO", "feature", "Новая", "Proposed"), (
            option.GetProperty("identifier").GetString(),
            option.GetProperty("title").GetString(),
            option.GetProperty("type").GetProperty("appearance").GetString(),
            option.GetProperty("state").GetProperty("name").GetString(),
            option.GetProperty("state").GetProperty("category").GetProperty("code").GetString()));
        Assert.False(answer.Body.GetProperty("moreMatched").GetBoolean());
    }

    // US-1 · "A parent is set and saved": «ссылк» finds «Вход по ссылке» and «Ссылка истекает раньше срока», ignoring
    // letter case; MSK-US-2 «Письмо со ссылкой» is the item itself.
    [Theory]
    [InlineData("ссылк")]
    [InlineData("СсЫлК")]
    [InlineData("  ссылк ")]
    public async Task Get_TextInTheTitle_IgnoringLetterCase(string text)
    {
        var (world, _) = await WorldAsync();

        Assert.Equal(["MSK-FEAT-6", "MSK-BUG-4"], await LinksTestData.OfferedAsync(factory, world, "MSK-US-2", text));
    }

    // US-1 · "The picker finds an item by its identifier": «feat-8» finds MSK-FEAT-8; a part of the identifier finds every
    // item it is in.
    [Fact]
    public async Task Get_TextInTheIdentifier_IgnoringLetterCase()
    {
        var (world, _) = await WorldAsync();

        Assert.Equal(["MSK-FEAT-8"], await LinksTestData.OfferedAsync(factory, world, "MSK-US-2", "feat-8"));
        Assert.Equal(["MSK-FEAT-8", "MSK-FEAT-6"], await LinksTestData.OfferedAsync(factory, world, "MSK-US-2", "MSK-FEAT"));
        Assert.Empty(await LinksTestData.OfferedAsync(factory, world, "MSK-US-2", "MSK-TASK-3"));
    }

    // US-1 · "Each type is offered the parents its level allows" (BR-H-2): MSK-EPIC-1 an «Эпик» only, MSK-FEAT-6 «Эпик» or
    // «Фича», a story or a bug the four types above «Задача» (the project's only story and only bug being the items
    // themselves, each is offered the other), MSK-TASK-3 all five; a «Задача» only to a «Задача».
    [Fact]
    public async Task Get_EachTypeIsOfferedTheTypesItsLevelAllows()
    {
        var (world, _) = await WorldAsync(upTo: 20);

        Assert.Equal(["EPIC"], await TypesOfferedAsync(world, "MSK-EPIC-1"));
        Assert.Equal(["EPIC", "FEAT"], await TypesOfferedAsync(world, "MSK-FEAT-6"));
        Assert.Equal(["EPIC", "FEAT", "US"], await TypesOfferedAsync(world, "MSK-BUG-4"));
        Assert.Equal(["BUG", "EPIC", "FEAT"], await TypesOfferedAsync(world, "MSK-US-2"));
        Assert.Equal(["BUG", "EPIC", "FEAT", "TASK", "US"], await TypesOfferedAsync(world, "MSK-TASK-3"));
        Assert.DoesNotContain("MSK-FEAT-6", await LinksTestData.OfferedAsync(factory, world, "MSK-FEAT-6"));
    }

    // US-5 · the dialog «Изменить родителя» offers only the level directly above (BR-H-14): «Фича» items for a story,
    // «Эпик» items for a feature, nothing for an epic; `false` is the form's picker.
    [Fact]
    public async Task Get_LevelAbove_OnlyTheLevelDirectlyAbove()
    {
        var (world, _) = await WorldAsync(upTo: 20);

        Assert.Equal(["FEAT"], await TypesOfferedAsync(world, "MSK-US-2", "true"));
        Assert.Equal(["EPIC"], await TypesOfferedAsync(world, "MSK-FEAT-6", "true"));
        Assert.Equal(["BUG", "US"], await TypesOfferedAsync(world, "MSK-TASK-3", "true"));
        Assert.Empty(await LinksTestData.OfferedAsync(factory, world, "MSK-EPIC-1", levelAbove: "true"));
        Assert.Equal(await LinksTestData.OfferedAsync(factory, world, "MSK-US-2"), await LinksTestData.OfferedAsync(factory, world, "MSK-US-2", levelAbove: "false"));
    }

    // api-contract § 3.4: `levelAbove` that is not a boolean fails binding with the platform's 400.
    [Fact]
    public async Task Get_LevelAboveNotABoolean_Returns400()
    {
        var (world, _) = await WorldAsync(upTo: 2);

        var answer = await LinksTestData.CandidatesAsync(factory, world, "MSK-US-2", levelAbove: "yes");

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
    }

    // US-2 · "A cycle is refused, naming its path": the item's descendants are offered — the write refuses the cycle.
    [Fact]
    public async Task Get_TheItemsDescendants_AreOffered()
    {
        var (world, items) = await WorldAsync(upTo: 12);
        await LinksTestData.LinkAsync(factory, world, items["MSK-TASK-11"], items["MSK-TASK-10"]);
        await LinksTestData.LinkAsync(factory, world, items["MSK-TASK-12"], items["MSK-TASK-11"]);

        Assert.Contains("MSK-TASK-12", await LinksTestData.OfferedAsync(factory, world, "MSK-TASK-10", "task-12"));
    }

    // US-7 · "A deleted item is not offered as a parent": MSK-EPIC-20 in «Удалена», whatever is typed, in the form's
    // picker and the dialog's.
    [Fact]
    public async Task Get_ADeletedItem_IsNeverOffered()
    {
        var (world, items) = await WorldAsync(upTo: 23);
        await LinksTestData.DeleteAsync(factory, world, items["MSK-EPIC-20"]);

        foreach (var text in new string?[] { null, "Отчётность", "epic-20", "epic" })
        {
            Assert.DoesNotContain("MSK-EPIC-20", await LinksTestData.OfferedAsync(factory, world, "MSK-FEAT-23", text));
            Assert.DoesNotContain("MSK-EPIC-20", await LinksTestData.OfferedAsync(factory, world, "MSK-FEAT-23", text, levelAbove: "true"));
        }

        Assert.Contains("MSK-EPIC-1", await LinksTestData.OfferedAsync(factory, world, "MSK-FEAT-23", "epic"));
    }

    // US-2 · "An item of another project is refused as not found": no item of «Склад», nor of another organization, is
    // ever offered on a «Мастаск» form, even under a matching title.
    [Fact]
    public async Task Get_ItemsOfAnotherProjectOrOrganization_AreNeverOffered()
    {
        var (world, _) = await WorldAsync();
        var warehouse = await WorkItemsTestData.AddProjectAsync(factory, world, "WH", "Склад");
        await WorkItemsTestData.CreateAsync(factory, warehouse, "EPIC", "Вход на склад");
        var other = await WorkItemsTestData.CreateWorldAsync(factory);
        await WorkItemsTestData.CreateAsync(factory, other, "EPIC", "Вход в чужую организацию");

        var offered = await LinksTestData.OfferedAsync(factory, world, "MSK-US-2", "вход");

        Assert.Equal(["MSK-FEAT-8", "MSK-FEAT-6", "MSK-EPIC-1"], offered);
    }

    // spec § Screens and copy: more than 20 matches answer the 20 most recently changed and say more matched.
    [Fact]
    public async Task Get_MoreThan20Match_The20MostRecent_AndMoreMatched()
    {
        var (world, _) = await WorldAsync(upTo: 26);

        var answer = await LinksTestData.CandidatesAsync(factory, world, "MSK-TASK-3");
        var narrowed = await LinksTestData.CandidatesAsync(factory, world, "MSK-TASK-3", "Заполнитель 2");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var offered = LinksTestData.Identifiers(answer.Body.GetProperty("items"));
        Assert.Equal(20, offered.Length);
        Assert.Equal("MSK-TASK-26", offered[0]);
        Assert.True(answer.Body.GetProperty("moreMatched").GetBoolean());
        Assert.False(narrowed.Body.GetProperty("moreMatched").GetBoolean());
    }

    // api-contract § 3.4: a text longer than any title matches nothing, and is not refused.
    [Fact]
    public async Task Get_ATextLongerThanATitle_MatchesNothing()
    {
        var (world, _) = await WorldAsync(upTo: 2);

        Assert.Empty(await LinksTestData.OfferedAsync(factory, world, "MSK-US-2", new string('в', 256)));
    }

    // The address resolves as the item's read: an identifier resolving to nothing, another organization's project.
    [Fact]
    public async Task Get_AddressResolvingToNothing_Returns404FromTheOutsideIn()
    {
        var (world, _) = await WorldAsync(upTo: 2);
        var other = await WorkItemsTestData.CreateWorldAsync(factory);
        await LinksTestData.CreateItemsAsync(factory, other, upTo: 2);

        LinksTestData.AssertRefused(await LinksTestData.CandidatesAsync(factory, world, "MSK-US-99"), HttpStatusCode.NotFound, "workitems.workitem.not_found");
        LinksTestData.AssertRefused(await LinksTestData.CandidatesAsync(factory, world, "MSK-BUG-2"), HttpStatusCode.NotFound, "workitems.workitem.not_found");
        LinksTestData.AssertRefused(
            await LinksTestData.CandidatesAsync(factory, world with { ProjectId = other.ProjectId }, "MSK-US-2"),
            HttpStatusCode.NotFound,
            "organizations.project.not_found");
    }

    // spec § Access: an anonymous caller gets 401.
    [Fact]
    public async Task Get_NoToken_Returns401()
    {
        var (world, _) = await WorldAsync(upTo: 2);

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, LinksTestData.Candidates(world, "MSK-US-2"));

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
    }

    /// <summary>The distinct type keys offered, in ordinal order.</summary>
    private async Task<string[]> TypesOfferedAsync(WorkItemsWorld world, string identifier, string? levelAbove = null) =>
        (await LinksTestData.OfferedAsync(factory, world, identifier, levelAbove: levelAbove))
            .Select(offered => offered.Split('-')[1])
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();
}
