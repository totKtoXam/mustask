using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Search;
using Mustask.Application.Features.Search.SearchStructure;
using Mustask.Domain.Identity;
using Mustask.Domain.Search;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Search;

/// <summary>feat-071 · feat-065 US-2 and US-4: the handler's rules over a fake read model (the SQL itself is the integration tests').</summary>
public sealed class SearchStructureHandlerTests
{
    private static readonly Guid Organization = Guid.CreateVersion7();

    private sealed class FakeReads(params SearchGroupRows[] groups) : ISearchReadModel
    {
        public List<(SearchScope Scope, IReadOnlyCollection<SearchGroupKind> Kinds, int Take, string Folded)> Reads { get; } = [];

        public int CandidateReads { get; private set; }

        public SearchCandidates Candidates { get; set; } = new([], [], [], []);

        public Task<SearchCandidates> ReadCandidatesAsync(Guid organizationId, IReadOnlyCollection<SearchGroupKind> kinds, CancellationToken cancellationToken)
        {
            CandidateReads++;

            return Task.FromResult(Candidates);
        }

        public Task<IReadOnlyList<SearchGroupRows>> ReadAsync(
            IReadOnlyList<SearchScope> scopes, SearchQueryText query, IReadOnlyCollection<SearchGroupKind> kinds, int take, CancellationToken cancellationToken)
        {
            Reads.Add((scopes.Single(), kinds, take, query.Folded));

            return Task.FromResult<IReadOnlyList<SearchGroupRows>>(groups.Where(group => kinds.Contains(group.Kind)).ToList());
        }
    }

    private sealed class Access(OrganizationRole role, bool membershipRead = true) : IOrganizationAccess
    {
        public OrganizationRole Role => role;

        public bool Holds(string code) => code != PermissionCatalog.OrganizationsMembershipRead || membershipRead;

        public Task<VisibleProjects> VisibleProjectsAsync(CancellationToken cancellationToken) => Task.FromResult(VisibleProjects.All);
    }

    private static SearchRow Row(SearchGroupKind kind, string name, int tier) =>
        new(kind, Guid.CreateVersion7().ToString(), name, null, null, null, null, null, null, kind == SearchGroupKind.Users ? "a@b.test" : null, kind == SearchGroupKind.Users ? false : null, tier);

    private static SearchGroupRows Group(SearchGroupKind kind, params (string Name, int Tier)[] rows) =>
        new(kind, rows.Select(row => Row(kind, row.Name, row.Tier)).ToList());

    private static SearchStructureQuery Query(string text, string? expand = null) => new(Organization.ToString(), text, expand);

    // US-2 · "Users need the membership read code": the users group is never read and never present without the code.
    [Fact]
    public async Task WithoutTheMembershipRead_TheUsersGroupIsNeverReadNorPresent()
    {
        var reads = new FakeReads(Group(SearchGroupKind.Teams, ("Орлы", 2)), Group(SearchGroupKind.Users, ("Макс Орлов", 4)));
        var handler = new SearchStructureHandler(reads, new Access(OrganizationRole.Admin, membershipRead: false));

        var answer = await handler.Handle(Query("орл"), CancellationToken.None);

        Assert.Equal(["teams"], answer.Groups.Select(group => group.Kind));
        Assert.DoesNotContain(SearchGroupKind.Users, reads.Reads.Single().Kinds);
        Assert.False(reads.Reads.Single().Scope.UsersAllowed);
    }

    // US-4 · "Users": expanding the users group without the code reads nothing and answers no groups.
    [Fact]
    public async Task WithoutTheMembershipRead_ExpandingUsersAnswersNothingAndReadsNothing()
    {
        var reads = new FakeReads(Group(SearchGroupKind.Users, ("Макс Орлов", 4)));
        var handler = new SearchStructureHandler(reads, new Access(OrganizationRole.Member, membershipRead: false));

        var answer = await handler.Handle(Query("орл", "users"), CancellationToken.None);

        Assert.Empty(answer.Groups);
        Assert.Empty(reads.Reads);
        Assert.Equal(0, reads.CandidateReads);
    }

    // US-4 · BR-TD-12: the administrator's read passes no ids and spends no candidates statement.
    [Fact]
    public async Task TheAdministrator_PassesNoIds_AndReadsNoCandidates()
    {
        var reads = new FakeReads();
        var handler = new SearchStructureHandler(reads, new Access(OrganizationRole.Admin));

        await handler.Handle(Query("ал"), CancellationToken.None);

        Assert.Equal(SearchVisibility.Unrestricted, reads.Reads.Single().Scope.Visibility);
        Assert.Equal(0, reads.CandidateReads);
    }

    // US-4 · BR-GS-4: a non-administrator's candidates go through the sight set before the read; one the set does not show never reaches it.
    [Fact]
    public async Task ANonAdministrator_ReadsOnlyTheIdsThatSightAllows()
    {
        var reads = new FakeReads { Candidates = new([Guid.CreateVersion7()], [Guid.CreateVersion7()], [Guid.CreateVersion7()], [Guid.CreateVersion7()]) };
        var handler = new SearchStructureHandler(reads, new Access(OrganizationRole.Member));

        await handler.Handle(Query("ал"), CancellationToken.None);

        var visibility = reads.Reads.Single().Scope.Visibility;
        Assert.Equal(1, reads.CandidateReads);
        Assert.Empty(visibility.Projects!);
        Assert.Empty(visibility.Teams!);
        Assert.Empty(visibility.Departments!);
        Assert.Empty(visibility.Areas!);
    }

