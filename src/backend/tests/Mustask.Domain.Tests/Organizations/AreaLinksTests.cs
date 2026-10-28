using Mustask.Domain.Organizations;

namespace Mustask.Domain.Tests.Organizations;

public sealed class AreaLinksTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    // US-4 · a link is the pair, who made it and when; it carries nothing else (BR-AW-6). Only departments are linked (OQ-8 A).
    [Fact]
    public void Link_KeepsThePairWhoAndWhen()
    {
        var organization = Guid.CreateVersion7();
        var area = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();

        var team = AreaTeam.Link(organization, area, other, "admin-sub", At);

        Assert.Equal((organization, area, other, "admin-sub", At), (team.OrganizationId, team.AreaId, team.TeamId, team.LinkedBy, team.LinkedAt));
    }

    // A zero id or a missing author is a defect of the caller, never a user answer.
    [Fact]
    public void Link_ZeroIdOrNoAuthor_IsADefect()
    {
        var id = Guid.CreateVersion7();

        Assert.Throws<ArgumentException>(() => AreaTeam.Link(Guid.Empty, id, id, "sub", At));
        Assert.Throws<ArgumentException>(() => AreaTeam.Link(id, Guid.Empty, id, "sub", At));
        Assert.Throws<ArgumentException>(() => AreaTeam.Link(id, id, Guid.Empty, "sub", At));
        Assert.Throws<ArgumentException>(() => AreaTeam.Link(id, id, id, " ", At));
        Assert.Throws<ArgumentException>(() => AreaTeam.Link(id, id, id, new string('x', OrganizationLimits.LinkedByMaxLength + 1), At));
    }
}
