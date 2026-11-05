using Mustask.Application.Features.Organizations.GetAreaTree;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.GetAreaTree;

/// <summary>mod-018 US-1 on fakes: the typed tree of an organization as one list in pre-order, with the type, path and level of each node.</summary>
public sealed class GetAreaTreeHandlerTests
{
    private static GetAreaTreeHandler HandlerOver(InMemoryOrganizations store) => new(new FakeAreaTreeReadModel(store));

    // US-1 · "Every member reads the whole tree": each node before its children, siblings by name, with type, path and level.
    [Fact]
    public async Task Handle_ListsThePreOrderWithTypePathAndDepth()
    {
        var store = new InMemoryOrganizations();
        var organization = store.AddOrganization();
        var development = store.AddDepartment(organization, "Разработка");
        var project = store.AddProject(organization, "Мастаск", "MST", development).Project;
        var quarterly = store.AddArea(store.RootOf(project), "Квартальные");
        var standalone = store.AddProject(organization, "Автономный", "AUT").Project;

        var result = await HandlerOver(store).Handle(new GetAreaTreeQuery(organization.Id.ToString()), CancellationToken.None);

        Assert.Equal(["Автономный", "Разработка", "Мастаск", "Квартальные"], result.Items.Select(item => item.Name));
        Assert.Equal(["project", "department", "project", "area"], result.Items.Select(item => item.Type));
        Assert.Equal([1, 1, 2, 3], result.Items.Select(item => item.Depth));
        Assert.Equal("Разработка / Мастаск / Квартальные", result.Items[3].Path);
        Assert.Equal(new Guid?[] { standalone.Id, null, project.Id, project.Id }, result.Items.Select(item => item.ProjectId));
        Assert.Equal(quarterly.Id, result.Items[3].Id);
        Assert.Equal(store.RootOf(project).Id, result.Items[3].ParentId);
        Assert.Null(result.Items[1].ParentId);
    }

    // OQ-6 A · a deactivated project's node and sub-areas stay in the tree, marked.
    [Fact]
    public async Task Handle_DeactivatedProject_StaysInTheTreeMarkedInactive()
    {
        var store = new InMemoryOrganizations();
        var organization = store.AddOrganization();
        var project = store.AddProject(organization).Project;
        store.AddArea(store.RootOf(project), "Квартальные");
        var department = store.AddDepartment(organization, "Разработка");
        project.Deactivate();

        var result = await HandlerOver(store).Handle(new GetAreaTreeQuery(organization.Id.ToString()), CancellationToken.None);

        Assert.Null(result.Items.Single(item => item.Id == department.Id).ProjectActive);
        Assert.All(result.Items.Where(item => item.ProjectId == project.Id), item => Assert.Equal(false, item.ProjectActive));
        Assert.Equal(3, result.Items.Count);
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
        store.AddDepartment(store.AddOrganization("Другая"), "Чужая");

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
