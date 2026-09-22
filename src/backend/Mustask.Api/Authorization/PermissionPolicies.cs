using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Mustask.Api.Authorization;

/// <summary>
/// Политики по кодам прав создаются лениво (`perm:&lt;код&gt;`) — новый код
/// права не требует правки регистрации DI. Плюс именованная политика
/// «достаточно аутентификации» и deny-all fallback.
/// </summary>
public sealed class PermissionPolicies : IAuthorizationPolicyProvider
{
    public const string AuthenticatedOnlyPolicyName = "AuthenticatedOnly";
    private const string PermissionPolicyPrefix = "perm:";
    private const string PermissionClaimType = "perm";

    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicies(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public static string PolicyName(string permissionCode) => PermissionPolicyPrefix + permissionCode;

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

        return _fallback.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
}
