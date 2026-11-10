using Microsoft.AspNetCore.Routing;
using Mustask.Domain.Identity;

namespace Mustask.Api.Authorization;

/// <summary>
/// The one refusal line (mod-019 NFR Observability): route, code, resource scope and user id, plus the organization and the reason.
/// Written once per refused request — by <see cref="ForbiddenErrorCodeResultHandler"/> for a policy refusal, and by the business
/// exception handler for a 403 raised inside a handler. Ids and codes only: no name, no body, no token.
/// </summary>
internal static class RefusalLog
{
    /// <summary>Set once the request's refusal is logged, so the exception handler does not write it a second time.</summary>
    internal const string LoggedKey = "Mustask.Api.Authorization.RefusalLogged";

    internal static void Write(
        ILogger logger, HttpContext context, string reason, string organization, string code, ResourceScope scope, string errorCode, string? route = null)
    {
        context.Items[LoggedKey] = true;

        logger.LogInformation(
            "Access refused: {Reason}, route {Route}, code {Code}, scope {Scope}, user {UserId}, organization {OrganizationId}, error {ErrorCode}",
            reason,
            route ?? RouteOf(context),
            code,
            scope,
            context.User.FindFirst("sub")?.Value ?? "anonymous",
            organization,
            errorCode);
    }

    /// <summary>The route pattern of the matched endpoint (the exception handler clears the endpoint, so the policy keeps it).</summary>
    internal static string RouteOf(HttpContext context) =>
        (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
}
