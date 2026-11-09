using Microsoft.Extensions.DependencyInjection;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Xunit.Abstractions;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// mod-019 NFR "the check's cost": the scoped check and the set form read the caller's grants and the tree in a fixed number of statements,
/// whatever the number of grants the organization holds (spec § NFR; plan-backend step 5). The caller holds one grant; the other users
/// hold ten, then ten thousand, on the same project.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class ScopedAccessStatementCountTests(OrganizationsDatabaseFixture database, ITestOutputHelper output)
{
    private const int CheckStatements = 3;
    private const int AreaStatements = 3;
    private const int ProjectStatements = 4;
    private const int SetStatements = 4;

    private async Task<(Guid Organization, Guid Project, string Caller)> SeedAsync(int otherHolders)
    {
        var structure = await database.CreateStructureAsync();
        var organization = structure.Organization.Id;
        var project = structure.Project.Project.Id;
        var caller = $"caller-{Guid.CreateVersion7()}";

        await using var context = database.NewContext();
        context.OrganizationMemberships.Add(OrganizationMembership.Add(organization, caller, OrganizationRole.Viewer, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
        context.ProjectGrants.Add(ProjectGrant.Give(organization, project, caller, GrantRole.Manager, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));

        for (var index = 0; index < otherHolders; index++)
        {
            var user = $"holder-{Guid.CreateVersion7()}";
            context.OrganizationMemberships.Add(OrganizationMembership.Add(organization, user, OrganizationRole.Viewer, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
            context.ProjectGrants.Add(ProjectGrant.Give(organization, project, user, GrantRole.Reader, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
        }

        await context.SaveChangesAsync();

        return (organization, project, caller);
    }

    private async Task<int> CountAsync(Func<IScopedAccessReadModel, Task> run)
    {
        var counter = new SqlLogInterceptor();
        await using var provider = database.BuildServices(counter);
        await using var scope = provider.CreateAsyncScope();

        await run(scope.ServiceProvider.GetRequiredService<IScopedAccessReadModel>());

        return counter.Commands.Count;
    }

    // mod-019 NFR · the same statements for ten grants as for ten thousand, for the check on one object and for the set form.
    [Fact]
    public async Task TheCheckAndTheSetForm_TakeTheSameNumberOfStatements_AtTenAndAtTenThousandGrants()
    {
        var small = await SeedAsync(10);
        var large = await SeedAsync(10_000);

        var smallCheck = await CountAsync(model => model.ReachingGrantsAsync(small.Organization, small.Caller, ResourceScope.Project, small.Project, default));
        var largeCheck = await CountAsync(model => model.ReachingGrantsAsync(large.Organization, large.Caller, ResourceScope.Project, large.Project, default));
        var smallSet = await CountAsync(model => model.LoadSetAsync(small.Organization, small.Caller, OrganizationRole.Viewer, default));
        var largeSet = await CountAsync(model => model.LoadSetAsync(large.Organization, large.Caller, OrganizationRole.Viewer, default));
        output.WriteLine($"check: {smallCheck} / {largeCheck}; set: {smallSet} / {largeSet}");

        Assert.Equal(smallCheck, largeCheck);
        Assert.Equal(smallSet, largeSet);
        Assert.True(largeCheck <= CheckStatements, $"The check took {largeCheck} statements; pinned at most {CheckStatements}.");
        Assert.True(largeSet <= SetStatements, $"The set form took {largeSet} statements; pinned at most {SetStatements}.");
    }

    /// <summary>A department with a project under it (the node inherits) and a chain of <paramref name="subAreas"/> sub-areas below the node.</summary>
    private async Task<(Guid Organization, Guid Project, Guid Deepest, string Caller, Guid Department)> SeedTreeAsync(int subAreas)
    {
        var structure = await database.CreateStructureAsync();
        var organization = structure.Organization.Id;
        var caller = $"caller-{Guid.CreateVersion7()}";
        var department = OrganizationsDatabaseFixture.NewDepartment(organization, OrganizationsDatabaseFixture.UniqueName("Департамент"));
        var project = Project.Create(
            organization, OrganizationsDatabaseFixture.Name(OrganizationsDatabaseFixture.UniqueName("Проект")), "DEP", OrganizationsDatabaseFixture.Now,
            OrganizationsDatabaseFixture.Creator, null, department.Id);
        var areas = new List<Area> { department, project.Root };
        var deepest = project.Root;

        for (var level = 0; level < subAreas; level++)
        {
            deepest = OrganizationsDatabaseFixture.NewSubArea(deepest, $"Уровень {level}");
            areas.Add(deepest);
        }

        await using var context = database.NewContext();
        context.OrganizationMemberships.Add(OrganizationMembership.Add(organization, caller, OrganizationRole.Viewer, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
        context.Projects.Add(project.Project);
        context.Areas.AddRange(areas);
        await context.SaveChangesAsync();

        context.AreaGrants.Add(AreaGrant.Give(organization, department.Id, caller, GrantRole.Manager, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
        context.ProjectGrants.Add(ProjectGrant.Give(organization, project.Project.Id, caller, GrantRole.Reader, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
        await context.SaveChangesAsync();

        return (organization, project.Project.Id, deepest.Id, caller, department.Id);
    }

    // mod-019 NFR · the area walk is one statement, so the count is equal at depth 1 and depth 10 (review C-1); the project under a
    // department takes the same fixed number: the project check (node, department grants) and the area check (walk, two grant reads).
    [Fact]
    public async Task TheAreaCheck_TakesTheSameNumberOfStatements_AtDepthOneAndTen()
    {
        var shallow = await SeedTreeAsync(0);
        var deep = await SeedTreeAsync(9);

        var shallowCheck = await CountAsync(model => model.ReachingGrantsAsync(shallow.Organization, shallow.Caller, ResourceScope.Area, shallow.Deepest, default));
        var deepCheck = await CountAsync(model => model.ReachingGrantsAsync(deep.Organization, deep.Caller, ResourceScope.Area, deep.Deepest, default));
        var shallowProject = await CountAsync(model => model.ReachingGrantsAsync(shallow.Organization, shallow.Caller, ResourceScope.Project, shallow.Project, default));
        var deepProject = await CountAsync(model => model.ReachingGrantsAsync(deep.Organization, deep.Caller, ResourceScope.Project, deep.Project, default));
        output.WriteLine($"area: {shallowCheck} / {deepCheck}; project: {shallowProject} / {deepProject}");

        Assert.Equal(shallowCheck, deepCheck);
        Assert.Equal(shallowProject, deepProject);
        Assert.Equal(AreaStatements, deepCheck);
        Assert.Equal(ProjectStatements, deepProject);

        // The deep node still reaches the department's grant and the project's: nothing is lost to the single statement.
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var reaching = await scope.ServiceProvider.GetRequiredService<IScopedAccessReadModel>()
            .ReachingGrantsAsync(deep.Organization, deep.Caller, ResourceScope.Area, deep.Deepest, default);

        Assert.NotNull(reaching);
        Assert.Contains(new ReachingGrant(ResourceScope.Area, GrantRole.Manager), reaching);
        Assert.Contains(new ReachingGrant(ResourceScope.Project, GrantRole.Reader), reaching);
    }
}
