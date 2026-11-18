using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Application.Features.Search.SearchStructure;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Search;

/// <summary>
/// feat-071 · feat-065 US-4 (BR-GS-4, -5, -6): only what the caller sees is found, and nothing shows what is hidden. One test per
/// object kind holds a hidden one out of the groups and out of «more»; the answer for a hidden name is byte-identical to the one
/// for a name that exists nowhere. Olga's cast: a member of VENUS with no grant; she sees «Логистика», MARS, VENUS and the project
/// «Альфа» (ALP), and not «Браво», «Финансы», JUPITER, «Чарли».
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SearchSightEndpointTests(MustaskApiFactory factory)
{
    private sealed record World(
        Guid OrganizationId,
        MembershipTestData.TestMember Ann,
        MembershipTestData.TestMember Olga,
        Area Logistics,
        Area Finance,
        NewProject Alpha,
        NewProject Bravo,
        NewProject Charlie,
        Team Venus,
        Team Mars,
        Team Jupiter);

    private async Task<World> WorldAsync()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, founded: false);
        var ann = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Admin);
        var olga = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);
        var logistics = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Логистика");
        var finance = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        var alpha = await SearchTestKit.ProjectAsync(factory, organization.Id, logistics, "Альфа", "ALP", visible: false);
        var bravo = await SearchTestKit.ProjectAsync(factory, organization.Id, logistics, "Браво", "BRV", visible: false);
        var charlie = await SearchTestKit.ProjectAsync(factory, organization.Id, finance, "Чарли", "CHR", visible: false);
        var venus = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, logistics.Id, "VENUS");
        var mars = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, logistics.Id, "MARS");
        var jupiter = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, finance.Id, "JUPITER");
        await OrganizationsTestData.LinkAsync(factory, alpha.Project.Id, venus.Id);
        await OrganizationsTestData.LinkAsync(factory, alpha.Project.Id, mars.Id);
        await OrganizationsTestData.LinkAsync(factory, charlie.Project.Id, jupiter.Id);
        await MembershipTestData.AddTeamMemberAsync(factory, organization.Id, venus.Id, olga.Id);

        return new World(organization.Id, ann, olga, logistics, finance, alpha, bravo, charlie, venus, mars, jupiter);
    }

    private Task<Answer> AsOlgaAsync(World w, string query, string? expand = null) =>
        SearchTestKit.SearchAsync(factory, w.OrganizationId, w.Olga.Token, query, expand);

    private async Task<string> NowhereBodyAsync(World w) =>
        (await AsOlgaAsync(w, "запрос-которого-нет")).Body.GetRawText();

    // US-4 · "A hidden project is not found": by name and by key, with the same body as for a name that exists nowhere.
    [Fact]
    public async Task AHiddenProject_IsNotFoundByNameOrKey_AndTheBodyIsTheNowhereBody()
    {
        var w = await WorldAsync();
        await SearchTestKit.SearchAsync(factory, w.OrganizationId, w.Ann.Token, "браво"); // the administrator does see it
        Assert.Equal(["Браво"], SearchTestKit.Names(await SearchTestKit.SearchAsync(factory, w.OrganizationId, w.Ann.Token, "браво"), "projects"));

        var nowhere = await NowhereBodyAsync(w);

        foreach (var text in new[] { "браво", "BRV", "чарли" })
        {
            var answer = await AsOlgaAsync(w, text);

            Assert.Empty(SearchTestKit.Groups(answer));
            Assert.Equal(nowhere, answer.Body.GetRawText());
        }
    }

    // US-4 · "Hidden teams and departments": JUPITER and «Финансы» are not found; MARS is, because she sees ALP linked to it (BR-TD-13).
    [Fact]
    public async Task AHiddenTeamAndAHiddenDepartment_AreNotFound_AndALinkedTeamIs()
    {
        var w = await WorldAsync();
        var nowhere = await NowhereBodyAsync(w);

        foreach (var text in new[] { "jupiter", "финансы" })
        {
            var answer = await AsOlgaAsync(w, text);

            Assert.Empty(SearchTestKit.Groups(answer));
            Assert.Equal(nowhere, answer.Body.GetRawText());
        }

        Assert.Equal(["MARS"], SearchTestKit.Names(await AsOlgaAsync(w, "mars"), "teams"));
        Assert.Equal(["Логистика"], SearchTestKit.Names(await AsOlgaAsync(w, "логистика"), "departments"));
        Assert.Equal(["Финансы"], SearchTestKit.Names(await SearchTestKit.SearchAsync(factory, w.OrganizationId, w.Ann.Token, "финансы"), "departments"));
    }

    // US-4 · "Entry-point tests", sub-area: a sub-area of a hidden project is not found, one of a visible project is.
    [Fact]
    public async Task AHiddenSubArea_IsNotFound_AndAVisibleOneIs()
    {
        var w = await WorldAsync();
        await OrganizationsTestData.AddSubAreaAsync(factory, w.OrganizationId, w.Bravo.Root.Id, "Секретная область");
        await OrganizationsTestData.AddSubAreaAsync(factory, w.OrganizationId, w.Alpha.Root.Id, "Сборка");
        var nowhere = await NowhereBodyAsync(w);

        var hidden = await AsOlgaAsync(w, "секретная");

        Assert.Equal(nowhere, hidden.Body.GetRawText());
        Assert.Equal(["Сборка"], SearchTestKit.Names(await AsOlgaAsync(w, "сборка"), "areas"));
        Assert.Equal(["Секретная область"], SearchTestKit.Names(await SearchTestKit.SearchAsync(factory, w.OrganizationId, w.Ann.Token, "секретная"), "areas"));
    }

    // US-4 · "Entry-point tests", user: a person is found only by a caller holding the membership read; every role of today holds it,
    // so here the point is that a user of another organization is never found (the handler test covers the caller without the code).
    [Fact]
    public async Task AUserOfAnotherOrganization_IsNotFound()
    {
        var w = await WorldAsync();
        var other = await OrganizationsTestData.CreateOrganizationAsync(factory, founded: false);
        await SearchTestKit.MemberAsync(factory, other.Id, "Пётр", "Чужой", "petr@elsewhere.test");

        Assert.Empty(SearchTestKit.Groups(await AsOlgaAsync(w, "чужой")));
        Assert.Empty(SearchTestKit.Groups(await AsOlgaAsync(w, "petr@else")));
    }

    // US-4 · "More never reflects a hidden row": 7 projects contain «про», Olga sees 4 of them, the administrator all 7.
    [Fact]
    public async Task More_ComesFromVisibleRowsOnly()
    {
        var w = await WorldAsync();

        for (var index = 1; index <= 7; index++)
        {
            await SearchTestKit.ProjectAsync(factory, w.OrganizationId, w.Logistics, $"Проект {index}", $"PRJ{index}", visible: index <= 4);
        }

        var olga = SearchTestKit.Groups(await AsOlgaAsync(w, "про")).Single(group => group.Kind == "projects");
        var ann = SearchTestKit.Groups(await SearchTestKit.SearchAsync(factory, w.OrganizationId, w.Ann.Token, "про")).Single(group => group.Kind == "projects");

        Assert.Equal((4, false), (olga.Names.Count, olga.HasMore));
        Assert.Equal((5, true), (ann.Names.Count, ann.HasMore));
    }

    // US-4 · "Order does not depend on hidden rows": a hidden object that would rank first changes neither the rows nor their order.
    [Fact]
    public async Task AHiddenRow_ChangesNeitherTheRowsNorTheirOrder()
    {
        var w = await WorldAsync();
        await SearchTestKit.ProjectAsync(factory, w.OrganizationId, w.Logistics, "Мориарти", "MOR");
        await SearchTestKit.ProjectAsync(factory, w.OrganizationId, w.Logistics, "Орион", "ORN");

        var without = await AsOlgaAsync(w, "ори");

        await SearchTestKit.ProjectAsync(factory, w.OrganizationId, w.Logistics, "Ори", "ORI", visible: false);

        var with = await AsOlgaAsync(w, "ори");

        Assert.Equal(["Орион", "Мориарти"], SearchTestKit.Names(with, "projects"));
        Assert.Equal(without.Body.GetRawText(), with.Body.GetRawText());
    }

    // US-4 · "No totals anywhere": neither the response types nor the JSON carry a total, a count or a hidden marker.
    [Fact]
    public async Task NoTotalsAnywhere()
    {
        var types = new[] { typeof(SearchStructureResponse), typeof(SearchGroupResponse), typeof(SearchRowResponse) };
        var names = types.SelectMany(type => type.GetProperties().Select(property => property.Name));

        Assert.DoesNotContain(names, name => name.Contains("Total", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Count", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Hidden", StringComparison.OrdinalIgnoreCase));

        var w = await WorldAsync();
        var keys = new List<string>();
        Collect(await SearchTestKit.SearchAsync(factory, w.OrganizationId, w.Ann.Token, "а"), keys);
        Collect(await SearchTestKit.SearchAsync(factory, w.OrganizationId, w.Ann.Token, "ал"), keys);

        Assert.DoesNotContain(keys, key => key.Contains("total", StringComparison.OrdinalIgnoreCase)
            || key.Contains("count", StringComparison.OrdinalIgnoreCase)
            || key.Contains("hidden", StringComparison.OrdinalIgnoreCase));

        static void Collect(Answer answer, List<string> into)
        {
            if (answer.Status == HttpStatusCode.OK)
            {
                Walk(answer.Body, into);
            }
        }

        static void Walk(JsonElement element, List<string> into)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        into.Add(property.Name);
                        Walk(property.Value, into);
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        Walk(item, into);
                    }

                    break;
            }
        }
    }

    // US-4 · "Sight changes between requests" + feat-041 cases: a project is found through a linked team's membership, through a grant and
    // through the «Видим всей организации» flag, and a project flipped to hidden vanishes on the next request.
    [Fact]
    public async Task SightChangesApplyOnTheNextRequest_ThroughTeamGrantAndFlag()
    {
        var w = await WorldAsync();

        Assert.Equal(["Альфа"], SearchTestKit.Names(await AsOlgaAsync(w, "ALP"), "projects"));

        // Her only route to ALP ends (VENUS membership removed): not found; MARS and VENUS went with it.
        await MembershipTestData.RemoveMemberAsync(factory, w.OrganizationId, w.Olga.Id);
        await MembershipTestData.AddMemberAsync(factory, w.OrganizationId, w.Olga.Id, OrganizationRole.Member);
        Assert.Empty(SearchTestKit.Groups(await AsOlgaAsync(w, "ALP")));

        // A grant on the project brings it back.
        await OrganizationsTestData.GrantAsync(factory, ResourceScope.Project, w.OrganizationId, w.Alpha.Project.Id, w.Olga.Id);
        Assert.Equal(["Альфа"], SearchTestKit.Names(await AsOlgaAsync(w, "ALP"), "projects"));

        // The flag brings a project she has no other route to.
        Assert.Empty(SearchTestKit.Groups(await AsOlgaAsync(w, "BRV")));
        await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var projects = (Mustask.Application.Abstractions.Organizations.IProjectRepository)services.GetService(typeof(Mustask.Application.Abstractions.Organizations.IProjectRepository))!;
            var project = await projects.LockAsync(w.OrganizationId, w.Bravo.Project.Id, ct);
            project!.SetVisibleToOrganization(true);
            await projects.SaveAsync(ct);

            return true;
        });
        Assert.Equal(["Браво"], SearchTestKit.Names(await AsOlgaAsync(w, "BRV"), "projects"));
    }

    // US-2 · "Typo tolerance respects sight and users": «jupitr» does not resurrect JUPITER for Olga, and finds it for the administrator.
    [Fact]
    public async Task TheTypoTier_DoesNotResurrectAHiddenObject()
    {
        var w = await WorldAsync();
        var nowhere = await NowhereBodyAsync(w);

        var olga = await AsOlgaAsync(w, "jupitr");

        Assert.Equal(nowhere, olga.Body.GetRawText());
        Assert.Equal(["JUPITER"], SearchTestKit.Names(await SearchTestKit.SearchAsync(factory, w.OrganizationId, w.Ann.Token, "jupitr"), "teams"));
    }

    // US-4 · "Foreign organization": the objects of another organization are not found.
    [Fact]
    public async Task AnotherOrganizationsObjects_AreNotFound()
    {
        var w = await WorldAsync();
        var other = await OrganizationsTestData.CreateOrganizationAsync(factory, founded: false);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, other.Id, "Другая");
        await SearchTestKit.ProjectAsync(factory, other.Id, department, "Зет", "ZED");

        Assert.Empty(SearchTestKit.Groups(await AsOlgaAsync(w, "зет")));
        Assert.Empty(SearchTestKit.Groups(await SearchTestKit.SearchAsync(factory, w.OrganizationId, w.Ann.Token, "ZED")));
    }

    // US-4 · "Not a member": a non-member and an instance administrator without a membership get the organization's 404, the body of an
    // organization that does not exist; the same for a viewer? no — a viewer is a member and searches.
    [Fact]
    public async Task ANonMember_AndAnInstanceAdministrator_GetTheOrganizationsNotFound()
    {
        var w = await WorldAsync();

        var stranger = await SearchTestKit.SearchAsync(factory, w.OrganizationId, MembershipTestData.Outsider(), "альфа");
        var instanceAdmin = await SearchTestKit.SearchAsync(factory, w.OrganizationId, OrganizationsTokens.SystemAdmin(), "альфа");
        var missing = await SearchTestKit.SearchAsync(factory, Guid.CreateVersion7(), MembershipTestData.Outsider(), "альфа");

        Assert.All(new[] { stranger, instanceAdmin }, answer => Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (answer.Status, answer.ErrorCode)));
        Assert.Equal((missing.Status, missing.ErrorCode, missing.Detail), (stranger.Status, stranger.ErrorCode, stranger.Detail));
    }

    // US-4 · "Membership ends while the palette is open": the member who is removed gets the organization's 404 on the next read.
    [Fact]
    public async Task AMemberWhoIsRemoved_GetsNotFoundOnTheNextRead()
    {
        var w = await WorldAsync();
        Assert.Equal(HttpStatusCode.OK, (await AsOlgaAsync(w, "альфа")).Status);

        await MembershipTestData.RemoveMemberAsync(factory, w.OrganizationId, w.Olga.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await AsOlgaAsync(w, "альфа")).Status);
    }

    // Not signed in: 401, never a read.
    [Fact]
    public async Task NotSignedIn_Is401()
    {
        var w = await WorldAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await SearchTestKit.SearchAsync(factory, w.OrganizationId, null, "альфа")).Status);
    }

    // A viewer is a member and searches (the route needs the organization's read, which every role holds).
    [Fact]
    public async Task AViewer_Searches()
    {
        var w = await WorldAsync();
        var viewer = await MembershipTestData.NewMemberAsync(factory, w.OrganizationId, OrganizationRole.Viewer);

        var answer = await SearchTestKit.SearchAsync(factory, w.OrganizationId, viewer.Token, "альфа");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
    }
}
