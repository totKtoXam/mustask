using Mustask.Domain.Organizations;
using static Mustask.Domain.Tests.Organizations.OrganizationsDomainTestData;

namespace Mustask.Domain.Tests.Organizations;

/// <summary>feat-028 BR-OL-4: a link between a project and a team of one organization.</summary>
public sealed class ProjectTeamTests
{
    // US-3 · "a link from either page": it records the caller and the time.
    [Fact]
    public void Link_RecordsTheEndsTheCallerAndTheTime()
    {
        var (organizationId, projectId, teamId) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        var link = ProjectTeam.Link(organizationId, projectId, teamId, Creator, Now);

        Assert.Equal((organizationId, projectId, teamId), (link.OrganizationId, link.ProjectId, link.TeamId));
        Assert.Equal(Creator, link.LinkedBy);
        Assert.Equal(Now, link.LinkedAt);
    }

    [Fact]
    public void Link_AnEmptyId_IsADefect()
    {
        Assert.Throws<ArgumentException>(() => ProjectTeam.Link(Guid.Empty, Guid.CreateVersion7(), Guid.CreateVersion7(), Creator, Now));
        Assert.Throws<ArgumentException>(() => ProjectTeam.Link(Guid.CreateVersion7(), Guid.Empty, Guid.CreateVersion7(), Creator, Now));
        Assert.Throws<ArgumentException>(() => ProjectTeam.Link(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.Empty, Creator, Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Link_ABlankAuthor_IsADefect(string linkedBy)
    {
        Assert.Throws<ArgumentException>(
            () => ProjectTeam.Link(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), linkedBy, Now));
    }

    [Fact]
    public void Link_AnAuthorOver255Characters_IsADefect()
    {
        Assert.Throws<ArgumentException>(
            () => ProjectTeam.Link(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), new string('u', 256), Now));
    }
}
