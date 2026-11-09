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
}
