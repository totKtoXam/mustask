using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// feat-041 (doc-016 BR-TD-12): the inverse form of the sight rule (<see cref="IScopedAccessReadModel.UsersSeeingProjectAsync"/>, who sees the
/// project) answers for every member as the set form does for each caller (<see cref="AccessSet.SeesProject"/>), flag off and flag on.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class UsersSeeingProjectParityTests(OrganizationsDatabaseFixture database)
{
    private static readonly (string Name, OrganizationRole Role)[] People =
    [
        ("admin", OrganizationRole.Admin),
        ("granted", OrganizationRole.Viewer),
        ("teamed", OrganizationRole.Member),
        ("plain", OrganizationRole.Member),
        ("elsewhere", OrganizationRole.Viewer),
    ];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheInverseForm_NamesExactlyTheCallersWhomTheSetFormLetsSee(bool visibleToOrganization)
    {
        var structure = await database.CreateStructureAsync();
        var organization = structure.Organization.Id;
        var project = structure.Project.Project.Id;
        var users = People.ToDictionary(person => person.Name, person => $"{person.Name}-{Guid.CreateVersion7()}");

        await using (var context = database.NewContext())
        {
            foreach (var (name, role) in People)
            {
                context.OrganizationMemberships.Add(OrganizationMembership.Add(organization, users[name], role, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
            }

            await context.SaveChangesAsync();

            context.ProjectGrants.Add(ProjectGrant.Give(organization, project, users["granted"], GrantRole.Reader, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
            context.AreaGrants.Add(AreaGrant.Give(organization, structure.Department.Id, users["elsewhere"], GrantRole.Reader, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
            context.TeamMemberships.Add(TeamMembership.Add(structure.FirstTeam.Id, organization, users["teamed"]));

            if (visibleToOrganization)
            {
                (await context.Projects.SingleAsync(p => p.Id == project)).SetVisibleToOrganization(true);
            }

            await context.SaveChangesAsync();
        }

        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<IScopedAccessReadModel>();

        var seeing = await model.UsersSeeingProjectAsync(organization, project, default);

        foreach (var (name, role) in People)
        {
            var set = await model.LoadSetAsync(organization, users[name], role, default);

            Assert.True(set.SeesProject(project) == seeing.Contains(users[name]), $"{name}: set form {set.SeesProject(project)}, inverse form {seeing.Contains(users[name])}.");
        }

        Assert.Contains(users["admin"], seeing);
        Assert.Equal(visibleToOrganization, seeing.Contains(users["plain"]));
        Assert.Equal(visibleToOrganization, seeing.Contains(users["elsewhere"]));
        Assert.True(seeing.Contains(users["granted"]) && seeing.Contains(users["teamed"]));
    }
}
