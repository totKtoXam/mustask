using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// mod-018 backend fix (2026-10-29): a project whose node sits under a department. The work item's area path is
/// project-relative (design § 8): the project's node is the top, whatever stands above it, so the department never appears in it.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class WorkItemAreaUnderDepartmentEndpointTests(MustaskApiFactory factory)
{
    // US-4 / design § 8 · the card, the list, the create and the save answer under a department with the project-relative path.
    [Fact]
    public async Task ItemReads_UnderADepartment_AnswerTheProjectRelativePath()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory, "UDP", "Alpha");
        var node = await OrganizationsTestData.NodeOfAsync(factory, world.ProjectId);
        var sub = await OrganizationsTestData.AddSubAreaAsync(factory, world.OrganizationId, node.Id, "Платформа");
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, world.OrganizationId, "Логистика");

        var created = await WorkItemsTestData.CreateAsync(factory, world, "BUG", "До переноса");
        var move = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Areas(world.OrganizationId)}/{node.Id}/parent",
            new { parentId = department.Id.ToString() },
            OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal(HttpStatusCode.OK, move.Status);

        var identifier = created.Body.GetProperty("identifier").GetString()!;
        var saved = await WorkItemsTestData.SaveAsync(factory, world, identifier, new { areaId = sub.Id.ToString() });
        var card = await WorkItemsTestData.GetAsync(factory, world, identifier);
        var list = await WorkItemsTestData.ListAsync(factory, world);
        var after = await WorkItemsTestData.CreateAsync(factory, world, "BUG", "После переноса");

        Assert.Equal(HttpStatusCode.OK, saved.Status);
        Assert.Equal(HttpStatusCode.OK, card.Status);
        Assert.Equal(HttpStatusCode.OK, list.Status);
        Assert.Equal(HttpStatusCode.Created, after.Status);
        Assert.Equal("Alpha / Платформа", saved.Body.GetProperty("area").GetProperty("path").GetString());
        Assert.Equal("Alpha / Платформа", card.Body.GetProperty("area").GetProperty("path").GetString());
        Assert.Equal("Alpha", after.Body.GetProperty("area").GetProperty("path").GetString());

        var listed = list.Body.GetProperty("data").EnumerateArray()
            .Single(item => item.GetProperty("identifier").GetString() == identifier);
        Assert.Equal("Alpha / Платформа", listed.GetProperty("area").GetProperty("path").GetString());
    }
}
