using Microsoft.AspNetCore.Authorization;
using Mustask.Domain.Identity;

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

    /// <summary>
    /// A project, team or area the address (or a <c>teamId</c>/<c>departmentId</c> of its query) names is missing or hidden from the caller
    /// (doc-016 BR-TD-12..-14, ADR-0029 § 8 items 3-4): the business 404 of that object, before any 403, the same bytes for a hidden and a missing one.
    /// </summary>
    ObjectNotFound,
}

/// <summary>
/// The failure reason <see cref="OrganizationPermissionHandler"/> hands to the authorization pipeline.
/// <see cref="ForbiddenErrorCodeResultHandler"/> reads it back from the failure and turns it into the business answer, so the
/// 404 and the 403 are rendered by the same code path (the exception handler, the catalog text) as every other business error.
/// </summary>
public sealed class OrganizationAccessFailure : AuthorizationFailureReason
{
    public OrganizationAccessFailure(
        IAuthorizationHandler handler, OrganizationAccessFailureKind kind, string organizationId, string code, ResourceScope scope = ResourceScope.Organization, string? errorCode = null)
        : base(handler, kind.ToString())
    {
        Kind = kind;
        OrganizationId = organizationId;
        Code = code;
        Scope = scope;
        ErrorCode = errorCode;
    }

    public OrganizationAccessFailureKind Kind { get; }

    /// <summary>The organization id for the log: the parsed id, or the word <c>malformed</c> — never user-typed text.</summary>
    public string OrganizationId { get; }

    /// <summary>The code the route required.</summary>
    public string Code { get; }

    /// <summary>The kind of object the route addresses, for the log.</summary>
    public ResourceScope Scope { get; }

    /// <summary>The business error code of an <see cref="OrganizationAccessFailureKind.ObjectNotFound"/> failure.</summary>
    public string? ErrorCode { get; }
}
