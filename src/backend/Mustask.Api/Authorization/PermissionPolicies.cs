using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Mustask.Api.Authorization;

/// <summary>
/// Политики по кодам прав создаются лениво (`perm:&lt;код&gt;`) — новый код
/// права не требует правки регистрации DI. Плюс именованная политика
/// «достаточно аутентификации» и deny-all fallback.
/// <para>
/// mod-016 (ADR-0027): two more lazily built families for the organization-scoped
/// check — <c>org:&lt;code&gt;</c> and <c>org+restore:&lt;code&gt;</c> (the route that also
/// accepts a deactivated organization). Each is "authenticated, and the caller's
/// membership in the organization of the address holds the code"
/// (<see cref="OrganizationPermissionRequirement"/>). The code of an organization
/// route is never a claim of the token.
/// </para>
/// </summary>
public sealed class PermissionPolicies : IAuthorizationPolicyProvider
{
    public const string AuthenticatedOnlyPolicyName = "AuthenticatedOnly";
    private const string PermissionPolicyPrefix = "perm:";
    private const string OrganizationPolicyPrefix = "org:";
    private const string OrganizationRestorePolicyPrefix = "org+restore:";
    private const string PermissionClaimType = "perm";

    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicies(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public static string PolicyName(string permissionCode) => PermissionPolicyPrefix + permissionCode;

    /// <summary>The policy name of an organization-scoped route (mod-016): the code, and whether a deactivated organization is accepted.</summary>
    public static string OrganizationPolicyName(string permissionCode, bool acceptsDeactivated) =>
        (acceptsDeactivated ? OrganizationRestorePolicyPrefix : OrganizationPolicyPrefix) + permissionCode;

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PermissionPolicyPrefix, StringComparison.Ordinal))
        {
            var code = policyName[PermissionPolicyPrefix.Length..];
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(PermissionClaimType, code)
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        if (policyName.StartsWith(OrganizationPolicyPrefix, StringComparison.Ordinal))
        {
            return Task.FromResult<AuthorizationPolicy?>(
                OrganizationPolicy(policyName[OrganizationPolicyPrefix.Length..], acceptsDeactivated: false));
        }

        if (policyName.StartsWith(OrganizationRestorePolicyPrefix, StringComparison.Ordinal))
        {
            return Task.FromResult<AuthorizationPolicy?>(
                OrganizationPolicy(policyName[OrganizationRestorePolicyPrefix.Length..], acceptsDeactivated: true));
        }

        return _fallback.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    private static AuthorizationPolicy OrganizationPolicy(string code, bool acceptsDeactivated) =>
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new OrganizationPermissionRequirement(code, acceptsDeactivated))
            .Build();
}