    // US-2 · "At most 5 rows a group": the first read takes 5 + 1 rows, an expansion 20 + 1 and reads only its group.
    [Fact]
    public async Task TheFirstRead_AsksSixRowsOfEveryGroup_AndAnExpansionTwentyOneOfOne()
    {
        var reads = new FakeReads();
        var handler = new SearchStructureHandler(reads, new Access(OrganizationRole.Admin));

        await handler.Handle(Query("ал"), CancellationToken.None);
        await handler.Handle(Query("ал", "projects"), CancellationToken.None);

        Assert.Equal((6, 5), (reads.Reads[0].Take, reads.Reads[0].Kinds.Count));
        Assert.Equal((21, 1), (reads.Reads[1].Take, reads.Reads[1].Kinds.Count));
    }

    // US-2 · "At most 5 rows a group and «Показать ещё»": a sixth visible row is hasMore and not shown; an expansion never says more.
    [Fact]
    public async Task HasMore_IsTrueOnlyPastTheLimit_AndNeverOnAnExpansion()
    {
        var six = Enumerable.Range(1, 6).Select(index => ($"Проект {index}", 4)).ToArray();
        var reads = new FakeReads(Group(SearchGroupKind.Projects, six));
        var handler = new SearchStructureHandler(reads, new Access(OrganizationRole.Admin));

        var first = await handler.Handle(Query("про"), CancellationToken.None);
        var expanded = await handler.Handle(Query("про", "projects"), CancellationToken.None);

        Assert.Equal((5, true), (first.Groups[0].Rows.Count, first.Groups[0].HasMore));
        Assert.Equal((6, false), (expanded.Groups[0].Rows.Count, expanded.Groups[0].HasMore));
    }

    // US-2 · "Group order follows the best match" and BR-GS-8: by the best row's tier, equal tiers in the fixed order; empty groups absent.
    [Fact]
    public async Task Groups_AreOrderedByTheirBestTier_ThenByTheFixedOrder()
    {
        var reads = new FakeReads(
            Group(SearchGroupKind.Projects, ("П", 4)),
            Group(SearchGroupKind.Teams, ("К", 2)),
            Group(SearchGroupKind.Departments, ("Д", 1)),
            Group(SearchGroupKind.Areas, ("О", 2)),
            Group(SearchGroupKind.Users, []));
        var handler = new SearchStructureHandler(reads, new Access(OrganizationRole.Admin));

        var answer = await handler.Handle(Query("снабжение"), CancellationToken.None);

        Assert.Equal(["departments", "teams", "areas", "projects"], answer.Groups.Select(group => group.Kind));
    }

    // US-2 · "Typo tolerance": a similar row is marked, fills only a short group and never displaces one.
    [Fact]
    public async Task SimilarRows_AreMarked_FillAShortGroup_AndNeverDisplaceOthers()
    {
        var reads = new FakeReads(Group(SearchGroupKind.Teams, ("А", 4), ("Б", 5)), Group(SearchGroupKind.Projects, ("1", 4), ("2", 4), ("3", 4), ("4", 4), ("5", 4), ("6", 5)));
        var handler = new SearchStructureHandler(reads, new Access(OrganizationRole.Admin));

        var answer = await handler.Handle(Query("логстика"), CancellationToken.None);

        Assert.Equal([false, true], answer.Groups.Single(group => group.Kind == "teams").Rows.Select(row => row.Similar));
        Assert.All(answer.Groups.Single(group => group.Kind == "projects").Rows, row => Assert.False(row.Similar));
        Assert.False(answer.Groups.Single(group => group.Kind == "projects").HasMore);
    }

    // US-2 · "The query is trimmed and bounded": the read gets the trimmed, folded text.
    [Fact]
    public async Task TheQuery_ReachesTheReadTrimmedAndFolded()
    {
        var reads = new FakeReads();
        var handler = new SearchStructureHandler(reads, new Access(OrganizationRole.Admin));

        await handler.Handle(Query("  ЗВЁЗДЫ  "), CancellationToken.None);

        Assert.Equal("звезды", reads.Reads.Single().Folded);
    }

    // US-2 · "Too short" and "bounded": the validator names the field and the reason, and never the text.
    [Theory]
    [InlineData("а", "query", SearchErrorCodes.QueryTooShort)]
    [InlineData(null, "query", SearchErrorCodes.QueryTooShort)]
    public void TheValidator_RefusesATooShortQuery(string? text, string field, string reason)
    {
        var failure = Assert.Throws<ValidationException>(() => SearchStructureValidator.Validate(Query(text!)));

        Assert.Equal(SearchErrorCodes.ValidationFailed, failure.Code);
        Assert.Equal(reason, failure.Errors[field][0].Code);
    }

    [Fact]
    public void TheValidator_RefusesATooLongQuery_AndAnUnknownGroup_TogetherWithTheirFields()
    {
        var failure = Assert.Throws<ValidationException>(() => SearchStructureValidator.Validate(Query(new string('я', 101), "everything")));

        Assert.Equal(SearchErrorCodes.QueryTooLong, failure.Errors["query"][0].Code);
        Assert.Equal(SearchErrorCodes.InvalidGroup, failure.Errors["expand"][0].Code);
        Assert.DoesNotContain("яяя", failure.Message, StringComparison.Ordinal);
    }

    // The address is judged before the body: a malformed organization is the organization's 404.
    [Fact]
    public async Task AMalformedOrganizationId_IsNotFound_BeforeTheBodyIsJudged()
    {
        var handler = new SearchStructureHandler(new FakeReads(), new Access(OrganizationRole.Admin));

        var failure = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new SearchStructureQuery("not-a-guid", "а", null), CancellationToken.None));

        Assert.Equal("organizations.organization.not_found", failure.Code);
    }
}
