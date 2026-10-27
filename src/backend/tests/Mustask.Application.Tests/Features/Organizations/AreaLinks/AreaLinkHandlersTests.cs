using Mustask.Application.Features.Organizations.LinkAreasToProject;
using Mustask.Application.Features.Organizations.LinkAreasToTeam;
using Mustask.Application.Features.Organizations.LinkProjectsToArea;
using Mustask.Application.Features.Organizations.LinkTeamsToArea;
using Mustask.Application.Features.Organizations.UnlinkAreaFromProject;
using Mustask.Application.Features.Organizations.UnlinkAreaFromTeam;
using Mustask.Application.Features.Organizations.UnlinkProjectFromArea;
using Mustask.Application.Features.Organizations.UnlinkTeamFromArea;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.AreaLinks;

/// <summary>
/// mod-018 US-3 / US-4 on fakes: the eight link and unlink slices of an area with a project or a team — what each stores, the
/// second codes asked first of all (D-7), the 404 of each level, the idempotence (BR-AW-6) and the lock order (ADR-0028 rule 8).
/// </summary>
public sealed class AreaLinkHandlersTests
{
    private sealed class World
    {
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeClock _clock = new();
        private readonly FakeCurrentUser _user = new();

        public World(OrganizationRole role = OrganizationRole.Admin)
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Team = Store.AddTeam(Organization);
            Reports = Store.AddArea(Organization, "Отчёты");
            Areas = new FakeAreaRepository(Store);
            Access = new FakeOrganizationAccess(role);
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public Project Project { get; }

        public Team Team { get; }

        public Area Reports { get; }

        public FakeAreaRepository Areas { get; }

        public FakeOrganizationAccess Access { get; }

        public string Org => Organization.Id.ToString();

        public LinkProjectsToAreaHandler LinkProjectsToArea() => new(
            new FakeOrganizationReadModel(Store), new FakeProjectRepository(Store), Areas, new FakeAreaLinkRepository(Store), Access, _user, _clock, _unitOfWork);

        public LinkTeamsToAreaHandler LinkTeamsToArea() => new(
            new FakeOrganizationReadModel(Store), new FakeTeamRepository(Store), Areas, new FakeAreaLinkRepository(Store), Access, _user, _clock, _unitOfWork);

        public LinkAreasToProjectHandler LinkAreasToProject() => new(
            new FakeOrganizationReadModel(Store), new FakeProjectRepository(Store), Areas, new FakeAreaLinkRepository(Store), Access, _user, _clock, _unitOfWork);

        public LinkAreasToTeamHandler LinkAreasToTeam() => new(
            new FakeOrganizationReadModel(Store),
            new FakeTeamRepository(Store),
            new FakeOrganizationStructure(Store),
            Areas,
            new FakeAreaLinkRepository(Store),
            Access,
            _user,
            _clock,
            _unitOfWork);

        public UnlinkProjectFromAreaHandler UnlinkProjectFromArea() => new(
            new FakeOrganizationReadModel(Store), new FakeProjectRepository(Store), Areas, new FakeAreaLinkRepository(Store), _unitOfWork);

        public UnlinkTeamFromAreaHandler UnlinkTeamFromArea() => new(
            new FakeOrganizationReadModel(Store), new FakeOrganizationStructure(Store), Areas, new FakeAreaLinkRepository(Store), _unitOfWork);

        public UnlinkAreaFromProjectHandler UnlinkAreaFromProject() => new(
            new FakeOrganizationReadModel(Store), new FakeProjectRepository(Store), Areas, new FakeAreaLinkRepository(Store), _unitOfWork);

        public UnlinkAreaFromTeamHandler UnlinkAreaFromTeam() => new(
            new FakeOrganizationReadModel(Store), new FakeOrganizationStructure(Store), Areas, new FakeAreaLinkRepository(Store), _unitOfWork);
    }

    private static CancellationToken Ct => CancellationToken.None;

    // ---- the area side: projects --------------------------------------------------------------------------------------

