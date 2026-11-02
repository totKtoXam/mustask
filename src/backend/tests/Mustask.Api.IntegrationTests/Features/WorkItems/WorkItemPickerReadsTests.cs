using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// The backend half of the form's «Статус» and «Область» controls (mod-003
/// api-contract § 3): they read mod-001's process and mod-002's area tree
/// unchanged, and these tests pin that those reads answer what US-4 and US-5
/// expect the controls to offer.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class WorkItemPickerReadsTests(MustaskApiFactory factory)
{
    // US-5 · "The control offers the type's states without «Удалена»": the process read gives each type's states in order,
    // and those not in the category Removed are exactly the outline's.
    [Theory]
    [InlineData("TASK", "Новая,Активна,Закрыта")]
    [InlineData("BUG", "Новая,Активна,Разрешена,Закрыта")]
    [InlineData("US", "Новая,Активна,Разрешена,Закрыта")]
    [InlineData("EPIC", "Новая,Активна,Разрешена,Закрыта")]
    [InlineData("FEAT", "Новая,Активна,Разрешена,Закрыта")]
    public async Task ProcessRead_TypesStatesWithoutRemoved_AreTheControlsOptions(string typeKey, string expected)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, $"/api/organizations/{organization.Id}/process", token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var type = answer.Body.GetProperty("workItemTypes").EnumerateArray().Single(candidate => candidate.GetProperty("key").GetString() == typeKey);
        var offered = type.GetProperty("states").EnumerateArray()
            .Where(state => state.GetProperty("category").GetProperty("code").GetString() != "Removed")
            .Select(state => state.GetProperty("name").GetString());
        Assert.Equal(expected.Split(','), offered);
    }

    // US-4 · "The area picker offers the project's whole tree": «Мастаск», «Отчёты», «Платформа», «Платформа / API» in
    // tree order, the root first, and nothing of «Операции».
    [Fact]
    public async Task AreaTreeRead_OffersTheProjectsTreeInOrder_RootFirst_NoOtherProject()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var platform = await OrganizationsTestData.AddAreaAsync(factory, world.ProjectId, world.RootAreaId, "Платформа");
        await OrganizationsTestData.AddAreaAsync(factory, world.ProjectId, platform.Id, "API");
        await OrganizationsTestData.AddAreaAsync(factory, world.ProjectId, world.RootAreaId, "Отчёты");
        var operations = await WorkItemsTestData.AddProjectAsync(factory, world, "OPS", "Операции");
        await OrganizationsTestData.AddAreaAsync(factory, operations.ProjectId, operations.RootAreaId, "Склад");

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, OrganizationsApi.Project(world.OrganizationId, world.ProjectId) + "/areas", token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(
            ["Мастаск", "Мастаск / Отчёты", "Мастаск / Платформа", "Мастаск / Платформа / API"],
            answer.Body.EnumerateArray().Select(node => node.GetProperty("path").GetString()));
    }
}
