using Mustask.Application.Features.Organizations.LinkAreasToTeam;
using Mustask.Application.Features.Organizations.LinkTeamsToArea;
using Mustask.Application.Features.Organizations.UnlinkAreaFromTeam;
using Mustask.Application.Features.Organizations.UnlinkTeamFromArea;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.AreaLinks;

/// <summary>
/// mod-018 US-4 on fakes: the four link and unlink slices of a department with a team — what each stores, the second codes asked
/// first of all (D-7), the 404 of each level, <c>link_type_not_allowed</c> for a node that is not a department (OQ-8 A), the
/// idempotence (BR-AW-6) and the lock order (ADR-0031, ADR-0028 rule 8 reworked).
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
            Project = Store.AddProject(Organization).Project;
            Team = Store.AddTeam(Organization);
            Reports = Store.AddDepartment(Organization, "Отчёты");
            Node = Store.RootOf(Project);
            Sub = Store.AddArea(Node, "Квартальные");
            Areas = new FakeAreaRepository(Store);
            Access = new FakeOrganizationAccess(role);
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public Project Project { get; }

        public Team Team { get; }

        /// <summary>A department.</summary>
        public Area Reports { get; }

        /// <summary>The project's node.</summary>
        public Area Node { get; }

        /// <summary>A sub-area below the project's node.</summary>
        public Area Sub { get; }

        public FakeAreaRepository Areas { get; }

        public FakeOrganizationAccess Access { get; }

        public string Org => Organization.Id.ToString();

        public LinkTeamsToAreaHandler LinkTeamsToArea() => new(
            new FakeOrganizationReadModel(Store), new FakeTeamRepository(Store), Areas, new FakeAreaLinkRepository(Store), Access, _user, _clock, _unitOfWork);

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

        public UnlinkTeamFromAreaHandler UnlinkTeamFromArea() => new(
            new FakeOrganizationReadModel(Store), new FakeOrganizationStructure(Store), Areas, new FakeAreaLinkRepository(Store), _unitOfWork);

        public UnlinkAreaFromTeamHandler UnlinkAreaFromTeam() => new(
            new FakeOrganizationReadModel(Store), new FakeOrganizationStructure(Store), Areas, new FakeAreaLinkRepository(Store), _unitOfWork);
    }

    private static CancellationToken Ct => CancellationToken.None;

    // ---- the department side ------------------------------------------------------------------------------------------

    // US-4 · "Teams are linked to a department"; the department FOR SHARE, then the teams FOR SHARE, then one insert.
    [Fact]
    public async Task LinkTeamsToArea_StoresTheLinks_LocksTheAreaThenTheTeams_ASecondTimeChangesNothing()
    {
        var w = new World();
        var command = new LinkTeamsToAreaCommand(w.Org, w.Reports.Id.ToString(), [w.Team.Id.ToString()]);

        await w.LinkTeamsToArea().Handle(command, Ct);

        Assert.Equal(w.Team.Id, Assert.Single(w.Store.AreaTeams).TeamId);
        Assert.Equal([$"lock area share {w.Reports.Id}", $"lock teams share {w.Team.Id}", "link area teams 1"], w.Store.Calls);

        await w.LinkTeamsToArea().Handle(command, Ct);
        Assert.Single(w.Store.AreaTeams);
    }

    // OQ-8 A, OQ-11 A · a project's node and a sub-area take no team link: a project's teams are its own links (feat-028).
    [Fact]
    public async Task LinkTeamsToArea_ProjectNodeOrSubArea_IsLinkTypeNotAllowed_NothingStored()
    {
        var w = new World();

        foreach (var area in new[] { w.Node, w.Sub })
        {
            var refusal = await Assert.ThrowsAsync<ConflictException>(() =>
                w.LinkTeamsToArea().Handle(new LinkTeamsToAreaCommand(w.Org, area.Id.ToString(), [w.Team.Id.ToString()]), Ct));

            Assert.Equal(OrganizationErrorCodes.AreaLinkTypeNotAllowed, refusal.Code);
        }

        Assert.Empty(w.Store.AreaTeams);
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

    // BR-AW-15: the organization, then the area (404), whatever the earlier read saw (A-9: the lock statement's answer).
    [Fact]
    public async Task LinkTeamsToArea_Address_Answers404OutsideIn_AndAVanishedAreaIs404()
    {
        var w = new World();
        var handler = w.LinkTeamsToArea();

        var org = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new LinkTeamsToAreaCommand("abc", w.Reports.Id.ToString(), []), Ct));
        var area = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new LinkTeamsToAreaCommand(w.Org, Guid.CreateVersion7().ToString(), []), Ct));
        w.Areas.VanishedAfterTheLock.Add(w.Reports.Id);
        var vanished = await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new LinkTeamsToAreaCommand(w.Org, w.Reports.Id.ToString(), [w.Team.Id.ToString()]), Ct));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, org.Code);
        Assert.Equal(OrganizationErrorCodes.AreaNotFound, area.Code);
        Assert.Equal(OrganizationErrorCodes.AreaNotFound, vanished.Code);
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

    // ---- the team side ------------------------------------------------------------------------------------------------

    // The departments FOR SHARE, then the team FOR SHARE and re-checked active, then one insert.
    [Fact]
    public async Task LinkAreasToTeam_StoresTheLinks_LocksTheAreasThenTheTeam()
    {
        var w = new World();

        await w.LinkAreasToTeam().Handle(new LinkAreasToTeamCommand(w.Org, w.Team.Id.ToString(), [w.Reports.Id.ToString()]), Ct);

        Assert.Equal(w.Reports.Id, Assert.Single(w.Store.AreaTeams).AreaId);
        Assert.Equal([$"lock areas share {w.Reports.Id}", $"lock team share {w.Team.Id}", "link area teams 1"], w.Store.Calls);
    }

    // OQ-8 A · one node that is not a department refuses the whole request; nothing is stored.
    [Fact]
    public async Task LinkAreasToTeam_AnyNodeThatIsNotADepartment_IsLinkTypeNotAllowed_NothingStored()
    {
        var w = new World();

        var refusal = await Assert.ThrowsAsync<ConflictException>(() =>
            w.LinkAreasToTeam().Handle(
                new LinkAreasToTeamCommand(w.Org, w.Team.Id.ToString(), [w.Reports.Id.ToString(), w.Node.Id.ToString()]), Ct));

        Assert.Equal(OrganizationErrorCodes.AreaLinkTypeNotAllowed, refusal.Code);
        Assert.Empty(w.Store.AreaTeams);
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