    // US-3 · "Projects are linked to an area" — one link per pair, who and when kept, nothing granted (BR-AW-6, BR-AW-8).
    [Fact]
    public async Task LinkProjectsToArea_StoresTheLinks_ASecondTimeChangesNothing()
    {
        var w = new World();
        var second = w.Store.AddProject(w.Organization, "Второй", "SEC");
        var command = new LinkProjectsToAreaCommand(w.Org, w.Reports.Id.ToString(), [w.Project.Id.ToString(), second.Id.ToString()]);

        await w.LinkProjectsToArea().Handle(command, Ct);
        await w.LinkProjectsToArea().Handle(command, Ct);

        Assert.Equal(2, w.Store.AreaProjects.Count);
        Assert.All(w.Store.AreaProjects, link => Assert.Equal(w.Reports.Id, link.AreaId));
    }

    // ADR-0028 rule 8: the area FOR SHARE, then the projects are read; one insert, no binder lock.
    [Fact]
    public async Task LinkProjectsToArea_LocksTheAreaShare_ThenInsertsOnce()
    {
        var w = new World();

        await w.LinkProjectsToArea().Handle(new LinkProjectsToAreaCommand(w.Org, w.Reports.Id.ToString(), [w.Project.Id.ToString()]), Ct);

        Assert.Equal([$"lock area share {w.Reports.Id}", "link area projects 1"], w.Store.Calls);
    }

    // US-6 · the second code project.link is asked first of all: a member learns nothing about any id.
    [Fact]
    public async Task LinkProjectsToArea_WithoutProjectLink_IsForbidden_BeforeAnyRead()
    {
        var w = new World(OrganizationRole.Member);

        var refusal = await Assert.ThrowsAsync<ForbiddenException>(() =>
            w.LinkProjectsToArea().Handle(new LinkProjectsToAreaCommand("abc", "x", ["y"]), Ct));

        Assert.Equal(OrganizationErrorCodes.AccessForbidden, refusal.Code);
        Assert.Empty(w.Store.Calls);
    }

