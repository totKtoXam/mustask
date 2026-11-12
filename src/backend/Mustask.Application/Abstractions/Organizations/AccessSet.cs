using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>One node of the area tree as the reach walk needs it.</summary>
public sealed record AccessNode(Guid Id, AreaType Type, Guid? ParentId, Guid? ProjectId, bool InheritsAccess);

/// <summary>
/// The set form of the effective-permissions rule (ADR-0029 § 6, mod-019 BR-SA-11): one caller's grants and the tree, read once,
/// answering "what does the caller hold on object X" for any number of objects in memory. It applies the same reach rule as
/// <see cref="IScopedAccessReadModel.ReachingGrantsAsync"/> (a parity test pins the two) and the same pure function
/// <see cref="EffectivePermissions"/>, so the single and the set form cannot disagree on the union, only on the walk, which the test covers.
/// </summary>
public sealed class AccessSet
{
    private const int MaxDepth = 10;

    private readonly Dictionary<Guid, AccessNode> _nodes;
    private readonly Dictionary<Guid, AccessNode> _projectNodes;
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<GrantRole>> _areaGrants;
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<GrantRole>> _projectGrants;
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<GrantRole>> _teamGrants;

    public AccessSet(
        OrganizationRole role,
        IEnumerable<AccessNode> nodes,
        IReadOnlyDictionary<Guid, IReadOnlyList<GrantRole>> areaGrants,
        IReadOnlyDictionary<Guid, IReadOnlyList<GrantRole>> projectGrants,
        IReadOnlyDictionary<Guid, IReadOnlyList<GrantRole>> teamGrants)
    {
        Role = role;
        _nodes = nodes.ToDictionary(node => node.Id);
        _projectNodes = _nodes.Values.Where(node => node.Type == AreaType.Project && node.ProjectId is not null)
            .ToDictionary(node => node.ProjectId!.Value);
        _areaGrants = areaGrants;
        _projectGrants = projectGrants;
        _teamGrants = teamGrants;
    }

    public OrganizationRole Role { get; }

    /// <summary>The caller's codes on one object of <paramref name="scope"/>; an unknown object is judged by the role alone.</summary>
    public IReadOnlySet<string> CodesOn(ResourceScope scope, Guid objectId)
    {
        if (scope == ResourceScope.Organization || Role == OrganizationRole.Admin)
        {
            return EffectivePermissions.CodesOn(Role, scope, []);
        }

        return EffectivePermissions.CodesOn(Role, scope, Reaching(scope, objectId));
    }

    /// <summary>The caller's codes on the object in the catalog's order (the served <c>codes</c> of an object read, mod-019 BR-SA-11).</summary>
    public IReadOnlyList<string> CodesInCatalogOrder(ResourceScope scope, Guid objectId)
    {
        var held = CodesOn(scope, objectId);

        return PermissionCatalog.All.Select(definition => definition.Code).Where(held.Contains).ToList();
    }

    /// <summary>Whether the caller holds <paramref name="code"/> on the object; the object's scope is the code's.</summary>
    public bool Holds(string code, Guid objectId)
    {
        var scope = PermissionCatalog.ResourceScopeOf(code);

        return scope == ResourceScope.Organization ? OrganizationRoleBundles.Holds(Role, code) : CodesOn(scope, objectId).Contains(code);
    }

    private List<ReachingGrant> Reaching(ResourceScope scope, Guid objectId)
    {
        var reaching = new List<ReachingGrant>();

        switch (scope)
        {
            case ResourceScope.Team:
                Add(reaching, ResourceScope.Team, _teamGrants, objectId);
                break;

            case ResourceScope.Project:
                Add(reaching, ResourceScope.Project, _projectGrants, objectId);

                if (_projectNodes.TryGetValue(objectId, out var node) && node is { InheritsAccess: true, ParentId: { } department })
                {
                    Add(reaching, ResourceScope.Area, _areaGrants, department);
                }

                break;

            case ResourceScope.Area:
                if (!_nodes.TryGetValue(objectId, out var current))
                {
                    break;
                }

                if (current.ProjectId is { } projectId)
                {
                    Add(reaching, ResourceScope.Project, _projectGrants, projectId);
                }

                for (var depth = 0; depth < MaxDepth; depth++)
                {
                    if (current.Type == AreaType.Department)
                    {
                        Add(reaching, ResourceScope.Area, _areaGrants, current.Id);
                        break;
                    }

                    if (!current.InheritsAccess || current.ParentId is not { } parentId || !_nodes.TryGetValue(parentId, out var parent))
                    {
                        break;
                    }

                    current = parent;
                }

                break;
        }

        return reaching;
    }

    private static void Add(List<ReachingGrant> reaching, ResourceScope grantScope, IReadOnlyDictionary<Guid, IReadOnlyList<GrantRole>> grants, Guid objectId)
    {
        if (grants.TryGetValue(objectId, out var roles))
        {
            reaching.AddRange(roles.Select(role => new ReachingGrant(grantScope, role)));
        }
    }
}
