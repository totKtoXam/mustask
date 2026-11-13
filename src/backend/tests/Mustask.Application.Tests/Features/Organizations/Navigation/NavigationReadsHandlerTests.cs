using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations.GetOverview;
using Mustask.Application.Features.Organizations.GetSelectorOffers;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.Navigation;

/// <summary>
/// feat-043 US-2 (BR-NV-9, BR-NV-15) on a scripted read model: the handlers of «Обзор» and of the selectors' offers hand the
/// read the caller (organization, user, role), pass the rows through in the order received, and refuse only at the organization level.
/// </summary>
public sealed class NavigationReadsHandlerTests
{
    private static readonly Guid Organization = Guid.CreateVersion7();

    private sealed class ScriptedNavigationReadModel : INavigationReadModel
    {
        public List<CallerAccess> Callers { get; } = [];

        public Task<OverviewRows> ReadOverviewAsync(CallerAccess caller, CancellationToken cancellationToken)
        {
            Callers.Add(caller);

            return Task.FromResult(new OverviewRows(
                2,
                3,
                4,
                [new NavigationRef(Guid.Empty, "Логистика")],
                [new NavigationProjectRef(Guid.Empty, "Альфа", "ALF")],
                [new NavigationTeamRef(Guid.Empty, "MARS", false)],
                [new NavigationTeamRef(Guid.Empty, "VENUS", true)]));
        }

        public Task<SelectorOfferRows> ReadSelectorOffersAsync(CallerAccess caller, CancellationToken cancellationToken)
        {
            Callers.Add(caller);
            var team = Guid.CreateVersion7();

            return Task.FromResult(new SelectorOfferRows(
                [new OfferDepartmentRow(Guid.CreateVersion7(), "Логистика", "blue")],
                [new OfferProjectRow(Guid.CreateVersion7(), "Альфа", "ALF", null, null, [team], "orange")],
                [new OfferTeamRow(team, "MARS", true, Guid.CreateVersion7(), "green")]));
        }
    }

    // US-2 · "Обзор": the counts and the cut lists reach the response unchanged, the read got the caller.
    [Fact]
    public async Task Overview_PassesTheRowsThrough_AndHandsTheReadTheCaller()
    {
        var read = new ScriptedNavigationReadModel();
        var handler = new GetOverviewHandler(read, new FakeOrganizationAccess(OrganizationRole.Viewer), new FakeCurrentUser { SubjectId = "u-olga" });

        var response = await handler.Handle(new GetOverviewQuery(Organization.ToString()), CancellationToken.None);

        Assert.Equal((2, 3, 4), (response.DepartmentCount, response.ProjectCount, response.TeamCount));
        Assert.Equal(["Логистика"], response.Departments.Select(row => row.Name));
        Assert.Equal(["ALF"], response.Projects.Select(row => row.Key));
        Assert.Equal(["VENUS"], response.MyTeams.Select(row => row.Name));
        Assert.Equal(new CallerAccess(Organization, "u-olga", OrganizationRole.Viewer), Assert.Single(read.Callers));
    }

    // US-8 · the selectors' offers: the links reach the panel, the read got the caller.
    [Fact]
    public async Task SelectorOffers_PassTheLinksThrough_AndHandTheReadTheCaller()
    {
        var read = new ScriptedNavigationReadModel();
        var handler = new GetSelectorOffersHandler(read, new FakeOrganizationAccess(OrganizationRole.Member), new FakeCurrentUser { SubjectId = "u-max" });

        var response = await handler.Handle(new GetSelectorOffersQuery(Organization.ToString()), CancellationToken.None);

        Assert.Equal(response.Teams[0].Id, Assert.Single(response.Projects[0].TeamIds));
        Assert.Null(response.Projects[0].DepartmentId);
        Assert.Equal(new CallerAccess(Organization, "u-max", OrganizationRole.Member), Assert.Single(read.Callers));
    }

    // BR-M1-20 · a malformed organization id is the organization's not-found, and the read is not asked.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task BothReads_MalformedOrganization_AreOrganizationNotFound_WithoutReading(string? organizationId)
    {
        var read = new ScriptedNavigationReadModel();
        var access = new FakeOrganizationAccess(OrganizationRole.Admin);
        var user = new FakeCurrentUser();

        var overview = await Assert.ThrowsAsync<NotFoundException>(
            () => new GetOverviewHandler(read, access, user).Handle(new GetOverviewQuery(organizationId), CancellationToken.None));
        var offers = await Assert.ThrowsAsync<NotFoundException>(
            () => new GetSelectorOffersHandler(read, access, user).Handle(new GetSelectorOffersQuery(organizationId), CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, overview.Code);
        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, offers.Code);
        Assert.Empty(read.Callers);
    }
}
