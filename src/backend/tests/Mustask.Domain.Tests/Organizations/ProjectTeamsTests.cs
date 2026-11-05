using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Tests.Organizations;

/// <summary>The rule for a team named in a write (feat-028 api-contract § 0.3, § 2.4), every row of its table.</summary>
public sealed class ProjectTeamsTests
{
    private static readonly Guid OrganizationId = Guid.CreateVersion7();

    private static TeamFacts Team(Guid? organizationId = null, TeamStatus status = TeamStatus.Active, bool linked = true) =>
        new(Guid.CreateVersion7(), organizationId ?? OrganizationId, status, linked);

    // US-3 · "an owning team must be linked": an active team linked to the project is accepted.
    [Fact]
    public void EnsureBindable_ActiveTeamLinkedToTheProject_IsAccepted()
    {
        ProjectTeams.EnsureBindable(Team(), OrganizationId);
    }

    // US-3 · "an owning team must be linked", row 1: no such team.
    [Fact]
    public void EnsureBindable_NoSuchTeam_IsNotFound()
    {
        var refusal = Assert.Throws<NotFoundException>(() => ProjectTeams.EnsureBindable(null, OrganizationId));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }

    // US-8 · a team of another organization answers as an id that names nothing.
    [Fact]
    public void EnsureBindable_TeamOfAnotherOrganization_IsNotFound()
    {
        var refusal = Assert.Throws<NotFoundException>(() =>
            ProjectTeams.EnsureBindable(Team(organizationId: Guid.CreateVersion7()), OrganizationId));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }

    // US-3 · "an owning team must be linked", row 2: a deleted team, linked or not (a deletion removes the links, so this only
    // guards a stale read).
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnsureBindable_DeletedTeam_IsNotFound_LinkedOrNot(bool linked)
    {
        var refusal = Assert.Throws<NotFoundException>(() =>
            ProjectTeams.EnsureBindable(Team(status: TeamStatus.Deleted, linked: linked), OrganizationId));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }

    // US-3 · "an owning team must be linked", row 3: an active team of the organization that is not linked to the project
    // (renamed from ...ActiveTeamOfAnotherActiveProject_IsNotInProject, D-16).
    [Fact]
    public void EnsureBindable_ActiveTeamNotLinkedToTheProject_IsNotInProject()
    {
        var refusal = Assert.Throws<ConflictException>(() => ProjectTeams.EnsureBindable(Team(linked: false), OrganizationId));

        Assert.Equal(OrganizationErrorCodes.TeamNotInProject, refusal.Code);
    }

    // US-3 · "only active ends are linked": a team to link is an active team of the organization, linked or not.
    [Fact]
    public void EnsureLinkable_ActiveTeamOfTheOrganization_IsAccepted_LinkedOrNot()
    {
        ProjectTeams.EnsureLinkable(Team(linked: true), OrganizationId);
        ProjectTeams.EnsureLinkable(Team(linked: false), OrganizationId);
    }

    [Fact]
    public void EnsureLinkable_NoSuchTeam_ForeignTeam_DeletedTeam_AreNotFound()
    {
        Assert.All(
            new TeamFacts?[] { null, Team(organizationId: Guid.CreateVersion7()), Team(status: TeamStatus.Deleted) },
            team =>
            {
                var refusal = Assert.Throws<NotFoundException>(() => ProjectTeams.EnsureLinkable(team, OrganizationId));

                Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
            });
    }

    // US-4 · "a bad destination": another team linked to the project is accepted.
    [Fact]
    public void EnsureDestination_AnotherLinkedActiveTeam_IsAccepted()
    {
        ProjectTeams.EnsureDestination(Team(), Guid.CreateVersion7());
    }

    // US-4 · "a bad destination": unknown (null covers a foreign team too) and deleted answer not found.
    [Fact]
    public void EnsureDestination_UnknownOrDeleted_IsNotFound()
    {
        Assert.All(
            new TeamFacts?[] { null, Team(status: TeamStatus.Deleted) },
            destination =>
            {
                var refusal = Assert.Throws<NotFoundException>(() => ProjectTeams.EnsureDestination(destination, Guid.CreateVersion7()));

                Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
            });
    }

    // US-4 · "a bad destination": the unlinked team itself, or a team not linked to the project, is not in the project.
    [Fact]
    public void EnsureDestination_TheUnlinkedTeamItself_OrATeamNotLinked_IsNotInProject()
    {
        var unlinked = Team();

        var itself = Assert.Throws<ConflictException>(() => ProjectTeams.EnsureDestination(unlinked, unlinked.Id));
        var notLinked = Assert.Throws<ConflictException>(() => ProjectTeams.EnsureDestination(Team(linked: false), unlinked.Id));

        Assert.Equal(OrganizationErrorCodes.TeamNotInProject, itself.Code);
        Assert.Equal(OrganizationErrorCodes.TeamNotInProject, notLinked.Code);
    }

    // BR-OT-3 · a child takes its parent's team, else the project default, else none.
    [Fact]
    public void ChildTeam_ParentsTeamWins_ThenTheDefault_ThenNone()
    {
        var parentTeam = Guid.CreateVersion7();
        var defaultTeam = Guid.CreateVersion7();

        Assert.Equal(parentTeam, ProjectTeams.ChildTeam(parentTeam, defaultTeam));
        Assert.Equal(parentTeam, ProjectTeams.ChildTeam(parentTeam, null));
        Assert.Equal(defaultTeam, ProjectTeams.ChildTeam(null, defaultTeam));
        Assert.Null(ProjectTeams.ChildTeam(null, null));
    }
}
