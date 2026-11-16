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
        IReadOnlyDictionary<Guid, IReadOnlyList<GrantRole>> teamGrants,
        SightData? sight = null)
    {
        _sight = sight ?? SightData.None;
        Role = role;
        _nodes = nodes.ToDictionary(node => node.Id);
        _projectNodes = _nodes.Values.Where(node => node.Type == AreaType.Project && node.ProjectId is not null)
            .ToDictionary(node => node.ProjectId!.Value);
        _areaGrants = areaGrants;
        _projectGrants = projectGrants;
        _teamGrants = teamGrants;
    }

    private readonly SightData _sight;

    public OrganizationRole Role { get; }

    /// <summary>
    /// Whether the caller sees the project (doc-016 BR-TD-12): a reaching grant, a linked team's membership or the flag; the
    /// administrator sees every project. <c>false</c> for a project that is not in the organization: a hidden and a missing
    /// project answer alike. The administrator's set holds no rows and is not asked (the caller keeps its own existence answers).
    /// </summary>
    public bool SeesProject(Guid projectId)
    {
        if (Role == OrganizationRole.Admin)
        {
            return true;
        }

        if (!_sight.Projects.TryGetValue(projectId, out var visibleToOrganization))
        {
            return false;
        }

        var memberOfLinkedTeam = _sight.TeamsOfProject.TryGetValue(projectId, out var teams) && teams.Any(_sight.MemberOfTeams.Contains);

        return SightRules.SeesProject(Role, visibleToOrganization, Reaching(ResourceScope.Project, projectId).Count > 0, memberOfLinkedTeam);
    }

    /// <summary>Whether the caller sees the team (BR-TD-13); <c>false</c> for a team not in the organization.</summary>
    public bool SeesTeam(Guid teamId)
    {
        if (Role == OrganizationRole.Admin)
        {
            return true;
        }

        if (!_sight.Teams.TryGetValue(teamId, out var department))
        {
            return false;
        }

        var grantOnDepartment = department is { } d && _areaGrants.ContainsKey(d);
        var seesLinkedProject = _sight.TeamsOfProject.Any(pair => pair.Value.Contains(teamId) && SeesProject(pair.Key));

        return SightRules.SeesTeam(Role, _sight.MemberOfTeams.Contains(teamId), _teamGrants.ContainsKey(teamId) || grantOnDepartment, seesLinkedProject);
    }

    /// <summary>
    /// Whether the caller sees the area (BR-TD-14): a department by a grant on it or a team or project standing in it; a project's
    /// node and its sub-areas exactly when their project is seen. <c>false</c> for an area not in the organization.
    /// </summary>
    public bool SeesArea(Guid areaId)
    {
        if (Role == OrganizationRole.Admin)
        {
            return true;
        }

        if (!_nodes.TryGetValue(areaId, out var node))
        {
            return false;
        }

        if (node.Type != AreaType.Department)
        {
            return node.ProjectId is { } projectId && SeesProject(projectId);
        }

        var seesTeamInside = _sight.Teams.Any(pair => pair.Value == areaId && SeesTeam(pair.Key));
        var seesProjectInside = _projectNodes.Values.Any(projectNode => projectNode.ParentId == areaId && SeesProject(projectNode.ProjectId!.Value));

        return SightRules.SeesDepartment(Role, _areaGrants.ContainsKey(areaId), seesTeamInside, seesProjectInside);
    }

    /// <summary>Whether the project, team or department exists in the organization as far as the sight data knows (non-administrator sets only).</summary>
    public bool KnowsTeam(Guid teamId) => _sight.Teams.ContainsKey(teamId);

    /// <summary>Whether the sight data holds the project (non-administrator sets only).</summary>
    public bool KnowsProject(Guid projectId) => _sight.Projects.ContainsKey(projectId);

    /// <summary>Whether the tree holds the area (non-administrator sets only).</summary>
    public bool KnowsArea(Guid areaId) => _nodes.ContainsKey(areaId);

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
