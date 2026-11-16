using System.Net;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Iterations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Domain.Iterations;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// feat-041 code review round 1: R-2 (ADR-0029 § 8 item 4: a sprint or an item that does not exist answers its not-found before any
/// 403, the container being visible) and R-5 (a body id that names a project or team the caller does not see answers as one that
/// names nothing).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ProjectSightNestedIdsTests(MustaskApiFactory factory)
{
    // R-2 · a viewer lacks the sprint and the item write codes: a missing sprint or item is 404 first, an existing one is 403.
    [Fact]
    public async Task ASprintOrAnItemThatDoesNotExist_Is404_BeforeTheViewers403_AnExistingOneIs403()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var viewer = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        var sprint = await IterationsTestData.NewAsync(factory, world, world.PlatformId);
        var identifier = await WorkItemsTestData.CreateItemAsync(factory, world.Items, "TASK", "Задача");
        var team = BoardsTestData.Team(world, world.PlatformId);
        var project = OrganizationsApi.Project(world.OrganizationId, world.ProjectId);

        var missingSprint = await OrganizationsApi.SendAsync(factory, HttpMethod.Delete, $"{team}/sprints/{Guid.CreateVersion7()}", null, viewer.Token);
        var malformedSprint = await OrganizationsApi.SendAsync(factory, HttpMethod.Delete, $"{team}/sprints/abc", null, viewer.Token);
        var realSprint = await OrganizationsApi.SendAsync(factory, HttpMethod.Delete, $"{team}/sprints/{sprint}", null, viewer.Token);
        var missingItem = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{project}/workitems/MSK-TASK-999/delete", null, viewer.Token);
        var malformedItem = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{project}/workitems/abc/delete", null, viewer.Token);
        var realItem = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{project}/workitems/{identifier}/delete", null, viewer.Token);

        Assert.Equal((HttpStatusCode.NotFound, SprintErrorCodes.NotFound), (missingSprint.Status, missingSprint.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, SprintErrorCodes.NotFound), (malformedSprint.Status, malformedSprint.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, WorkItemErrorCodes.WorkItemNotFound), (missingItem.Status, missingItem.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, WorkItemErrorCodes.WorkItemNotFound), (malformedItem.Status, malformedItem.ErrorCode));
        Assert.Equal(HttpStatusCode.Forbidden, realSprint.Status);
        Assert.Equal(HttpStatusCode.Forbidden, realItem.Status);
    }

    // R-5 · POST /teams/{t}/projects: a project the caller does not see answers byte for byte as an unknown one (404, not 403).
    [Fact]
    public async Task ABodyProjectThatIsHidden_AnswersAsOneThatNamesNothing()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var hidden = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id, "Скрытый", "HID", visibleToOrganization: false);
        var team = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "Команда");
        var manager = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);
        await OrganizationsTestData.GrantAsync(factory, ResourceScope.Team, organization.Id, team.Id, manager.Id, GrantRole.Manager);
        var path = $"{OrganizationsApi.OrgTeams(organization.Id)}/{team.Id}/projects";

        var hiddenAnswer = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, path, new { projectIds = new[] { hidden.Project.Id.ToString() } }, manager.Token);
        var unknownAnswer = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, path, new { projectIds = new[] { Guid.CreateVersion7().ToString() } }, manager.Token);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.project.not_found"), (unknownAnswer.Status, unknownAnswer.ErrorCode));
        Assert.Equal((unknownAnswer.Status, unknownAnswer.ErrorCode, unknownAnswer.Detail), (hiddenAnswer.Status, hiddenAnswer.ErrorCode, hiddenAnswer.Detail));
    }
}
