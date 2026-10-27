using Mustask.Application.Features.Organizations.GetOrganizationTeams;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Shared.Exceptions;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Tests.Features.Organizations.GetOrganizationTeams;

/// <summary>
/// <c>GetOrganizationTeams</c> on fakes (feat-028 api-contract § 1.2, § 2.1; US-1 "the organization's teams are listed", US-7 "no team
/// anywhere"): the active teams by name with their active projects, never another organization's, never a deleted team.
/// </summary>
public sealed class GetOrganizationTeamsHandlerTests
{
    // US-1 · by name in any letter case, then id; each with its ACTIVE projects by name; a deactivated project is left out of the row
    // but its link stays; a deleted team and another organization's team are not listed.
    [Fact]
    public async Task Get_ListsTheOrganizationsActiveTeams_ByNameAnyCase_WithTheirActiveProjects()
    {
        var store = new InMemoryOrganizations();
        var organization = store.AddOrganization();
        var alpha = store.AddProject(organization, "Альфа", "ALP");
        var beta = store.AddProject(organization, "бета", "BET");
        var old = store.AddProject(organization, "Старый", "OLD");
        old.Deactivate();
        var mars = store.AddTeam(organization, "mars");
        var venus = store.AddTeam(organization, "VENUS");
        store.AddTeam(organization, "Удалена").Delete();
        store.AddTeam(store.AddOrganization("Другая"), "Чужая");
        store.Link(beta, mars);
        store.Link(alpha, mars);
        store.Link(old, mars);
        var handler = new GetOrganizationTeamsHandler(new FakeTeamReadModel(store), new FakeCurrentUser());

        var result = await handler.Handle(new GetOrganizationTeamsQuery(organization.Id.ToString()), CancellationToken.None);

        Assert.Equal(["mars", "VENUS"], result.Items.Select(team => team.Name));
        Assert.Equal(["Альфа", "бета"], result.Items[0].Projects.Select(project => project.Name));
        Assert.Empty(result.Items[1].Projects);
        Assert.Equal(venus.Id, result.Items[1].Id);
        Assert.True(store.IsLinked(old.Id, mars.Id));
    }

    // feat-040 api-contract § 4.1 · "the user's own teams first": each team says whether the CALLER is in it — the caller's team,
    // a team only someone else is in and a team nobody is in are told apart; the list's own order (by name) does not change.
    [Fact]
    public async Task Get_SaysWhichTeamsTheCallerIsIn_AndKeepsTheServersOrder()
    {
        var store = new InMemoryOrganizations();
        var organization = store.AddOrganization();
        var mars = store.AddTeam(organization, "MARS");
        var venus = store.AddTeam(organization, "VENUS");
        store.AddTeam(organization, "JUPITER");
        var other = store.AddOrganization("Другая");
        store.AddTeam(other, "Орион");
        var caller = new FakeCurrentUser { SubjectId = "u-max" };
        store.AddTeamMember(organization, venus, "u-max");
        store.AddTeamMember(organization, mars, "u-someone-else");
        var handler = new GetOrganizationTeamsHandler(new FakeTeamReadModel(store), caller);

        var result = await handler.Handle(new GetOrganizationTeamsQuery(organization.Id.ToString()), CancellationToken.None);

        Assert.Equal(["JUPITER", "MARS", "VENUS"], result.Items.Select(team => team.Name));
        Assert.Equal([false, false, true], result.Items.Select(team => team.IsMember));
    }

    // US-7 · the first state after the upgrade: no team at all.
    [Fact]
    public async Task Get_AfterTheUpgrade_IsEmpty()
    {
        var store = new InMemoryOrganizations();
        var organization = store.AddOrganization();
        var handler = new GetOrganizationTeamsHandler(new FakeTeamReadModel(store), new FakeCurrentUser());

        var result = await handler.Handle(new GetOrganizationTeamsQuery(organization.Id.ToString()), CancellationToken.None);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Get_MalformedOrganization_IsOrganizationNotFound()
    {
        var handler = new GetOrganizationTeamsHandler(new FakeTeamReadModel(new InMemoryOrganizations()), new FakeCurrentUser());

        var refusal = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new GetOrganizationTeamsQuery("abc"), CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }
}
