using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Xunit;

namespace Mustask.Application.Tests.Abstractions.Organizations;

/// <summary>mod-024: the managers' scope of the import codes and <see cref="AccessSet.HoldsAnywhere"/>.</summary>
public sealed class AccessSetImportsTests
{
    private static readonly Guid DepartmentId = Guid.NewGuid();
    private static readonly Guid ProjectNodeId = Guid.NewGuid();
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid OtherProjectId = Guid.NewGuid();

    private static readonly IReadOnlyDictionary<Guid, IReadOnlyList<GrantRole>> NoGrants = new Dictionary<Guid, IReadOnlyList<GrantRole>>();

    private static AccessSet Set(
        OrganizationRole role,
        IReadOnlyDictionary<Guid, IReadOnlyList<GrantRole>>? area = null,
        IReadOnlyDictionary<Guid, IReadOnlyList<GrantRole>>? project = null) =>
        new(
            role,
            [
                new AccessNode(DepartmentId, AreaType.Department, null, null, false),
                new AccessNode(ProjectNodeId, AreaType.Project, DepartmentId, ProjectId, true),
                new AccessNode(Guid.NewGuid(), AreaType.Project, null, OtherProjectId, false),
            ],
            area ?? NoGrants,
            project ?? NoGrants,
            NoGrants);

    [Fact]
    public void HoldsAnywhere_Administrator_HoldsEverything() =>
        Assert.True(Set(OrganizationRole.Admin).HoldsAnywhere(PermissionCatalog.ImportsTemplateWrite));

    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public void HoldsAnywhere_MemberOrViewerWithoutGrants_HoldsNeitherWriteNorRun(OrganizationRole role)
    {
        var set = Set(role);

        Assert.False(set.HoldsAnywhere(PermissionCatalog.ImportsTemplateWrite));
        Assert.False(set.HoldsAnywhere(PermissionCatalog.ImportsImportRun));
    }

    [Fact]
    public void HoldsAnywhere_ProjectManager_HoldsWriteAndRunOnThatProjectOnly()
    {
        var set = Set(OrganizationRole.Member, project: new Dictionary<Guid, IReadOnlyList<GrantRole>> { [ProjectId] = [GrantRole.Manager] });

        Assert.True(set.HoldsAnywhere(PermissionCatalog.ImportsTemplateWrite));
        Assert.True(set.Holds(PermissionCatalog.ImportsImportRun, ProjectId));
        Assert.False(set.Holds(PermissionCatalog.ImportsImportRun, OtherProjectId));
    }

    [Fact]
    public void HoldsAnywhere_DepartmentManager_ReachesTheDepartmentsProject()
    {
        var set = Set(OrganizationRole.Member, area: new Dictionary<Guid, IReadOnlyList<GrantRole>> { [DepartmentId] = [GrantRole.Manager] });

        Assert.True(set.HoldsAnywhere(PermissionCatalog.ImportsImportRun));
        Assert.True(set.Holds(PermissionCatalog.ImportsImportRun, ProjectId));
        Assert.False(set.Holds(PermissionCatalog.ImportsImportRun, OtherProjectId));
    }

    [Fact]
    public void HoldsAnywhere_ProjectMemberOnly_HoldsNeither()
    {
        var set = Set(OrganizationRole.Member, project: new Dictionary<Guid, IReadOnlyList<GrantRole>> { [ProjectId] = [GrantRole.Member] });

        Assert.False(set.HoldsAnywhere(PermissionCatalog.ImportsTemplateWrite));
    }

    [Fact]
    public void TemplateRead_IsHeldByEveryRole()
    {
        foreach (var role in OrganizationRoles.All)
        {
            Assert.True(Set(role).HoldsAnywhere(PermissionCatalog.ImportsTemplateRead));
        }
    }
}
