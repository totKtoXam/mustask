using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}</c>
/// (mod-003 api-contract § 2.3, US-3): the identifier resolved inside the
/// address's organization, under the addressed project (BR-M3-5, D-5), and the
/// not-found answers from the outside in (§ 0.3).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetWorkItemEndpointTests(MustaskApiFactory factory)
{
    /// <summary>«Мастаск» with MSK-EPIC-1, MSK-FEAT-2, MSK-US-3 and MSK-BUG-4 «Падение при входе».</summary>
    private async Task<WorkItemsWorld> WorldWithBug4Async()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);

        foreach (var type in new[] { "EPIC", "FEAT", "US" })
        {
            await WorkItemsTestData.CreateItemAsync(factory, world, type, $"Элемент {type}");
        }

        Assert.Equal("MSK-BUG-4", await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Падение при входе"));
        return world;
    }

    // US-3 · "The form opens at the item's address": the item with its type, which the form shows and never changes.
    [Fact]
    public async Task Get_ByIdentifier_Returns200_TheItem()
    {
        var world = await WorldWithBug4Async();

        var answer = await WorkItemsTestData.GetAsync(factory, world, "MSK-BUG-4");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("MSK-BUG-4", answer.Body.GetProperty("identifier").GetString());
        Assert.Equal("Падение при входе", answer.Body.GetProperty("title").GetString());
        Assert.Equal(("BUG", "Баг"), (
            answer.Body.GetProperty("type").GetProperty("key").GetString(),
            answer.Body.GetProperty("type").GetProperty("name").GetString()));
        Assert.Equal(world.ProjectId, answer.Body.GetProperty("projectId").GetGuid());
    }

    // US-3 · "The identifier is resolved inside the address's organization": two organizations, each with a project MSK
    // and an item MSK-BUG-4; each address answers its own, and «Орг А» with «Орг Б»'s project is «Проект не найден».
    [Fact]
    public async Task Get_SameIdentifierInTwoOrganizations_EachAnswersItsOwn_ForeignProjectIsNotFound()
    {
        var a = await WorldWithBug4Async();
        var b = await WorldWithBug4Async();
        await WorkItemsTestData.SaveAsync(factory, b, "MSK-BUG-4", new { title = "Баг организации Б" });

        var ofA = await WorkItemsTestData.GetAsync(factory, a, "MSK-BUG-4");
        var ofB = await WorkItemsTestData.GetAsync(factory, b, "MSK-BUG-4");
        var crossed = await WorkItemsTestData.GetAsync(factory, a with { ProjectId = b.ProjectId }, "MSK-BUG-4");

        Assert.Equal(("Падение при входе", a.ProjectId), (ofA.Body.GetProperty("title").GetString(), ofA.Body.GetProperty("projectId").GetGuid()));
        Assert.Equal(("Баг организации Б", b.ProjectId), (ofB.Body.GetProperty("title").GetString(), ofB.Body.GetProperty("projectId").GetGuid()));
        Assert.Equal(HttpStatusCode.NotFound, crossed.Status);
        Assert.Equal("organizations.project.not_found", crossed.ErrorCode);
    }

    // US-3 · "An identifier that resolves to nothing": every case of the identifier answers «Рабочий элемент не найден»,
    // ABC-BUG-4 included (ABC is not the addressed project's key, D-5), and MSK-XYZ-1 (no such type key).
    [Theory]
    [InlineData("msk-bug-4")]
    [InlineData("MSK-BUG-04")]
    [InlineData("MSK-BUG-999")]
    [InlineData("MSK-TASK-4")]
    [InlineData("ABC-BUG-4")]
    [InlineData("MSK-XYZ-1")]
    [InlineData("MSK-BUG-0")]
    [InlineData("4")]
    public async Task Get_IdentifierResolvingToNothing_Returns404WorkItemNotFound(string identifier)
    {
        var world = await WorldWithBug4Async();

        var answer = await WorkItemsTestData.GetAsync(factory, world, identifier);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("workitems.workitem.not_found", answer.ErrorCode);
        Assert.Equal("Рабочий элемент не найден.", answer.Detail);
    }

    // US-3 · "An identifier that resolves to nothing", the project rows: a malformed or unknown project id, a deactivated
    // «Мастаск», another organization's project — «Проект не найден», whatever the identifier.
    [Fact]
    public async Task Get_ProjectMalformedUnknownDeactivatedOrForeign_Returns404ProjectNotFound()
    {
        var world = await WorldWithBug4Async();
        var other = await WorldWithBug4Async();

        var malformed = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, $"/api/organizations/{world.OrganizationId}/projects/abc/workitems/MSK-BUG-4", token: OrganizationsTokens.SignedIn());
        var unknown = await WorkItemsTestData.GetAsync(factory, world with { ProjectId = Guid.CreateVersion7() }, "MSK-BUG-4");
        var foreign = await WorkItemsTestData.GetAsync(factory, world with { ProjectId = other.ProjectId }, "msk-bug-4");
        await OrganizationsTestData.SetProjectStatusAsync(factory, world.OrganizationId, world.ProjectId, ProjectStatus.Deactivated);
        var deactivated = await WorkItemsTestData.GetAsync(factory, world, "MSK-BUG-4");

        Assert.All(
            new[] { malformed, unknown, foreign, deactivated },
            answer =>
            {
                Assert.Equal(HttpStatusCode.NotFound, answer.Status);
                Assert.Equal("organizations.project.not_found", answer.ErrorCode);
            });
    }

    // spec § Roles and access.
    [Fact]
    public async Task Get_NoToken_Returns401()
    {
        var world = await WorldWithBug4Async();

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, WorkItemsTestData.Item(world, "MSK-BUG-4"));

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
    }
}
