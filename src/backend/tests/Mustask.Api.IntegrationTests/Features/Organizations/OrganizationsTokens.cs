using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// Access tokens for the organizations tests (mod-002 plan-backend § 14.1 step
/// 5): with the create right <c>organizations.organization.create</c>, or a plain
/// signed-in user without it (BR-M1-19). mod-016 (plan-backend § 4.8): a plain
/// <see cref="SignedIn"/> is <see cref="OrganizationsTestData.Creator"/>, the
/// administrator of every organization <see cref="OrganizationsTestData"/> builds,
/// so a test that built its data there needs no membership of its own; a test that
/// needs a member of another role, or nobody, says so (<see cref="MembershipTestData"/>,
/// <see cref="Stranger"/>).
/// </summary>
public static class OrganizationsTokens
{
    public static string NewSubject() => Guid.CreateVersion7().ToString();

    /// <summary>A user holding the create right, as the administrator or a member of «Создатели организаций» does; a fresh subject unless named.</summary>
    public static string WithCreateRight(string? subject = null) =>
        KeycloakTestRealm.CreateToken(
            [PermissionCatalog.OrganizationsOrganizationCreate],
            options => options.Subject = subject ?? NewSubject());

    /// <summary>
    /// A signed-in user with no token-carried right: the subject named, or <see cref="OrganizationsTestData.Creator"/> —
    /// the administrator of every organization the test data builds.
    /// </summary>
    public static string SignedIn(string? subject = null) =>
        KeycloakTestRealm.CreateToken([], options => options.Subject = subject ?? OrganizationsTestData.Creator);

    /// <summary>A signed-in user nobody has added to anything: a fresh subject.</summary>
    public static string Stranger() => SignedIn(NewSubject());
}
