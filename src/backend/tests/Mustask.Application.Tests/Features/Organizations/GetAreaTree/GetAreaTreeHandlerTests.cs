using Mustask.Application.Features.Organizations.GetAreaTree;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.GetAreaTree;

/// <summary>mod-018 US-1 on fakes: the tree of an organization as one list in pre-order, with the path and the level of each node.</summary>
public sealed class GetAreaTreeHandlerTests
{
    private static GetAreaTreeHandler HandlerOver(InMemoryOrganizations store) => new(new FakeAreaTreeReadModel(store));

    // US-1 · "Every member reads the whole tree": each node before its children, siblings by name, with path and level.
    [Fact]
    public async Task Handle_ListsThePreOrderWithPathAndDepth()
    {
        var store = new InMemoryOrganizations();
        var organization = store.AddOrganization();
        var reports = store.AddArea(organization, "Отчёты");
        var quarterly = store.AddArea(organization, "Квартальные", reports);
        var archive = store.AddArea(organization, "Архив");

        var result = await HandlerOver(store).Handle(new GetAreaTreeQuery(organization.Id.ToString()), CancellationToken.None);

        Assert.Equal(["Архив", "Отчёты", "Квартальные"], result.Items.Select(item => item.Name));
        Assert.Equal([1, 1, 2], result.Items.Select(item => item.Depth));
        Assert.Equal("Отчёты / Квартальные", result.Items[2].Path);
        Assert.Equal(reports.Id, result.Items[2].ParentId);
        Assert.Equal(quarterly.Id, result.Items[2].Id);
        Assert.Null(result.Items[0].ParentId);
        Assert.Equal(archive.Id, result.Items[0].Id);
    }

    // US-1 · "An organization with no area works fully": an empty list, not an error.
    [Fact]
    public async Task Handle_NoAreas_IsAnEmptyList()
    {
        var store = new InMemoryOrganizations();
        var organization = store.AddOrganization();

        var result = await HandlerOver(store).Handle(new GetAreaTreeQuery(organization.Id.ToString()), CancellationToken.None);

        Assert.Empty(result.Items);
    }

    // BR-AW-2: the tree is the organization's own; another organization's areas never appear.
    [Fact]
    public async Task Handle_OtherOrganizationsAreas_AreNotListed()
    {
        var store = new InMemoryOrganizations();
        var organization = store.AddOrganization();
        store.AddArea(store.AddOrganization("Другая"), "Чужая");

        var result = await HandlerOver(store).Handle(new GetAreaTreeQuery(organization.Id.ToString()), CancellationToken.None);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound()
    {
        var refusal = await Assert.ThrowsAsync<NotFoundException>(() =>
            HandlerOver(new InMemoryOrganizations()).Handle(new GetAreaTreeQuery("abc"), CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }
}
