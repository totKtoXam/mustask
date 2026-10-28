using Mustask.Application.Features.Organizations.GetTeamDeletionCounts;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.GetTeamDeletionCounts;

/// <summary>
/// <c>GetTeamDeletionCounts</c> on fakes (feat-028 api-contract § 1.5, § 2.1; US-5 "the confirmation counts per project"): every
/// project the team is linked to, deactivated ones and zeros included, by project name.
/// </summary>
public sealed class GetTeamDeletionCountsHandlerTests
{
    private static GetTeamDeletionCountsHandler Handler(InMemoryOrganizations store, FakeWorkItemTeamOwnership ownership) =>
        new(new FakeTeamReadModel(store), ownership);

    // US-5 · per project, by name; a deactivated project is listed with active=false; a project where the team owns nothing reads 0.
    [Fact]
    public async Task Counts_PerLinkedProject_DeactivatedAndZerosIncluded()
    {
        var store = new InMemoryOrganizations();
        var organization = store.AddOrganization();
        var alpha = store.AddProject(organization, "Альфа", "ALP");
        var delta = store.AddProject(organization, "Дельта", "DEL");
        var zero = store.AddProject(organization, "Ноль", "ZER");
        delta.Project.Deactivate();
        var mars = store.AddTeam(organization, "MARS");
        store.Link(alpha, mars);
        store.Link(delta, mars);
        store.Link(zero, mars);
        var ownership = new FakeWorkItemTeamOwnership(store);
        ownership.Owned[(alpha.Project.Id, mars.Id)] = 5;
        ownership.Owned[(delta.Project.Id, mars.Id)] = 3;

        var result = await Handler(store, ownership).Handle(
            new GetTeamDeletionCountsQuery(organization.Id.ToString(), mars.Id.ToString()), CancellationToken.None);

        Assert.Equal(
            [("Альфа", true, 5), ("Дельта", false, 3), ("Ноль", true, 0)],
            result.Projects.Select(project => (project.Name, project.Active, project.Count)));
    }

    // US-5 · a team linked to no project: an empty list.
    [Fact]
    public async Task Counts_TeamLinkedToNone_IsEmpty()
    {
        var store = new InMemoryOrganizations();
        var organization = store.AddOrganization();
        var mars = store.AddTeam(organization, "MARS");

        var result = await Handler(store, new FakeWorkItemTeamOwnership(store)).Handle(
            new GetTeamDeletionCountsQuery(organization.Id.ToString(), mars.Id.ToString()), CancellationToken.None);

        Assert.Empty(result.Projects);
    }

    [Theory]
    [InlineData("deleted")]
    [InlineData("foreign")]
    [InlineData("malformed")]
    public async Task Counts_TeamNotActiveInTheOrganization_IsTeamNotFound(string how)
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

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => Handler(store, new FakeWorkItemTeamOwnership(store)).Handle(
            new GetTeamDeletionCountsQuery(organization.Id.ToString(), id), CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }
}
