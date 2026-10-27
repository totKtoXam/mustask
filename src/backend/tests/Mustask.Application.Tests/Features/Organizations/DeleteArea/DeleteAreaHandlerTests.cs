using Mustask.Application.Features.Organizations.DeleteArea;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.DeleteArea;

/// <summary>mod-018 US-2 on fakes: the deletion, the order of its answers (BR-AW-15) and its lock order (ADR-0028 rule 8).</summary>
public sealed class DeleteAreaHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Reports = Store.AddArea(Organization, "Отчёты");
            Quarterly = Store.AddArea(Organization, "Квартальные", Reports);
            Areas = new FakeAreaRepository(Store);
            Handler = new DeleteAreaHandler(new FakeOrganizationReadModel(Store), Areas, new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public Area Reports { get; }

        public Area Quarterly { get; }

        public FakeAreaRepository Areas { get; }

        public DeleteAreaHandler Handler { get; }

        public Task HandleAsync(Area area, string? organizationId = null) =>
            Handler.Handle(new DeleteAreaCommand(organizationId ?? Organization.Id.ToString(), area.Id.ToString()), CancellationToken.None);
    }

    // US-2 · "A leaf area is deleted; its links go with it and nothing else changes" (BR-AW-10).
    [Fact]
    public async Task Handle_Leaf_IsDeleted_WithItsLinks()
    {
        var f = new Fixture();
        var project = f.Store.AddProject(f.Organization);
        var team = f.Store.AddTeam(f.Organization);
        f.Store.LinkArea(f.Quarterly, project);
        f.Store.LinkArea(f.Quarterly, team);

        await f.HandleAsync(f.Quarterly);

        Assert.DoesNotContain(f.Store.Areas, area => area.Id == f.Quarterly.Id);
        Assert.Empty(f.Store.AreaProjects);
        Assert.Empty(f.Store.AreaTeams);
        Assert.Contains(f.Store.Projects, candidate => candidate.Id == project.Id);
        Assert.Contains(f.Store.Teams, candidate => candidate.Id == team.Id);
    }

    // US-2 · "An area with child areas is not deleted" (BR-AW-3): «Нельзя удалить область, в которой есть вложенные области».
    [Fact]
    public async Task Handle_WithChildren_IsAreaNotEmpty_AndNothingChanges()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Reports));

        Assert.Equal(OrganizationErrorCodes.AreaNotEmpty, refusal.Code);
        Assert.Equal(2, f.Store.Areas.Count);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // BR-AW-15 outside in: organization, then area (404), never the existence of another organization's area.
    [Fact]
    public async Task Handle_Address_Answers404OutsideIn()
    {
        var f = new Fixture();
        var foreign = f.Store.AddArea(f.Store.AddOrganization("Другая"), "Чужая");

        var org = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Reports, "abc"));
        var area = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(foreign));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, org.Code);
        Assert.Equal(OrganizationErrorCodes.AreaNotFound, area.Code);
        Assert.Contains(f.Store.Areas, candidate => candidate.Id == foreign.Id);
    }

    // ADR-0028 rule 8: the tree lock, then the area FOR UPDATE, then one save.
    [Fact]
    public async Task Handle_LocksTheTree_ThenTheArea_ThenSavesOnce()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Quarterly);

        Assert.Equal([$"lock area tree {f.Organization.Id}", $"lock area update {f.Quarterly.Id}", "save"], f.Store.Calls);
    }

    // ADR-0028 A-9: absent from the lock statement's answer is 404, whatever the unlocked read saw (a concurrent delete won).
    [Fact]
    public async Task Handle_VanishedAtTheLock_IsAreaNotFound()
    {
        var f = new Fixture();
        f.Areas.VanishedAfterTheLock.Add(f.Quarterly.Id);

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Quarterly));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
    }
}
