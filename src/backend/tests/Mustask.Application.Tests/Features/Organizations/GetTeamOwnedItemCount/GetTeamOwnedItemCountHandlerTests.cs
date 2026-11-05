using Mustask.Application.Features.Organizations.GetTeamOwnedItemCount;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.GetTeamOwnedItemCount;

/// <summary>
/// <c>GetTeamOwnedItemCount</c> (the unlink's count) on fakes (feat-028 api-contract § 2.4, BR-OL-8; US-4 "the dialog states the items
/// and one destination"): the number of items a team owns <b>in this project</b>, in any state; the address answered from the
/// outermost level in, where a team not linked to the project is a not-found. Rewritten from the deletion's count (mod-017 BR-OT-12;
/// D-16).
/// </summary>
public sealed class GetTeamOwnedItemCountHandlerTests
{
    private sealed class Fixture
    {
        public Fixture(OrganizationRole role = OrganizationRole.Admin)
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Other = Store.AddProject(Organization, "Второй проект", "SEC");
            Platform = Store.AddLinkedTeam(Organization, Project);
            Store.Link(Other, Platform);
            Ownership = new FakeWorkItemTeamOwnership(Store);
            Handler = new GetTeamOwnedItemCountHandler(
                new FakeProjectReadModel(Store), new FakeTeamReadModel(Store), Ownership, new FakeOrganizationAccess(role));
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public NewProject Other { get; }

        public Team Platform { get; }

        public FakeWorkItemTeamOwnership Ownership { get; }

        public GetTeamOwnedItemCountHandler Handler { get; }

        public GetTeamOwnedItemCountQuery Query(string? teamId = null) =>
            new(Organization.Id.ToString(), Project.Project.Id.ToString(), teamId ?? Platform.Id.ToString());

        public Task<GetTeamOwnedItemCountResult> HandleAsync(GetTeamOwnedItemCountQuery query) => Handler.Handle(query, CancellationToken.None);
    }

    // US-4 · "the dialog states the items": this project's items only, never another project's; zero when the team owns nothing.
    [Fact]
    public async Task Count_IsThisProjectsItemsInAnyState_NotAnotherProjects_LocksNothing()
    {
        var f = new Fixture();
        f.Ownership.Owned[(f.Project.Project.Id, f.Platform.Id)] = 7;
        f.Ownership.Owned[(f.Other.Project.Id, f.Platform.Id)] = 100;
        var empty = f.Store.AddLinkedTeam(f.Organization, f.Project, "Пустая");

        var count = await f.HandleAsync(f.Query());
        var none = await f.HandleAsync(f.Query(empty.Id.ToString()));

        Assert.Equal(7, count.Count);
        Assert.Equal(0, none.Count);
        Assert.Empty(f.Store.Calls);
    }

    // BR-OL-11, api-contract § 0.3 · a team not linked to the project, a deleted one, a foreign and a malformed one: team.not_found
    // (renamed from Count_NotATeamOfTheProject_Returns404TeamNotFound, D-16).
    [Theory]
    [InlineData("not linked")]
    [InlineData("deleted")]
    [InlineData("of another organization")]
    [InlineData("malformed")]
    public async Task Count_NotLinkedToTheProject_Returns404TeamNotFound(string how)
    {
        var f = new Fixture();
        var unlinked = f.Store.AddTeam(f.Organization, "Не связана");
        var foreign = f.Store.AddTeam(f.Store.AddOrganization("Другая организация"), "Чужая");
        var query = how switch
        {
            "not linked" => f.Query(unlinked.Id.ToString()),
            "of another organization" => f.Query(foreign.Id.ToString()),
            "malformed" => f.Query("not-a-team"),
            _ => f.Query(),
        };

        if (how == "deleted")
        {
            f.Platform.Delete();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(query));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }

    [Fact]
    public async Task Count_ProjectNotReachable_IsProjectNotFound_BeforeTheTeam()
    {
        var f = new Fixture();
        f.Project.Project.Deactivate();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Query("not-a-team")));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
    }

    // D-7 · the route declares project.link and the handler asks team.link first of all: a role without it learns nothing.
    [Fact]
    public async Task Count_WithoutTheSecondCode_Is403_BeforeAnyIdIsLooked_At()
    {
        var f = new Fixture(OrganizationRole.Member);

        var refusal = await Assert.ThrowsAsync<ForbiddenException>(() => f.HandleAsync(f.Query("not-a-team")));

        Assert.Equal(OrganizationErrorCodes.AccessForbidden, refusal.Code);
    }
}
