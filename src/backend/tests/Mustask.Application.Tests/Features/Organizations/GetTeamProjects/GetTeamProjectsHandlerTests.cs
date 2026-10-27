using Mustask.Application.Features.Organizations.GetTeamProjects;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.GetTeamProjects;

/// <summary>
/// <c>GetTeamProjects</c> on fakes (feat-028 api-contract § 2.2; US-3 "a deactivated project keeps its links"): a team's active
/// projects by name, with the key.
/// </summary>
public sealed class GetTeamProjectsHandlerTests
{
    // US-3 · the active projects by name; a deactivated project is omitted and its link survives; a team linked to none: [].
    [Fact]
    public async Task Get_ListsTheActiveProjects_ByName_WithTheKey_OmitsADeactivatedOne()
    {
        var store = new InMemoryOrganizations();
        var organization = store.AddOrganization();
        var beta = store.AddProject(organization, "Бета", "BET");
        var alpha = store.AddProject(organization, "альфа", "ALP");
        var old = store.AddProject(organization, "Старый", "OLD");
        old.Deactivate();
        var mars = store.AddTeam(organization, "MARS");
        var empty = store.AddTeam(organization, "VENUS");
        store.Link(beta, mars);
        store.Link(alpha, mars);
        store.Link(old, mars);
        var handler = new GetTeamProjectsHandler(new FakeTeamReadModel(store));

        var projects = await handler.Handle(new GetTeamProjectsQuery(organization.Id.ToString(), mars.Id.ToString()), CancellationToken.None);
        var none = await handler.Handle(new GetTeamProjectsQuery(organization.Id.ToString(), empty.Id.ToString()), CancellationToken.None);

        Assert.Equal([("альфа", "ALP"), ("Бета", "BET")], projects.Items.Select(project => (project.Name, project.Key)));
        Assert.Empty(none.Items);
        Assert.True(store.IsLinked(old.Id, mars.Id));
    }

    // US-8 · a deleted team, a team of another organization and a malformed id answer team.not_found; the team is read even when its
    // only project is deactivated.
    [Theory]
    [InlineData("deleted")]
    [InlineData("foreign")]
    [InlineData("malformed")]
    public async Task Get_TeamNotActiveInTheOrganization_IsTeamNotFound(string how)
    {
        var store = new InMemoryOrganizations();
        var organization = store.AddOrganization();
        var mars = store.AddTeam(organization, "MARS");
        var foreign = store.AddTeam(store.AddOrganization("Другая"), "Чужая");

        if (how == "deleted")
        {
            mars.Delete();
        }

        var id = how switch { "foreign" => foreign.Id.ToString(), "malformed" => "abc", _ => mars.Id.ToString() };
        var handler = new GetTeamProjectsHandler(new FakeTeamReadModel(store));

        var refusal = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new GetTeamProjectsQuery(organization.Id.ToString(), id), CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }
}
