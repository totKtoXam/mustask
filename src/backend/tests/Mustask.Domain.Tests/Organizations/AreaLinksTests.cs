using Mustask.Domain.Organizations;

namespace Mustask.Domain.Tests.Organizations;

public sealed class AreaLinksTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    // US-3 / US-4 · a link is the pair, who made it and when; it carries nothing else (BR-AW-6, BR-AW-8).
    [Fact]
    public void Link_KeepsThePairWhoAndWhen_ForAProjectAndForATeam()
    {
        var organization = Guid.CreateVersion7();
        var area = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();

        var project = AreaProject.Link(organization, area, other, "admin-sub", At);
        var team = AreaTeam.Link(organization, area, other, "admin-sub", At);

        Assert.Equal((organization, area, other, "admin-sub", At), (project.OrganizationId, project.AreaId, project.ProjectId, project.LinkedBy, project.LinkedAt));
        Assert.Equal((organization, area, other, "admin-sub", At), (team.OrganizationId, team.AreaId, team.TeamId, team.LinkedBy, team.LinkedAt));
    }

    // A zero id or a missing author is a defect of the caller, never a user answer.
    [Fact]
    public void Link_ZeroIdOrNoAuthor_IsADefect()
    {
        var id = Guid.CreateVersion7();

        Assert.Throws<ArgumentException>(() => AreaProject.Link(Guid.Empty, id, id, "sub", At));
        Assert.Throws<ArgumentException>(() => AreaProject.Link(id, Guid.Empty, id, "sub", At));
        Assert.Throws<ArgumentException>(() => AreaProject.Link(id, id, Guid.Empty, "sub", At));
        Assert.Throws<ArgumentException>(() => AreaTeam.Link(id, id, id, " ", At));
        Assert.Throws<ArgumentException>(() => AreaTeam.Link(id, id, id, new string('x', OrganizationLimits.LinkedByMaxLength + 1), At));
    }
}
