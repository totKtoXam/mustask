using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Tests.Organizations;

/// <summary>
/// mod-016 US-1 / US-3 / US-6 · the membership entities and the last-administrator rule, on the domain alone.
/// </summary>
public sealed class OrganizationMembershipTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = new(2026, 10, 12, 10, 30, 0, TimeSpan.Zero);

    // US-1 · "An administrator adds a member from «Участники»": who added it and when, and no role change recorded.
    [Fact]
    public void Add_RecordsTheAdderAndTheTime_AndNoRoleChange()
    {
        var organizationId = Guid.CreateVersion7();

        var membership = OrganizationMembership.Add(organizationId, "user-1", OrganizationRole.Member, "admin-1", Created);

        Assert.Equal(organizationId, membership.OrganizationId);
        Assert.Equal("user-1", membership.UserId);
        Assert.Equal(OrganizationRole.Member, membership.Role);
        Assert.Equal("admin-1", membership.AddedBy);
        Assert.Equal(Created, membership.AddedAt);
        Assert.Null(membership.RoleChangedBy);
        Assert.Null(membership.RoleChangedAt);
    }

    // US-6 / BR-M1-25 · the founding membership: the creator, as admin, added by themselves at the creation time.
    [Fact]
    public void Founder_IsAnAdministratorAddedByThemselves_WithNoRoleChange()
    {
        var organizationId = Guid.CreateVersion7();

        var founder = OrganizationMembership.Founder(organizationId, "creator-1", Created);

        Assert.Equal(OrganizationRole.Admin, founder.Role);
        Assert.True(founder.IsAdmin);
        Assert.Equal("creator-1", founder.UserId);
        Assert.Equal("creator-1", founder.AddedBy);
        Assert.Equal(Created, founder.AddedAt);
        Assert.Null(founder.RoleChangedBy);
        Assert.Null(founder.RoleChangedAt);
    }

    // US-1 · "An administrator changes a member's role from the row menu": the role and who/when of the change.
    [Fact]
    public void ChangeRole_ADifferentRole_RecordsWhoAndWhen()
    {
        var membership = OrganizationMembership.Add(Guid.CreateVersion7(), "user-1", OrganizationRole.Member, "admin-1", Created);

        var changed = membership.ChangeRole(OrganizationRole.Viewer, "admin-2", Later);

        Assert.True(changed);
        Assert.Equal(OrganizationRole.Viewer, membership.Role);
        Assert.Equal("admin-2", membership.RoleChangedBy);
        Assert.Equal(Later, membership.RoleChangedAt);
        Assert.Equal("admin-1", membership.AddedBy);
        Assert.Equal(Created, membership.AddedAt);
    }

    // api-contract § 1.5: setting the role the member already has changes nothing — not even the two «changed» fields.
    [Fact]
    public void ChangeRole_TheSameRole_ChangesNothing_NotEvenTheChangedFields()
    {
        var membership = OrganizationMembership.Add(Guid.CreateVersion7(), "user-1", OrganizationRole.Member, "admin-1", Created);

        var changed = membership.ChangeRole(OrganizationRole.Member, "admin-2", Later);

        Assert.False(changed);
        Assert.Equal(OrganizationRole.Member, membership.Role);
        Assert.Null(membership.RoleChangedBy);
        Assert.Null(membership.RoleChangedAt);
    }

    // The change record keeps the first change until a real one overwrites it.
    [Fact]
    public void ChangeRole_BackToTheOriginalRole_IsStillAChange()
    {
        var membership = OrganizationMembership.Add(Guid.CreateVersion7(), "user-1", OrganizationRole.Member, "admin-1", Created);
        membership.ChangeRole(OrganizationRole.Viewer, "admin-2", Later);

        var changed = membership.ChangeRole(OrganizationRole.Member, "admin-3", Later.AddHours(1));

        Assert.True(changed);
        Assert.Equal("admin-3", membership.RoleChangedBy);
        Assert.Equal(Later.AddHours(1), membership.RoleChangedAt);
    }

    // api-contract § 0.3: a user id is 1 to 255 characters and not blank; the longest is accepted, one more is a defect.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Add_ABlankUser_IsADefect(string userId)
    {
        Assert.Throws<ArgumentException>(() => OrganizationMembership.Add(Guid.CreateVersion7(), userId, OrganizationRole.Member, "admin-1", Created));
    }

    [Fact]
    public void Add_TheLongestUserId_IsAccepted_OneMoreIsADefect()
    {
        var longest = new string('u', OrganizationLimits.UserIdMaxLength);

        var membership = OrganizationMembership.Add(Guid.CreateVersion7(), longest, OrganizationRole.Viewer, "admin-1", Created);

        Assert.Equal(longest, membership.UserId);
        Assert.Throws<ArgumentException>(() =>
            OrganizationMembership.Add(Guid.CreateVersion7(), longest + "u", OrganizationRole.Viewer, "admin-1", Created));
    }

    [Fact]
    public void Add_ABlankAdderOrAnEmptyOrganization_IsADefect()
    {
        Assert.Throws<ArgumentException>(() => OrganizationMembership.Add(Guid.CreateVersion7(), "user-1", OrganizationRole.Member, " ", Created));
        Assert.Throws<ArgumentException>(() => OrganizationMembership.Add(Guid.Empty, "user-1", OrganizationRole.Member, "admin-1", Created));
    }

    [Fact]
    public void ChangeRole_ABlankActor_IsADefect()
    {
        var membership = OrganizationMembership.Add(Guid.CreateVersion7(), "user-1", OrganizationRole.Member, "admin-1", Created);

        Assert.Throws<ArgumentException>(() => membership.ChangeRole(OrganizationRole.Viewer, string.Empty, Later));
    }

    // US-3 · a team membership holds the team, its organization and the user, and nothing else (no role, no who/when).
    [Fact]
    public void TeamMembership_Add_HoldsTheTeamItsOrganizationAndTheUser()
    {
        var teamId = Guid.CreateVersion7();
        var organizationId = Guid.CreateVersion7();

        var membership = TeamMembership.Add(teamId, organizationId, "user-1");

        Assert.Equal((teamId, organizationId, "user-1"), (membership.TeamId, membership.OrganizationId, membership.UserId));
    }

    [Fact]
    public void TeamMembership_Add_BadArguments_AreDefects()
    {
        Assert.Throws<ArgumentException>(() => TeamMembership.Add(Guid.Empty, Guid.CreateVersion7(), "user-1"));
        Assert.Throws<ArgumentException>(() => TeamMembership.Add(Guid.CreateVersion7(), Guid.Empty, "user-1"));
        Assert.Throws<ArgumentException>(() => TeamMembership.Add(Guid.CreateVersion7(), Guid.CreateVersion7(), " "));
        Assert.Throws<ArgumentException>(() =>
            TeamMembership.Add(Guid.CreateVersion7(), Guid.CreateVersion7(), new string('u', OrganizationLimits.UserIdMaxLength + 1)));
    }

    // US-1 · BR-M1-4: no administrator left means the change is refused with the registry code; one left passes.
    [Fact]
    public void EnsureAnAdministratorRemains_None_IsRefusedWithLastAdmin()
    {
        var refusal = Assert.Throws<ConflictException>(() => OrganizationAdminRule.EnsureAnAdministratorRemains(0));

        Assert.Equal(OrganizationErrorCodes.MembershipLastAdmin, refusal.Code);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(500)]
    public void EnsureAnAdministratorRemains_OneOrMore_Passes(int left)
    {
        OrganizationAdminRule.EnsureAnAdministratorRemains(left);
    }

    [Fact]
    public void EnsureAnAdministratorRemains_ANegativeCount_IsADefect()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OrganizationAdminRule.EnsureAnAdministratorRemains(-1));
    }
}
