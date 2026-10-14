using Mustask.Application.Features.Organizations.SearchMemberCandidates;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.SearchMemberCandidates;

/// <summary>
/// <c>SearchMemberCandidates</c> on fakes (mod-016 plan-backend § 3.6 row 3): the «Добавление участника» search over the whole
/// projection — mod-014's rules pass through, an inactive user is never offered, an existing member is (status.md D-16 OP-6).
/// </summary>
public sealed class SearchMemberCandidatesHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Handler = new SearchMemberCandidatesHandler(World.Directory);
        }

        public MembershipWorld World { get; } = new();

        public SearchMemberCandidatesHandler Handler { get; }

        public Task<MemberCandidatesResponse> SearchAsync(string? text, string? organizationId = null) =>
            Handler.Handle(new SearchMemberCandidatesQuery(organizationId ?? World.OrganizationId, text), CancellationToken.None);
    }

    // US-1 · "An administrator adds a member from «Участники»": typing part of a name offers the matching active projected user,
    // with their e-mail; typing part of an e-mail finds the user by it.
    [Fact]
    public async Task Handle_TextOfTwoOrMoreCharacters_OffersActiveProjectedUsersByNameOrEmail()
    {
        var f = new Fixture();
        f.World.AddOutsider("u-1", "Иван Кузнецов", email: "ivan@example.org");
        f.World.AddOutsider("u-2", "Пётр Петров", email: "kuznec.petrov@example.org");
        f.World.AddOutsider("u-3", "Другой Человек");

        var byName = await f.SearchAsync("кузнец");
        var byEmail = await f.SearchAsync("kuznec");

        Assert.Equal(["u-1"], byName.Users.Select(user => user.Id));
        Assert.Equal(new MemberCandidateResponse("u-1", "Иван Кузнецов", "ivan@example.org"), byName.Users[0]);
        Assert.False(byName.MoreMatched);
        Assert.Equal(["u-2"], byEmail.Users.Select(user => user.Id));
    }

    // US-1 · "The search needs two characters": one character answers nobody.
    [Theory]
    [InlineData("")]
    [InlineData("с")]
    [InlineData(" с ")]
    [InlineData(null)]
    public async Task Handle_UnderTwoCharacters_AnswersNobody(string? text)
    {
        var f = new Fixture();
        f.World.AddOutsider("u-1", "Иван Кузнецов");

        var result = await f.SearchAsync(text);

        Assert.Empty(result.Users);
        Assert.False(result.MoreMatched);
    }

    // US-1 · "More than twenty matches are cut and said so": exactly 20 in order and the flag.
    [Fact]
    public async Task Handle_MoreThanTwentyMatches_AnswersTwentyInOrder_AndMoreMatched()
    {
        var f = new Fixture();

        for (var i = 0; i < 25; i++)
        {
            f.World.AddOutsider($"u-{i:D2}", $"Иванов {i:D2}");
        }

        var result = await f.SearchAsync("иванов");

        Assert.Equal(20, result.Users.Count);
        Assert.True(result.MoreMatched);
        Assert.Equal(Enumerable.Range(0, 20).Select(i => $"u-{i:D2}"), result.Users.Select(user => user.Id));
    }

    // US-1 · "An inactive or unknown user is neither offered nor added" (BR-M1-32): a disabled account is not offered.
    [Fact]
    public async Task Handle_InactiveUser_IsNotOffered()
    {
        var f = new Fixture();
        f.World.AddOutsider("u-disabled", "Отключённый Кузнецов", isActive: false);
        f.World.AddOutsider("u-active", "Активный Кузнецов");

        var result = await f.SearchAsync("кузнецов");

        Assert.Equal(["u-active"], result.Users.Select(user => user.Id));
    }

    // api-contract § 1.3: an existing member is offered (and refused on add), as doc-006 US-3 has it.
    [Fact]
    public async Task Handle_AnExistingMember_IsStillOffered()
    {
        var f = new Fixture();
        f.World.AddMember("u-member", "Участник Кузнецов", OrganizationRole.Viewer);

        var result = await f.SearchAsync("кузнецов");

        Assert.Equal(["u-member"], result.Users.Select(user => user.Id));
    }

    // api-contract § 0.5: a text over 100 characters is mod-014's own 400, passed through unchanged.
    [Fact]
    public async Task Handle_TextOver100Characters_PassesMod014sValidationFailureThrough()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.SearchAsync(new string('я', 101)));

        Assert.Equal("user_directory.validation_failed", refusal.Code);
        Assert.Equal(["text"], refusal.Errors.Keys);
    }

    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.SearchAsync("кузнец", organizationId: "abc"));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }
}
