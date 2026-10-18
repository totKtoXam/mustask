using Mustask.Application.Features.Organizations.SearchTeamMemberCandidates;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.SearchTeamMemberCandidates;

/// <summary>
/// <c>SearchNewTeamMemberCandidates</c> on fakes (feat-028 api-contract § 2.3a; the human's gate-3 choice «Серверный поиск сейчас»): the
/// search of the creation form's «Участники» answers what a team's own search answers — the organization's active members, the same
/// rules — and asks <c>organizations.team.write</c> first of all (D-7).
/// </summary>
public sealed class SearchNewTeamMemberCandidatesHandlerTests
{
    private sealed class Fixture
    {
        public Fixture(OrganizationRole role = OrganizationRole.Admin)
        {
            Access = new FakeOrganizationAccess(role);
            Handler = new SearchNewTeamMemberCandidatesHandler(World.Members, World.Directory, Access);
        }

        public MembershipWorld World { get; } = new();

        public FakeOrganizationAccess Access { get; }

        public SearchNewTeamMemberCandidatesHandler Handler { get; }

        public Task<TeamMemberCandidatesResponse> SearchAsync(string? text, string? organizationId = null) =>
            Handler.Handle(new SearchNewTeamMemberCandidatesQuery(organizationId ?? World.OrganizationId, text), CancellationToken.None);
    }

    // US-1 · "Only active members of the organization are offered": a member is; a user of another organization, a projected user in
    // none, and a member whose account is disabled are not.
    [Fact]
    public async Task Handle_OffersActiveMembersOfThisOrganizationOnly()
    {
        var f = new Fixture();
        f.World.AddMember("u-member", "Кузнецов Участник", OrganizationRole.Viewer, email: "kuznetsov@example.org");
        f.World.AddMember("u-disabled", "Кузнецов Отключённый", isActive: false);
        f.World.AddOutsider("u-nobody", "Кузнецов Посторонний");
        var other = f.World.Store.AddOrganization("Другая организация");
        f.World.Directory.Add("u-other-org", "Кузнецов Чужой");
        f.World.Store.AddMember(other, "u-other-org");

        var result = await f.SearchAsync("кузнецов");

        Assert.Equal(["u-member"], result.Users.Select(user => user.Id));
        Assert.Equal("kuznetsov@example.org", result.Users.Single().Email);
    }

    // mod-016 § 1.3 · "found by name or e-mail", letter case not deciding, the text trimmed.
    [Fact]
    public async Task Handle_FindsByNameOrEmail_IgnoringLetterCase_AndTrimsTheText()
    {
        var f = new Fixture();
        f.World.AddMember("u-vera", "Вера Новикова", email: "Vera.Novikova@Example.org");

        var byName = await f.SearchAsync("  НОВИКОВА ");
        var byEmail = await f.SearchAsync("vera.novikova@");

        Assert.Equal(["u-vera"], byName.Users.Select(user => user.Id));
        Assert.Equal(["u-vera"], byEmail.Users.Select(user => user.Id));
    }

    // US-1 · "The search needs two characters": below two, and no match, answer 200 with nobody and no moreMatched.
    [Theory]
    [InlineData("в")]
    [InlineData(" в ")]
    [InlineData(null)]
    [InlineData("никого-такого-нет")]
    public async Task Handle_UnderTwoCharactersOrNoMatch_AnswersNobody(string? text)
    {
        var f = new Fixture();
        f.World.AddMember("u-vera", "Вера Короткая");

        var result = await f.SearchAsync(text);

        Assert.Empty(result.Users);
        Assert.False(result.MoreMatched);
    }

    // US-1 · "More than twenty matches are cut and said so" and "Past 500 ids … still answer": 501 members are 2 batches of the
    // restriction, a member is found among them, at most 20 come back in name order and `moreMatched` says the rest.
    [Fact]
    public async Task Handle_AnOrganizationOf501Members_FindsAMember_CutsAt20_AndSaysMoreMatched()
    {
        var f = new Fixture();

        for (var i = 0; i < 500; i++)
        {
            f.World.AddMember($"u-{i:D3}", $"Участник {i:D3}");
        }

        f.World.AddMember("u-needle", "Уникальнов Иголкин");
        f.World.Directory.SearchSizes.Clear();

        var found = await f.SearchAsync("иголкин");
        var many = await f.SearchAsync("участник");

        Assert.Equal(["u-needle"], found.Users.Select(user => user.Id));
        Assert.Equal(20, many.Users.Count);
        Assert.True(many.MoreMatched);
        Assert.Equal(Enumerable.Range(0, 20).Select(i => $"Участник {i:D3}"), many.Users.Select(user => user.DisplayName));
        Assert.All(f.World.Directory.SearchSizes, size => Assert.InRange(size, 1, 500));
    }

    // mod-016 § 0.5 · a text over 100 characters is mod-014's own 400, passed through.
    [Fact]
    public async Task Handle_TextOver100Characters_PassesMod014sValidationFailureThrough()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.SearchAsync(new string('я', 101)));

        Assert.Equal("user_directory.validation_failed", refusal.Code);
    }

    // D-7 · the second code is asked first of all: a role without `team.write` is refused before the address is read, and the
    // directory is never asked. Only «Администратор» holds it until mod-019.
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Handle_ARoleWithoutTeamWrite_IsRefusedFirstOfAll(OrganizationRole role)
    {
        var f = new Fixture(role);
        f.World.AddMember("u-member", "Кузнецов Участник");
        f.World.Directory.SearchSizes.Clear();

        var refusal = await Assert.ThrowsAsync<ForbiddenException>(() => f.SearchAsync("кузнецов", organizationId: "abc"));

        Assert.Equal(OrganizationErrorCodes.AccessForbidden, refusal.Code);
        Assert.Empty(f.World.Directory.SearchSizes);
    }

    // The organization's address, read as every level reads it (BR-M1-22).
    [Fact]
    public async Task Handle_AMalformedOrganization_IsOrganizationNotFound()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.SearchAsync("кузнецов", organizationId: "abc"));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }
}
