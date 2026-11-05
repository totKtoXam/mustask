using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations.GetMembers;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.GetMembers;

/// <summary>
/// <c>GetMembers</c> on fakes (mod-016 plan-backend § 3.6 row 2, § 3.7): the order of api-contract § 0.5, the people named, the
/// state of an inactive or an unknown member, and the batching of the projection lookups.
/// </summary>
public sealed class GetMembersHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Handler = new GetMembersHandler(World.Members);
        }

        public MembershipWorld World { get; } = new();

        public GetMembersHandler Handler { get; }

        public async Task<IReadOnlyList<MemberView>> ListAsync() =>
            (await Handler.Handle(new GetMembersQuery(World.OrganizationId), CancellationToken.None)).Items;
    }

    // api-contract § 0.5: members are ordered by display name (the ru collation at primary strength), then by user id.
    [Fact]
    public async Task Handle_OrdersMembersByDisplayName_RuCollation_CaseInsensitive_EAndYoTogether()
    {
        var f = new Fixture();
        f.World.AddMember("u-3", "Ёлкин Пётр");
        f.World.AddMember("u-1", "Елизаров Иван");
        f.World.AddMember("u-2", "ёлочкин Олег");
        f.World.AddMember("u-4", "Яковлев Яков");
        f.World.AddMember("u-5", "Борисов Борис");

        var names = (await f.ListAsync()).Select(member => member.User.DisplayName);

        // «ё» equals «е» and letter case is ignored (BR-UP-15): «Елизаров» < «Ёлкин» < «ёлочкин» by their third letters и, к, о.
        Assert.Equal(["Анна Смирнова", "Борисов Борис", "Елизаров Иван", "Ёлкин Пётр", "ёлочкин Олег", "Яковлев Яков"], names);
    }

    // api-contract § 0.5: the same name is ordered by user id in code-point order.
    [Fact]
    public async Task Handle_SameDisplayName_IsOrderedByUserIdByCodePoint()
    {
        var f = new Fixture();
        f.World.AddMember("b-id", "Иван Иванов");
        f.World.AddMember("a-id", "Иван Иванов");
        f.World.AddMember("B-id", "Иван Иванов");

        var ids = (await f.ListAsync()).Where(member => member.User.DisplayName == "Иван Иванов").Select(member => member.User.Id);

        Assert.Equal(["B-id", "a-id", "b-id"], ids);
    }

    // US-1 · "An inactive or unknown member is shown as such": inactive after the name's turn, unknown last, by id, with no name.
    [Fact]
    public async Task Handle_InactiveMemberStaysListedAsInactive_UnknownIsLastByIdWithNoNameAndNoEmail()
    {
        var f = new Fixture();
        f.World.AddMember("u-disabled", "Олег Иванов", isActive: false, email: "oleg@example.org");
        f.World.AddUnknownMember("z-ghost");
        f.World.AddUnknownMember("a-ghost");
        f.World.AddMember("u-vera", "Вера Новикова", email: "");

        var members = await f.ListAsync();

        Assert.Equal(
            ["Анна Смирнова", "Вера Новикова", "Олег Иванов", null, null],
            members.Select(member => member.User.DisplayName));
        Assert.Equal(["a-ghost", "z-ghost"], members.Skip(3).Select(member => member.User.Id));
        Assert.Equal(UserRefStates.Inactive, members.Single(member => member.User.Id == "u-disabled").User.State);
        Assert.Equal("oleg@example.org", members.Single(member => member.User.Id == "u-disabled").User.Email);

        var ghost = members.Single(member => member.User.Id == "a-ghost");
        Assert.Equal((UserRefStates.Unknown, null, null), (ghost.User.State, ghost.User.DisplayName, ghost.User.Email));
        Assert.Equal("", members.Single(member => member.User.Id == "u-vera").User.Email);
    }

    // api-contract § 1.2: «Добавлен» and «Роль изменена» name people too — also a person who is no longer a member.
    [Fact]
    public async Task Handle_NamesTheAdderAndTheRoleChanger_AlsoWhenTheyAreNoLongerMembers()
    {
        var f = new Fixture();
        f.World.Directory.Add("former-admin", "Бывший Админ");
        var membership = f.World.Store.AddMember(f.World.Organization, "u-new", OrganizationRole.Member);
        f.World.Directory.Add("u-new", "Новый Участник");
        f.World.Store.Memberships.Remove(membership);
        membership = OrganizationMembership.Add(f.World.Organization.Id, "u-new", OrganizationRole.Member, "former-admin", InMemoryOrganizations.Now);
        membership.ChangeRole(OrganizationRole.Viewer, "former-admin", InMemoryOrganizations.Now.AddHours(1));
        f.World.Store.Memberships.Add(membership);

        var member = (await f.ListAsync()).Single(candidate => candidate.User.Id == "u-new");

        Assert.Equal("Бывший Админ", member.AddedBy.DisplayName);
        Assert.Equal("Бывший Админ", member.RoleChangedBy!.DisplayName);
        Assert.Equal(InMemoryOrganizations.Now.AddHours(1), member.RoleChangedAt);
        Assert.Equal("viewer", member.Role);
    }

    // The role changer or adder the projection does not know is shown unknown, not dropped.
    [Fact]
    public async Task Handle_AnAdderTheProjectionDoesNotKnow_IsShownUnknown()
    {
        var f = new Fixture();

        var admin = (await f.ListAsync()).Single(member => member.User.Id == MembershipWorld.AdminId);

        Assert.Equal(UserRefStates.Unknown, admin.AddedBy.State);
        Assert.Equal(InMemoryOrganizations.Creator, admin.AddedBy.Id);
        Assert.Null(admin.RoleChangedBy);
        Assert.Null(admin.RoleChangedAt);
    }

    // US-1 · "Past 500 ids the list ... still answer": 501 members with adders who are no longer members — all listed, each with
    // a name; the projection is asked in batches of at most 500 (BR-UP-17), never refused.
    [Fact]
    public async Task Handle_501Members_ListsAll_LooksTheProjectionUpInBatchesOfAtMost500()
    {
        var f = new Fixture();

        for (var i = 0; i < 500; i++)
        {
            f.World.AddMember($"u-{i:D3}", $"Участник {i:D3}");
        }

        f.World.Directory.LookupSizes.Clear();

        var members = await f.ListAsync();

        Assert.Equal(501, members.Count);
        Assert.Equal(501, members.Select(member => member.User.Id).Distinct().Count());
        Assert.All(members.Where(member => member.User.Id != MembershipWorld.AdminId), member => Assert.NotNull(member.User.DisplayName));
        Assert.All(f.World.Directory.LookupSizes, size => Assert.InRange(size, 1, 500));
        Assert.True(f.World.Directory.LookupSizes.Count >= 2, "501 members and the adders need more than one batch.");
    }

    // The list is the organization's alone.
    [Fact]
    public async Task Handle_ListsOnlyTheOrganizationsOwnMembers()
    {
        var f = new Fixture();
        var other = f.World.Store.AddOrganization("Другая организация");
        f.World.Store.AddMember(other, "u-outsider");

        var members = await f.ListAsync();

        Assert.DoesNotContain(members, member => member.User.Id == "u-outsider");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound(string? organizationId)
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.Handler.Handle(new GetMembersQuery(organizationId), CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }
}