    // BR-AW-15: the organization, then the area, then each project (404 each), nothing stored on a refusal.
    [Fact]
    public async Task LinkProjectsToArea_Address_Answers404OutsideIn()
    {
        var w = new World();
        w.Project.Deactivate();
        var handler = w.LinkProjectsToArea();

        var org = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new LinkProjectsToAreaCommand("abc", w.Reports.Id.ToString(), []), Ct));
        var area = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new LinkProjectsToAreaCommand(w.Org, Guid.CreateVersion7().ToString(), [w.Project.Id.ToString()]), Ct));
        var project = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new LinkProjectsToAreaCommand(w.Org, w.Reports.Id.ToString(), [w.Project.Id.ToString()]), Ct));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, org.Code);
        Assert.Equal(OrganizationErrorCodes.AreaNotFound, area.Code);
        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, project.Code);
        Assert.Empty(w.Store.AreaProjects);
    }

    // ADR-0028 A-9: an area absent from the lock statement's answer is 404, whatever the earlier read saw.
    [Fact]
    public async Task LinkProjectsToArea_AreaDeletedBeforeTheLock_IsAreaNotFound()
    {
        var w = new World();
        w.Areas.VanishedAfterTheLock.Add(w.Reports.Id);

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() =>
            w.LinkProjectsToArea().Handle(new LinkProjectsToAreaCommand(w.Org, w.Reports.Id.ToString(), [w.Project.Id.ToString()]), Ct));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
        Assert.Empty(w.Store.AreaProjects);
    }

    // US-4 · "A project is unlinked from an area": the link goes; a pair that is not linked is a success.
    [Fact]
    public async Task UnlinkProjectFromArea_RemovesTheLink_ANonLinkedPairIsASuccess()
    {
        var w = new World();
        w.Store.LinkArea(w.Reports, w.Project);
        var command = new UnlinkProjectFromAreaCommand(w.Org, w.Reports.Id.ToString(), w.Project.Id.ToString());

        await w.UnlinkProjectFromArea().Handle(command, Ct);
        await w.UnlinkProjectFromArea().Handle(command, Ct);

        Assert.Empty(w.Store.AreaProjects);
        Assert.Contains(w.Store.Areas, area => area.Id == w.Reports.Id);
    }

    // US-3 / BR-AW-10: a deactivated project is not reachable, so its link is kept for a restore (404 project.not_found).
    [Fact]
    public async Task UnlinkProjectFromArea_DeactivatedProject_Returns404_KeepsTheLink()
    {
        var w = new World();
        w.Store.LinkArea(w.Reports, w.Project);
        w.Project.Deactivate();

        await Assert.ThrowsAsync<Mustask.Shared.Exceptions.NotFoundException>(() => w.UnlinkProjectFromArea().Handle(new UnlinkProjectFromAreaCommand(w.Org, w.Reports.Id.ToString(), w.Project.Id.ToString()), Ct));

        Assert.Single(w.Store.AreaProjects);
    }

    [Fact]
    public async Task UnlinkProjectFromArea_Address_Answers404OutsideIn()
    {
        var w = new World();
        var handler = w.UnlinkProjectFromArea();

        var area = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new UnlinkProjectFromAreaCommand(w.Org, "x", "y"), Ct));
        var project = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new UnlinkProjectFromAreaCommand(w.Org, w.Reports.Id.ToString(), Guid.CreateVersion7().ToString()), Ct));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, area.Code);
        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, project.Code);
    }

    // ---- the area side: teams -----------------------------------------------------------------------------------------

    // US-3 · "Teams are linked to an area"; ADR-0028 rule 8: the area FOR SHARE, then the teams FOR SHARE, then one insert.
    [Fact]
    public async Task LinkTeamsToArea_StoresTheLinks_LocksTheAreaThenTheTeams()
    {
        var w = new World();

        await w.LinkTeamsToArea().Handle(new LinkTeamsToAreaCommand(w.Org, w.Reports.Id.ToString(), [w.Team.Id.ToString()]), Ct);

        Assert.Equal(w.Team.Id, Assert.Single(w.Store.AreaTeams).TeamId);
        Assert.Equal([$"lock area share {w.Reports.Id}", $"lock teams share {w.Team.Id}", "link area teams 1"], w.Store.Calls);
    }

    [Fact]
    public async Task LinkTeamsToArea_DeletedOrUnknownTeam_IsTeamNotFound_WithoutTeamLinkIsForbidden()
    {
        var w = new World();
        w.Team.Delete();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() =>
            w.LinkTeamsToArea().Handle(new LinkTeamsToAreaCommand(w.Org, w.Reports.Id.ToString(), [w.Team.Id.ToString()]), Ct));
        var member = new World(OrganizationRole.Member);
        var forbidden = await Assert.ThrowsAsync<ForbiddenException>(() =>
            member.LinkTeamsToArea().Handle(new LinkTeamsToAreaCommand("abc", "x", []), Ct));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.Equal(OrganizationErrorCodes.AccessForbidden, forbidden.Code);
        Assert.Empty(w.Store.AreaTeams);
    }

    [Fact]
    public async Task UnlinkTeamFromArea_RemovesTheLink_ANonLinkedPairIsASuccess()
    {
        var w = new World();
        w.Store.LinkArea(w.Reports, w.Team);
        var command = new UnlinkTeamFromAreaCommand(w.Org, w.Reports.Id.ToString(), w.Team.Id.ToString());

        await w.UnlinkTeamFromArea().Handle(command, Ct);
        await w.UnlinkTeamFromArea().Handle(command, Ct);

        Assert.Empty(w.Store.AreaTeams);
    }

    [Fact]
    public async Task UnlinkTeamFromArea_UnknownTeam_IsTeamNotFound()
    {
        var w = new World();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() =>
            w.UnlinkTeamFromArea().Handle(new UnlinkTeamFromAreaCommand(w.Org, w.Reports.Id.ToString(), Guid.CreateVersion7().ToString()), Ct));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }

    // ---- the project side ---------------------------------------------------------------------------------------------

    // US-3 · "From a project's page, areas are linked": the second code is area.link; the areas are locked FOR SHARE.
    [Fact]
    public async Task LinkAreasToProject_StoresTheLinks_LocksTheAreas_NeedsAreaLink()
    {
        var w = new World();
        var archive = w.Store.AddArea(w.Organization, "Архив");

        await w.LinkAreasToProject().Handle(new LinkAreasToProjectCommand(w.Org, w.Project.Id.ToString(), [w.Reports.Id.ToString(), archive.Id.ToString()]), Ct);

        Assert.Equal(2, w.Store.AreaProjects.Count(link => link.ProjectId == w.Project.Id));
        Assert.Equal($"lock areas share {string.Join(",", new[] { w.Reports.Id, archive.Id }.Order())}", w.Store.Calls[0]);
    }

    [Fact]
    public async Task LinkAreasToProject_ForeignAreaOrDeactivatedProject_Is404_AndMemberIsForbidden()
    {
        var w = new World();
        var foreign = w.Store.AddArea(w.Store.AddOrganization("Другая"), "Чужая");

        var area = await Assert.ThrowsAsync<NotFoundException>(() =>
            w.LinkAreasToProject().Handle(new LinkAreasToProjectCommand(w.Org, w.Project.Id.ToString(), [foreign.Id.ToString()]), Ct));
        w.Project.Deactivate();
        var project = await Assert.ThrowsAsync<NotFoundException>(() =>
            w.LinkAreasToProject().Handle(new LinkAreasToProjectCommand(w.Org, w.Project.Id.ToString(), [w.Reports.Id.ToString()]), Ct));
        var forbidden = await Assert.ThrowsAsync<ForbiddenException>(() =>
            new World(OrganizationRole.Member).LinkAreasToProject().Handle(new LinkAreasToProjectCommand("abc", "x", []), Ct));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, area.Code);
        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, project.Code);
        Assert.Equal(OrganizationErrorCodes.AccessForbidden, forbidden.Code);
        Assert.Empty(w.Store.AreaProjects);
    }

    [Fact]
    public async Task UnlinkAreaFromProject_RemovesTheLink_ADeactivatedProjectIsNotFound()
    {
        var w = new World();
        w.Store.LinkArea(w.Reports, w.Project);
        var command = new UnlinkAreaFromProjectCommand(w.Org, w.Project.Id.ToString(), w.Reports.Id.ToString());

        await w.UnlinkAreaFromProject().Handle(command, Ct);
        Assert.Empty(w.Store.AreaProjects);

        w.Project.Deactivate();
        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => w.UnlinkAreaFromProject().Handle(command, Ct));
        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
    }

    // ---- the team side ------------------------------------------------------------------------------------------------

    // ADR-0028 rule 8: the areas FOR SHARE, then the team FOR SHARE and re-checked active, then one insert.
    [Fact]
    public async Task LinkAreasToTeam_StoresTheLinks_LocksTheAreasThenTheTeam()
    {
        var w = new World();

        await w.LinkAreasToTeam().Handle(new LinkAreasToTeamCommand(w.Org, w.Team.Id.ToString(), [w.Reports.Id.ToString()]), Ct);

        Assert.Equal(w.Reports.Id, Assert.Single(w.Store.AreaTeams).AreaId);
        Assert.Equal([$"lock areas share {w.Reports.Id}", $"lock team share {w.Team.Id}", "link area teams 1"], w.Store.Calls);
    }

    [Fact]
    public async Task LinkAreasToTeam_DeletedTeamOrVanishedArea_Is404_AndMemberIsForbidden()
    {
        var w = new World();
        w.Areas.VanishedAfterTheLock.Add(w.Reports.Id);

        var area = await Assert.ThrowsAsync<NotFoundException>(() =>
            w.LinkAreasToTeam().Handle(new LinkAreasToTeamCommand(w.Org, w.Team.Id.ToString(), [w.Reports.Id.ToString()]), Ct));
        w.Team.Delete();
        var team = await Assert.ThrowsAsync<NotFoundException>(() =>
            w.LinkAreasToTeam().Handle(new LinkAreasToTeamCommand(w.Org, w.Team.Id.ToString(), [w.Reports.Id.ToString()]), Ct));
        var forbidden = await Assert.ThrowsAsync<ForbiddenException>(() =>
            new World(OrganizationRole.Member).LinkAreasToTeam().Handle(new LinkAreasToTeamCommand("abc", "x", []), Ct));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, area.Code);
        Assert.Equal(OrganizationErrorCodes.TeamNotFound, team.Code);
        Assert.Equal(OrganizationErrorCodes.AccessForbidden, forbidden.Code);
        Assert.Empty(w.Store.AreaTeams);
    }

    [Fact]
    public async Task UnlinkAreaFromTeam_RemovesTheLink_ADeletedTeamIsNotFound()
    {
        var w = new World();
        w.Store.LinkArea(w.Reports, w.Team);
        var command = new UnlinkAreaFromTeamCommand(w.Org, w.Team.Id.ToString(), w.Reports.Id.ToString());

        await w.UnlinkAreaFromTeam().Handle(command, Ct);
        Assert.Empty(w.Store.AreaTeams);

        w.Team.Delete();
        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => w.UnlinkAreaFromTeam().Handle(command, Ct));
        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }
}
