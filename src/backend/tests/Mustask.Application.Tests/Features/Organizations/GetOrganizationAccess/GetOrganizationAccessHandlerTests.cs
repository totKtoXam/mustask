using Mustask.Application.Features.Organizations.GetOrganizationAccess;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;

namespace Mustask.Application.Tests.Features.Organizations.GetOrganizationAccess;

/// <summary>
/// <c>GetOrganizationAccess</c> on a scripted access (mod-016 plan-backend § 3.6 row 1; US-4 "Mustask.Api serves the role and the
/// codes of one organization"): the role and exactly its bundle, sorted, no instance code. The 404 for a non-member and the
/// <c>no-store</c> header are the scoped check's and the endpoint's, tested at the Api.
/// </summary>
public sealed class GetOrganizationAccessHandlerTests
{
    private static Task<OrganizationAccessResponse> HandleAsync(OrganizationRole role) =>
        new GetOrganizationAccessHandler(new FakeOrganizationAccess(role)).Handle(new GetOrganizationAccessQuery(), CancellationToken.None);

    // US-4 · the answer holds the role and exactly the role's bundle of the spec's matrix, in ordinal order.
    [Theory]
    [InlineData(OrganizationRole.Admin, "admin")]
    [InlineData(OrganizationRole.Member, "member")]
    [InlineData(OrganizationRole.Viewer, "viewer")]
    public async Task Handle_EachRole_AnswersItsNameAndExactlyItsBundle_Sorted(OrganizationRole role, string name)
    {
        var response = await HandleAsync(role);

        Assert.Equal(name, response.Role);
        Assert.Equal(OrganizationRoleBundles.CodesOf(role).Order(StringComparer.Ordinal), response.Permissions);
    }

    [Fact]
    public async Task Handle_Admin_HoldsEveryOneOfTheTwentyFiveOrganizationCodes()
    {
        var response = await HandleAsync(OrganizationRole.Admin);

        Assert.Equal(25, response.Permissions.Count);
    }

    [Fact]
    public async Task Handle_Member_HoldsTheSevenReadsAndTheFourNonAdminWrites()
    {
        var response = await HandleAsync(OrganizationRole.Member);

        Assert.Equal(11, response.Permissions.Count);
        Assert.Contains("workitems.workitem.delete", response.Permissions);
        Assert.DoesNotContain("workitems.workitem.purge", response.Permissions);
        Assert.Contains("workitems.workitem.write", response.Permissions);
        Assert.DoesNotContain("organizations.membership.write", response.Permissions);
    }

    [Fact]
    public async Task Handle_Viewer_HoldsOnlyReadCodes()
    {
        var response = await HandleAsync(OrganizationRole.Viewer);

        Assert.Equal(7, response.Permissions.Count);
        Assert.All(response.Permissions, code => Assert.EndsWith(".read", code, StringComparison.Ordinal));
    }

    // US-4 · "it holds no instance code — those stay in the token".
    [Theory]
    [InlineData(OrganizationRole.Admin)]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Handle_NeverListsAnInstanceCode(OrganizationRole role)
    {
        var response = await HandleAsync(role);

        Assert.Empty(response.Permissions.Intersect(PermissionCatalog.InstanceCodes));
    }

    // BR-M1-31 / BR-M1-6: nothing is cached — the next call after a role change answers the new bundle.
    [Fact]
    public async Task Handle_FollowsTheRoleAtCallTime()
    {
        var access = new FakeOrganizationAccess(OrganizationRole.Member);
        var handler = new GetOrganizationAccessHandler(access);

        var before = await handler.Handle(new GetOrganizationAccessQuery(), CancellationToken.None);
        access.Role = OrganizationRole.Viewer;
        var after = await handler.Handle(new GetOrganizationAccessQuery(), CancellationToken.None);

        Assert.Equal(("member", "viewer"), (before.Role, after.Role));
        Assert.Contains("workitems.workitem.write", before.Permissions);
        Assert.DoesNotContain("workitems.workitem.write", after.Permissions);
    }
}
