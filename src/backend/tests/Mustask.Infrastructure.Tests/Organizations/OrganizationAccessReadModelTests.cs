using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure.Tests.Process;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// mod-016 plan-backend § 3.4, § 3.5 · the scoped check's only read (ADR-0027 rule 3): the organization's status joined to the
/// caller's membership in <b>one</b> statement that touches no user-projection table (BR-M1-32: a projected status never refuses or
/// grants an access check).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class OrganizationAccessReadModelTests(OrganizationsDatabaseFixture database)
{
    [Theory]
    [InlineData(OrganizationRole.Admin)]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Find_AMember_AnswersTheOrganizationsStatusAndTheirRole(OrganizationRole role)
    {
        var structure = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, "u-access", role);

        var row = await FindAsync(structure.Organization.Id, "u-access");

        Assert.Equal(new OrganizationAccessRow(OrganizationStatus.Active, role), row);
    }

    // A non-member: the organization exists (its status is read) and the caller has no role.
    [Fact]
    public async Task Find_ANonMember_HasNoRole()
    {
        var structure = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, "u-someone-else");

        var row = await FindAsync(structure.Organization.Id, "u-access");

        Assert.Equal(new OrganizationAccessRow(OrganizationStatus.Active, null), row);
    }

    // The membership of another organization grants nothing here.
    [Fact]
    public async Task Find_AMemberOfAnotherOrganizationOnly_HasNoRole()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        await database.AddMemberAsync(other.Organization.Id, "u-access", OrganizationRole.Admin);

        var row = await FindAsync(structure.Organization.Id, "u-access");

        Assert.Null(row!.Role);
    }

    [Fact]
    public async Task Find_ADeactivatedOrganization_AnswersItsStatus_WithTheMembersRole()
    {
        var structure = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, "u-access", OrganizationRole.Admin);
        await database.ExecuteAsync("UPDATE organizations SET status = 'Deactivated' WHERE id = @id", ("id", structure.Organization.Id));

        var row = await FindAsync(structure.Organization.Id, "u-access");

        Assert.Equal(new OrganizationAccessRow(OrganizationStatus.Deactivated, OrganizationRole.Admin), row);
    }

    [Fact]
    public async Task Find_NoSuchOrganization_IsNull()
    {
        Assert.Null(await FindAsync(Guid.CreateVersion7(), "u-access"));
    }

    // The user id is matched exactly: another user's id, a different letter case or a trailing space is a different user.
    [Theory]
    [InlineData("U-ACCESS")]
    [InlineData("u-access ")]
    [InlineData("u-acces")]
    public async Task Find_TheUserIdIsMatchedExactly(string userId)
    {
        var structure = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, "u-access", OrganizationRole.Admin);

        var row = await FindAsync(structure.Organization.Id, userId);

        Assert.Null(row!.Role);
    }

    // ADR-0027 rule 3 / NFR: one read per request, nothing about the user projection in it.
    [Fact]
    public async Task Find_IsOneStatement_AndNeverTouchesTheUserProjection()
    {
        var structure = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, "u-access", OrganizationRole.Member);
        var counter = new CommandCountingInterceptor();
        await using var provider = database.BuildServices(counter);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IOrganizationAccessReadModel>()
            .FindAsync(structure.Organization.Id, "u-access", CancellationToken.None);

        var statement = Assert.Single(counter.Commands);
        Assert.DoesNotContain("directory_users", statement, StringComparison.Ordinal);
        Assert.Contains("organization_memberships", statement, StringComparison.Ordinal);
    }

    private async Task<OrganizationAccessRow?> FindAsync(Guid organizationId, string userId)
    {
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IOrganizationAccessReadModel>()
            .FindAsync(organizationId, userId, CancellationToken.None);
    }
}
