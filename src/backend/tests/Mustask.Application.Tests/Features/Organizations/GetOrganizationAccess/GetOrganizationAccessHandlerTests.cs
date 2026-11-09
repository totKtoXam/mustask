using Mustask.Application.Features.Organizations.GetOrganizationAccess;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;

namespace Mustask.Application.Tests.Features.Organizations.GetOrganizationAccess;

/// <summary>
/// <c>GetOrganizationAccess</c> on a scripted access (mod-016 plan-backend § 3.6 row 1; US-4 "Mustask.Api serves the role and the
/// codes of one organization"): the role and the codes of its bundle decided at the organization, in catalog order, no instance code. The 404 for a non-member and the
/// <c>no-store</c> header are the scoped check's and the endpoint's, tested at the Api.
/// </summary>
public sealed class GetOrganizationAccessHandlerTests
{
    private static Task<OrganizationAccessResponse> HandleAsync(OrganizationRole role) =>
        new GetOrganizationAccessHandler(new FakeOrganizationAccess(role)).Handle(new GetOrganizationAccessQuery(), CancellationToken.None);

    // BR-SA-11 · api-contract § 5: the answer holds the role and the codes of its bundle decided at the organization, in catalog order.
    [Theory]
    [InlineData(OrganizationRole.Admin, "admin")]
    [InlineData(OrganizationRole.Member, "member")]
    [InlineData(OrganizationRole.Viewer, "viewer")]
    public async Task Handle_EachRole_AnswersItsNameAndItsOrganizationScopeCodes_InCatalogOrder(OrganizationRole role, string name)
    {
        var response = await HandleAsync(role);

        Assert.Equal(name, response.Role);
        Assert.Equal(
            PermissionCatalog.All
                .Where(definition => definition.Resource == ResourceScope.Organization && OrganizationRoleBundles.Holds(role, definition.Code))
                .Select(definition => definition.Code),
            response.Permissions);
    }

    // BR-SA-11 · the exact served set of each role, spelled out so a change of a bundle or of a code's scope fails here.
    [Fact]
    public async Task Handle_Admin_ServesExactlyTheTwelveOrganizationScopeCodes()
    {
        var response = await HandleAsync(OrganizationRole.Admin);

        Assert.Equal(
            [
                "organizations.organization.read",
                "organizations.organization.write",
                "organizations.organization.deactivate",
                "organizations.membership.read",
                "organizations.membership.write",
                "organizations.project.deactivate",
                "organizations.team.read",
                "organizations.team.create",
                "organizations.team.delete",
                "organizations.area.read",
                "organizations.area.create",
                "process.definition.read",
            ],
            response.Permissions);
    }

    [Fact]
    public async Task Handle_Member_ServesExactlyTheFiveOrganizationScopeReads()
    {
        var response = await HandleAsync(OrganizationRole.Member);

        Assert.Equal(
            [
                "organizations.organization.read",
                "organizations.membership.read",
                "organizations.team.read",
                "organizations.area.read",
                "process.definition.read",
            ],
            response.Permissions);
    }

    [Fact]
    public async Task Handle_Viewer_ServesExactlyTheFiveOrganizationScopeReads()
    {
        var response = await HandleAsync(OrganizationRole.Viewer);

        Assert.Equal(
            [
                "organizations.organization.read",
                "organizations.membership.read",
                "organizations.team.read",
                "organizations.area.read",
                "process.definition.read",
            ],
            response.Permissions);
    }

    // BR-SA-11 · project, team and area codes are served on their own objects, never in the organization answer.
    [Theory]
    [InlineData(OrganizationRole.Admin)]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Handle_NeverListsACodeOfANarrowerScope(OrganizationRole role)
    {
        var response = await HandleAsync(role);

        Assert.All(response.Permissions, code => Assert.Equal(ResourceScope.Organization, PermissionCatalog.ResourceScopeOf(code)));
        Assert.DoesNotContain("organizations.area.write", response.Permissions);
        Assert.DoesNotContain("workitems.workitem.write", response.Permissions);
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
        var access = new FakeOrganizationAccess(OrganizationRole.Admin);
        var handler = new GetOrganizationAccessHandler(access);

        var before = await handler.Handle(new GetOrganizationAccessQuery(), CancellationToken.None);
        access.Role = OrganizationRole.Viewer;
        var after = await handler.Handle(new GetOrganizationAccessQuery(), CancellationToken.None);

        Assert.Equal(("admin", "viewer"), (before.Role, after.Role));
        Assert.Contains("organizations.organization.write", before.Permissions);
        Assert.DoesNotContain("organizations.organization.write", after.Permissions);
    }
}
