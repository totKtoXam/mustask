using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Tests.Organizations;

/// <summary>The rule for a team named in a write (mod-017 api-contract § 0.2), every row of its table.</summary>
public sealed class ProjectTeamsTests
{
    private static readonly Guid OrganizationId = Guid.CreateVersion7();
    private static readonly Guid ProjectId = Guid.CreateVersion7();

    private static TeamFacts Team(Guid? organizationId = null, Guid? projectId = null, TeamStatus status = TeamStatus.Active, bool reachable = true) =>
        new(Guid.CreateVersion7(), organizationId ?? OrganizationId, projectId ?? ProjectId, status, reachable);

    // US-1 · "a team the project does not have": an active team of the project is accepted.
    [Fact]
    public void EnsureBindable_ActiveTeamOfTheProject_IsAccepted()
    {
        ProjectTeams.EnsureBindable(Team(), OrganizationId, ProjectId);
    }

    // US-1 · "a team the project does not have", row 1: no such team.
    [Fact]
    public void EnsureBindable_NoSuchTeam_IsNotFound()
    {
        var refusal = Assert.Throws<NotFoundException>(() => ProjectTeams.EnsureBindable(null, OrganizationId, ProjectId));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }

    // BR-OT-16 · another organization's team answers as an id that names nothing.
    [Fact]
    public void EnsureBindable_TeamOfAnotherOrganization_IsNotFound_EvenOfTheSameProjectId()
    {
        var refusal = Assert.Throws<NotFoundException>(() =>
            ProjectTeams.EnsureBindable(Team(organizationId: Guid.CreateVersion7()), OrganizationId, ProjectId));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }

    // US-1 · "a team the project does not have", row 2: a deleted team.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnsureBindable_DeletedTeam_IsNotFound_WhateverTheProject(bool ofTheProject)
    {
        var team = Team(projectId: ofTheProject ? ProjectId : Guid.CreateVersion7(), status: TeamStatus.Deleted, reachable: false);

        var refusal = Assert.Throws<NotFoundException>(() => ProjectTeams.EnsureBindable(team, OrganizationId, ProjectId));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }

    // ADR-0026 rule 5 · a team of a deactivated project is not found, never revealed as such.
    [Fact]
    public void EnsureBindable_ActiveTeamOfADeactivatedProject_IsNotFound()
    {
        var team = Team(projectId: Guid.CreateVersion7(), reachable: false);

        var refusal = Assert.Throws<NotFoundException>(() => ProjectTeams.EnsureBindable(team, OrganizationId, ProjectId));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }

    // US-1 · "a team the project does not have", row 3: an active team of another active project.
    [Fact]
    public void EnsureBindable_ActiveTeamOfAnotherActiveProject_IsNotInProject()
    {
        var refusal = Assert.Throws<ConflictException>(() =>
            ProjectTeams.EnsureBindable(Team(projectId: Guid.CreateVersion7()), OrganizationId, ProjectId));

        Assert.Equal(OrganizationErrorCodes.TeamNotInProject, refusal.Code);
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
