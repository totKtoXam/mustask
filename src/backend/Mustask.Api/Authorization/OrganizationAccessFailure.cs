using Microsoft.AspNetCore.Authorization;

namespace Mustask.Api.Authorization;

/// <summary>Why the scoped check failed (api-contract § 0.1, rows 2 and 3).</summary>
public enum OrganizationAccessFailureKind
{
    /// <summary>
    /// The organization id is malformed, names no organization, names one that is not active (the restore excepted), or the
    /// caller holds no membership: <c>404 organizations.organization.not_found</c>, the same bytes for every cause.
    /// </summary>
    OrganizationNotFound,

    /// <summary>The caller is a member whose role bundle lacks the route's code: <c>403 organizations.access.forbidden</c>.</summary>
    RoleLacksCode,
}

/// <summary>
/// The failure reason <see cref="OrganizationPermissionHandler"/> hands to the authorization pipeline.
/// <see cref="ForbiddenErrorCodeResultHandler"/> reads it back from the failure and turns it into the business answer, so the
/// 404 and the 403 are rendered by the same code path (the exception handler, the catalog text) as every other business error.
/// </summary>
public sealed class OrganizationAccessFailure : AuthorizationFailureReason
{
    public OrganizationAccessFailure(IAuthorizationHandler handler, OrganizationAccessFailureKind kind, string organizationId, string code)
        : base(handler, kind.ToString())
    {
        Kind = kind;
        OrganizationId = organizationId;
        Code = code;
    }

    public OrganizationAccessFailureKind Kind { get; }

    /// <summary>The organization id for the log: the parsed id, or the word <c>malformed</c> — never user-typed text.</summary>
    public string OrganizationId { get; }

    /// <summary>The code the route required.</summary>
    public string Code { get; }
}
