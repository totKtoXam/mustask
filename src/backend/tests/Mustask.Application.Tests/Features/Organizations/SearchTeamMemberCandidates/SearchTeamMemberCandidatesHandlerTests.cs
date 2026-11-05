using Mustask.Application.Features.Organizations.SearchTeamMemberCandidates;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.SearchTeamMemberCandidates;

/// <summary>
/// <c>SearchTeamMemberCandidates</c> on fakes (mod-016 plan-backend § 3.6 row 8): the «Участник организации» search offers only
/// active members of this organization, finds a member among 501 by batching the restriction, and keeps mod-014's rules.
/// </summary>
public sealed class SearchTeamMemberCandidatesHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Handler = new SearchTeamMemberCandidatesHandler(World.TeamReads, World.Members, World.Directory);
        }

        public MembershipWorld World { get; } = new();

        public SearchTeamMemberCandidatesHandler Handler { get; }

        public Task<TeamMemberCandidatesResponse> SearchAsync(string? text, string? teamId = null) =>
            Handler.Handle(
                new SearchTeamMemberCandidatesQuery(World.OrganizationId, teamId ?? World.PlatformId, text),
                CancellationToken.None);
    }

    // US-3 · "Only active organization members are offered and accepted": a user of another organization, a projected user in
    // none, and a member whose account is disabled are not offered; a member is.
    [Fact]
    public async Task Handle_OffersActiveMembersOfThisOrganizationOnly()
    {
        var f = new Fixture();
        f.World.AddMember("u-member", "Кузнецов Участник", OrganizationRole.Viewer);
        f.World.AddMember("u-disabled", "Кузнецов Отключённый", isActive: false);
        f.World.AddOutsider("u-nobody", "Кузнецов Посторонний");
        var other = f.World.Store.AddOrganization("Другая организация");
        f.World.Directory.Add("u-other-org", "Кузнецов Чужой");
        f.World.Store.AddMember(other, "u-other-org");

        var result = await f.SearchAsync("кузнецов");

        Assert.Equal(["u-member"], result.Users.Select(user => user.Id));
    }

    // US-3 · "...a search with no candidate shows «Пользователи не найдены»": nobody matches, an empty answer.
    [Fact]
    public async Task Handle_NoCandidate_AnswersAnEmptyList()
    {
        var f = new Fixture();
        f.World.AddOutsider("u-nobody", "Кузнецов Посторонний");

        var result = await f.SearchAsync("кузнецов");

        Assert.Empty(result.Users);
        Assert.False(result.MoreMatched);
    }

    // api-contract § 2.2: a user already in the team is offered (and refused on add).
    [Fact]
    public async Task Handle_AUserAlreadyInTheTeam_IsStillOffered()
    {
        var f = new Fixture();
        f.World.AddMember("u-member", "Кузнецов Участник");
        f.World.Store.AddTeamMember(f.World.Organization, f.World.Platform, "u-member");

        var result = await f.SearchAsync("кузнецов");

        Assert.Equal(["u-member"], result.Users.Select(user => user.Id));
    }

    // US-1 · "Past 500 ids ... still answer": a member is found among 501, by splitting the restriction into batches of 500 and
    // merging; at most 20, in name order, and a text under two characters answers nobody.
    [Fact]
    public async Task Handle_FindsAMemberAmong501_BatchesTheRestriction_NeverRefuses()
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
        var tooShort = await f.SearchAsync("у");

        Assert.Equal(["u-needle"], found.Users.Select(user => user.Id));
        Assert.Equal(20, many.Users.Count);
        Assert.True(many.MoreMatched);
        Assert.Empty(tooShort.Users);
        Assert.All(f.World.Directory.SearchSizes, size => Assert.InRange(size, 1, 500));
        Assert.Equal(6, f.World.Directory.SearchSizes.Count);
    }

    // api-contract § 0.5: a text over 100 characters is mod-014's own 400, passed through.
    [Fact]
    public async Task Handle_TextOver100Characters_PassesMod014sValidationFailureThrough()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.SearchAsync(new string('я', 101)));

        Assert.Equal("user_directory.validation_failed", refusal.Code);
    }

    // The address chain from the outside in (BR-M1-22).
    [Fact]
    public async Task Handle_ADeletedTeam_IsTeamNotFound()
    {
        var f = new Fixture();
        f.World.Platform.Delete();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.SearchAsync("кузнецов"));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }

    [Theory]
    [InlineData("organization")]
    [InlineData("team")]
    public async Task Handle_MalformedAddress_AnswersItsLevelsNotFound(string level)
    {
        var f = new Fixture();
        var query = level == "organization"
            ? new SearchTeamMemberCandidatesQuery("abc", f.World.PlatformId, "кузнецов")
            : new SearchTeamMemberCandidatesQuery(f.World.OrganizationId, "abc", "кузнецов");

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.Handler.Handle(query, CancellationToken.None));

        Assert.Equal(level == "organization" ? OrganizationErrorCodes.OrganizationNotFound : OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }
}
