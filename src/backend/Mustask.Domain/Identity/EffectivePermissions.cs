namespace Mustask.Domain.Identity;

/// <summary>One grant that reaches the object being checked: the scope it was held on and its role (ADR-0029 § 6, Reach).</summary>
public readonly record struct ReachingGrant(ResourceScope GrantScope, GrantRole Role);

/// <summary>
/// The single form of the effective-permissions rule (ADR-0029 § 6 steps 2 and 4, as amended by ADR-0038), a pure function:
/// the codes of the target's resource scope held by the organization role, plus those held by every grant that reaches the
/// target. The union is additive: no deny, no precedence, so the answer is the same in any order of the grants and with a
/// grant repeated (BR-SA-8). Which grants reach the target is the read model's question (a statement over the caller's grants
/// and the object's chain); a project grant also reaches its project's node and sub-areas, where only its codes of resource
/// scope <see cref="ResourceScope.Area"/> apply (ADR-0038 § 2), which this function gets by filtering on the target.
/// </summary>
public static class EffectivePermissions
{
    /// <summary>The organization codes the caller holds on an object of <paramref name="target"/>.</summary>
    public static IReadOnlySet<string> CodesOn(OrganizationRole role, ResourceScope target, IEnumerable<ReachingGrant> reaching)
    {
        ArgumentNullException.ThrowIfNull(reaching);

        var codes = new HashSet<string>(StringComparer.Ordinal);

        Add(codes, OrganizationRoleBundles.CodesOf(role), target);

        // The administrator holds every code on every object; the grants add nothing to it.
        if (role == OrganizationRole.Admin)
        {
            return codes;
        }

        foreach (var grant in reaching)
        {
            Add(codes, ScopedRoleBundles.CodesOf(grant.GrantScope, grant.Role), target);
        }

        return codes;
    }

    /// <summary>
    /// Whether the caller holds <paramref name="code"/> on an object of <paramref name="target"/>. A code of another resource
    /// scope than the object's is never held there (a mis-declared route is a permanent 403, which the inventory catches).
    /// </summary>
    public static bool Holds(OrganizationRole role, ResourceScope target, IEnumerable<ReachingGrant> reaching, string code) =>
        CodesOn(role, target, reaching).Contains(code);

    private static void Add(HashSet<string> codes, IReadOnlySet<string> bundle, ResourceScope target)
    {
        foreach (var code in bundle)
        {
            if (PermissionCatalog.ResourceScopeOf(code) == target)
            {
                codes.Add(code);
            }
        }
    }
}
