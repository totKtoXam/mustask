using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// Access tokens for the organizations tests (mod-002 plan-backend § 14.1 step
/// 5), each with a fresh subject unless the test names one: with the create
/// right <c>organizations.organization.create</c>, or a plain signed-in user
/// without it (BR-M1-19).
/// </summary>
public static class OrganizationsTokens
{
    public static string NewSubject() => Guid.CreateVersion7().ToString();

    /// <summary>A user holding the create right, as the administrator or a member of «Создатели организаций» does.</summary>
    public static string WithCreateRight(string? subject = null) =>
        KeycloakTestRealm.CreateToken(
            [PermissionCatalog.OrganizationsOrganizationCreate],
            options => options.Subject = subject ?? NewSubject());

    /// <summary>A signed-in user with no right at all: until mod-016, they may do everything but create an organization.</summary>
    public static string SignedIn(string? subject = null) =>
        KeycloakTestRealm.CreateToken([], options => options.Subject = subject ?? NewSubject());
}
