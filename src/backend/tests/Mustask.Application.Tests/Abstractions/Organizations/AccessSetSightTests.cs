using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Tests.Abstractions.Organizations;

// feat-041 · doc-016 BR-TD-12..-14: the set form's sight, on one department «Д» holding project P (node N) and team T linked to P.
public sealed class AccessSetSightTests
{
    private static readonly Guid Department = Guid.CreateVersion7();
    private static readonly Guid Node = Guid.CreateVersion7();
    private static readonly Guid Project = Guid.CreateVersion7();
    private static readonly Guid Team = Guid.CreateVersion7();
    private static readonly Guid OtherDepartment = Guid.CreateVersion7();
    private static readonly Guid OtherTeam = Guid.CreateVersion7();

    private static readonly IReadOnlyDictionary<Guid, IReadOnlyList<GrantRole>> NoGrants = new Dictionary<Guid, IReadOnlyList<GrantRole>>();

    private static IReadOnlyDictionary<Guid, IReadOnlyList<GrantRole>> Grant(Guid id) =>
        new Dictionary<Guid, IReadOnlyList<GrantRole>> { [id] = [GrantRole.Reader] };

    private static AccessSet Set(
        OrganizationRole role = OrganizationRole.Member,
        bool flag = false,
        bool inherits = true,
        IReadOnlyDictionary<Guid, IReadOnlyList<GrantRole>>? areaGrants = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<GrantRole>>? projectGrants = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<GrantRole>>? teamGrants = null,
        Guid[]? memberOf = null) =>
        new(
            role,
            [
                new AccessNode(Department, AreaType.Department, null, null, true),
                new AccessNode(OtherDepartment, AreaType.Department, null, null, true),
                new AccessNode(Node, AreaType.Project, Department, Project, inherits),
            ],
            areaGrants ?? NoGrants,
            projectGrants ?? NoGrants,
            teamGrants ?? NoGrants,
            new SightData(
                new Dictionary<Guid, bool> { [Project] = flag },
                new Dictionary<Guid, Guid?> { [Team] = Department, [OtherTeam] = OtherDepartment },
                new Dictionary<Guid, IReadOnlyList<Guid>> { [Project] = [Team] },
                (memberOf ?? []).ToHashSet()));

    [Fact]
    public void NothingReaches_NothingIsSeen_AndAnUnknownObjectIsNotSeenEither()
    {
        var set = Set();

        Assert.False(set.SeesProject(Project));
        Assert.False(set.SeesTeam(Team));
        Assert.False(set.SeesArea(Department));
        Assert.False(set.SeesArea(Node));
        Assert.False(set.SeesProject(Guid.CreateVersion7()));
        Assert.False(set.SeesTeam(Guid.CreateVersion7()));
        Assert.False(set.SeesArea(Guid.CreateVersion7()));
    }

    [Fact]
    public void TheFlag_ShowsTheProjectItsNodeItsLinkedTeamAndItsDepartment_ButNotAnotherTeam()
    {
        var set = Set(flag: true);

        Assert.True(set.SeesProject(Project));
        Assert.True(set.SeesArea(Node));
        Assert.True(set.SeesTeam(Team));
        Assert.True(set.SeesArea(Department));
        Assert.False(set.SeesTeam(OtherTeam));
        Assert.False(set.SeesArea(OtherDepartment));
    }

    [Fact]
    public void MembershipOfALinkedTeam_ShowsTheProjectTheTeamAndTheDepartment()
    {
        var set = Set(memberOf: [Team]);

        Assert.True(set.SeesProject(Project));
        Assert.True(set.SeesTeam(Team));
        Assert.True(set.SeesArea(Department));
        Assert.False(set.SeesTeam(OtherTeam));
    }

    [Fact]
    public void ADepartmentGrant_ReachesTheProjectThroughItsNode_UntilTheSwitchIsOff_AndShowsTheTeamsOfTheDepartment()
    {
        var on = Set(areaGrants: Grant(Department));
        var off = Set(areaGrants: Grant(Department), inherits: false);

        Assert.True(on.SeesProject(Project));
        Assert.True(on.SeesTeam(Team));
        Assert.False(off.SeesProject(Project));
        Assert.False(off.SeesArea(Node));
        Assert.True(off.SeesTeam(Team));
        Assert.True(off.SeesArea(Department));
        Assert.False(off.SeesTeam(OtherTeam));
    }

    [Fact]
    public void AProjectGrant_ShowsTheProjectItsTeamAndItsDepartment_AndATeamGrantShowsTheTeamAlone()
    {
        var byProject = Set(projectGrants: Grant(Project));
        var byTeam = Set(teamGrants: Grant(OtherTeam));

        Assert.True(byProject.SeesProject(Project));
        Assert.True(byProject.SeesTeam(Team));
        Assert.True(byProject.SeesArea(Department));
        Assert.True(byTeam.SeesTeam(OtherTeam));
        Assert.True(byTeam.SeesArea(OtherDepartment));
        Assert.False(byTeam.SeesProject(Project));
    }

    [Fact]
    public void TheAdministrator_SeesEverythingWithNoData()
    {
        var set = new AccessSet(OrganizationRole.Admin, [], NoGrants, NoGrants, NoGrants);

        Assert.True(set.SeesProject(Project));
        Assert.True(set.SeesTeam(Team));
        Assert.True(set.SeesArea(Department));
    }

    // feat-062 · BR-TD-17: the projects a multi-project read narrows by are the set form's own SeesProject over the organization's projects.
    [Fact]
    public void SeenProjects_AreExactlyTheProjectsSeesProjectAnswersTrueFor()
    {
        Assert.Empty(Set().SeenProjects().Ids!);
        Assert.Equal([Project], Set(flag: true).SeenProjects().Ids!);
        Assert.Equal([Project], Set(memberOf: [Team]).SeenProjects().Ids!);
        Assert.Equal([Project], Set(projectGrants: Grant(Project)).SeenProjects().Ids!);
        Assert.Empty(Set(teamGrants: Grant(OtherTeam)).SeenProjects().Ids!);
    }

    [Fact]
    public void SeenProjects_OfTheAdministrator_IsAll_AndAHiddenProjectIsNotSeenByAMember()
    {
        var administrator = new AccessSet(OrganizationRole.Admin, [], NoGrants, NoGrants, NoGrants).SeenProjects();

        Assert.True(administrator.IsAll);
        Assert.True(administrator.Sees(Guid.NewGuid()));
        Assert.False(Set().SeenProjects().Sees(Project));
        Assert.True(Set(flag: true).SeenProjects().Sees(Project));
    }

    // feat-062 · a read of several projects' rows fails closed: a non-administrator's CallerAccess without its sight is a defect.
    [Fact]
    public void CallerAccess_OfANonAdministrator_WithNoSight_Throws_AndOfTheAdministratorIsAll()
    {
        var member = new CallerAccess(Guid.NewGuid(), "u", OrganizationRole.Member);
        var administrator = new CallerAccess(Guid.NewGuid(), "u", OrganizationRole.Admin);
        var withSight = new CallerAccess(Guid.NewGuid(), "u", OrganizationRole.Member, VisibleProjects.Only([Project]));

        Assert.Throws<InvalidOperationException>(() => member.Projects);
        Assert.True(administrator.Projects.IsAll);
        Assert.True(withSight.Projects.Sees(Project));
        Assert.False(withSight.Projects.Sees(Guid.NewGuid()));
    }
}
