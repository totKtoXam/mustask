using System.Net;
using System.Text;
using System.Text.Json;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// mod-016 US-2 · "Access is decided by the membership of the resource's organization" (ADR-0027, api-contract § 0.1): the scoped
/// check answers 401, then 404 for a non-member — the same answer, byte for byte, as for an organization that does not exist, is
/// malformed or deactivated — then 403 for a member whose role lacks the route's code, all before the request body is read, and
/// reads the membership on every request so that a role change applies on the next one. The 404, 401 and deactivated theories run
/// on <b>every</b> organization-addressed route; the 403 theories on the ten routes of the memberships, whose codes only an
/// administrator holds (the inventory test calls every route with every role, <c>OrganizationAccessInventoryTests</c>).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ScopedAccessTests(MustaskApiFactory factory)
{
    private const string NotFoundCode = "organizations.organization.not_found";
    private const string ForbiddenCode = "organizations.access.forbidden";

    /// <summary>Every organization-addressed route that exists, with its code.</summary>
    public static TheoryData<string, string, string> AllRoutes()
    {
        var data = new TheoryData<string, string, string>();

        foreach (var (method, template, code) in OrganizationAddressedRoutes.All)
        {
            data.Add(method, template, code);
        }

        return data;
    }

    /// <summary>Every route but the organization's restore, the one request that accepts a deactivated organization.</summary>
    public static TheoryData<string, string, string> RoutesRefusingADeactivatedOrganization()
    {
        var data = new TheoryData<string, string, string>();

        foreach (var (method, template, code) in OrganizationAddressedRoutes.All.Where(route => !route.AcceptsDeactivated))
        {
            data.Add(method, template, code);
        }

        return data;
    }

    /// <summary>The write routes of the memberships, whose codes only an administrator holds.</summary>
    public static TheoryData<string, string, string> WriteRoutes()
    {
        var data = new TheoryData<string, string, string>();

        foreach (var (method, template, code) in OrganizationAddressedRoutes.Memberships.Where(route => !route.Code.EndsWith(".read", StringComparison.Ordinal)))
        {
            data.Add(method, template, code);
        }

        return data;
    }

    private static object? Body(string method, string template) => (method, template) switch
    {
        ("POST", var t) when t.EndsWith("/members", StringComparison.Ordinal) && t.Contains("/teams/", StringComparison.Ordinal) => new { userId = "u-someone" },
        ("POST", var t) when t.EndsWith("/members", StringComparison.Ordinal) => new { userId = "u-someone", role = "member" },
        ("PUT", var t) when t.EndsWith("/role", StringComparison.Ordinal) => new { role = "viewer" },
        _ => OrganizationAddressedRoutes.Body(method),
    };

    private Task<Answer> SendAsync(string method, string template, string organizationId, string? token) =>
        OrganizationsApi.SendAsync(
            factory, new HttpMethod(method), OrganizationAddressedRoutes.Path(template, organizationId), Body(method, template), token);

    private async Task<Answer> SendRawAsync(string method, string template, string organizationId, string rawBody, string? token)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), OrganizationAddressedRoutes.Path(template, organizationId))
        {
            Content = new StringContent(rawBody, Encoding.UTF8, "application/json"),
        };

        if (token is not null)
        {
            request.WithBearer(token);
        }

        using var response = await factory.CreateClient().SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();

        return new Answer(response.StatusCode, response.Headers.Location, response.Content.Headers.ContentType?.MediaType, JsonDocument.Parse(text).RootElement.Clone());
    }

    /// <summary>The answer without what legitimately differs per request (<c>instance</c> echoes the path, <c>traceId</c> the request).</summary>
    private static string Shape(Answer answer)
    {
        var fields = answer.Body.EnumerateObject()
            .Where(property => property.Name is not ("instance" or "traceId"))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => $"{property.Name}={property.Value.GetRawText()}");

        return $"{(int)answer.Status} {answer.MediaType} {string.Join(';', fields)}";
    }

    // US-2 · "A non-member gets «Организация не найдена» at every depth": for each of the new routes, the answer for an organization
    // the caller holds no membership in is 404 «Организация не найдена.» — the same bytes (modulo the echoed path) as for an
    // organization that never existed and for a malformed id. Nothing about the organization's existence is told.
    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task NonMember_Gets404OrganizationNotFound_TheSameAsANeverExistingAndAMalformedOrganization(string method, string template, string code)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var stranger = OrganizationsTokens.Stranger();

        var nonMember = await SendAsync(method, template, organization.Id.ToString(), stranger);
        var neverExisted = await SendAsync(method, template, Guid.CreateVersion7().ToString(), stranger);
        var malformed = await SendAsync(method, template, "abc", stranger);
        var zeros = await SendAsync(method, template, Guid.Empty.ToString(), stranger);

        Assert.True(nonMember.Status == HttpStatusCode.NotFound, $"{method} {template} ({code}) answered {nonMember.Status} {nonMember.ErrorCode}.");
        Assert.Equal(NotFoundCode, nonMember.ErrorCode);
        Assert.Equal("Организация не найдена.", nonMember.Detail);
        Assert.Equal(Shape(nonMember), Shape(neverExisted));
        Assert.Equal(Shape(nonMember), Shape(malformed));
        Assert.Equal(Shape(nonMember), Shape(zeros));
    }

    // The same answer for a member of ANOTHER organization only, and for a deactivated organization the caller administers: the
    // check accepts a deactivated one on the organization's restore alone, and these are not the restore.
    [Theory]
    [MemberData(nameof(RoutesRefusingADeactivatedOrganization))]
    public async Task MemberOfAnotherOrganizationOnly_AndTheMemberOfADeactivatedOne_Get404OrganizationNotFound(string method, string template, string code)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var elsewhere = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var deactivated = await OrganizationsTestData.CreateOrganizationAsync(factory, status: OrganizationStatus.Deactivated);
        var user = await MembershipTestData.NewMemberAsync(factory, elsewhere.Id, OrganizationRole.Admin);
        await MembershipTestData.AddMemberAsync(factory, deactivated.Id, user.Id, OrganizationRole.Admin);

        var other = await SendAsync(method, template, organization.Id.ToString(), user.Token);
        var inactive = await SendAsync(method, template, deactivated.Id.ToString(), user.Token);

        foreach (var answer in new[] { other, inactive })
        {
            Assert.True(answer.Status == HttpStatusCode.NotFound, $"{method} {template} ({code}) answered {answer.Status} {answer.ErrorCode}.");
            Assert.Equal(NotFoundCode, answer.ErrorCode);
        }
    }

    // US-2 · "A member whose role lacks the code is refused": a member or a viewer on a write route gets 403 «Недостаточно прав»,
    // and nothing changes. Admins pass the check (what they then get is the operation's own answer).
    [Theory]
    [MemberData(nameof(WriteRoutes))]
    public async Task MemberAndViewer_OnAWriteRoute_Get403AccessForbidden_AdminPassesTheCheck(string method, string template, string code)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organizationId = structure.OrganizationId.ToString();
        var member = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);
        var viewer = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Viewer);
        var admin = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Admin);
        var membershipsBefore = await MembershipCountAsync(structure.OrganizationId);

        foreach (var caller in new[] { member, viewer })
        {
            var answer = await SendAsync(method, template, organizationId, caller.Token);

            Assert.True(answer.Status == HttpStatusCode.Forbidden, $"{method} {template} ({code}) answered {answer.Status} {answer.ErrorCode}.");
            Assert.Equal(ForbiddenCode, answer.ErrorCode);
            Assert.Equal("Недостаточно прав", answer.Detail);
        }

        var admitted = await SendAsync(method, template, organizationId, admin.Token);

        Assert.NotEqual(HttpStatusCode.Forbidden, admitted.Status);
        Assert.True(admitted.ErrorCode != NotFoundCode || template.Contains("{projectId}", StringComparison.Ordinal), "An admin is never 'not found' at the organization level.");
        Assert.Equal(membershipsBefore, await MembershipCountAsync(structure.OrganizationId));
    }

    // US-2 · the read routes: every role holds the read codes, so a viewer reads the access, the members and the team members.
    [Theory]
    [InlineData(OrganizationRole.Admin)]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task EveryRole_ReadsTheAccessTheMembersAndTheTeamMembers(OrganizationRole role)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, role);
        var org = OrganizationsApi.Organization(structure.OrganizationId);
        var team = TeamTestData.Team(structure.OrganizationId, structure.Platform.Id);

        foreach (var path in new[] { $"{org}/access", $"{org}/members", $"{team}/members" })
        {
            var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: caller.Token);

            Assert.True(answer.Status == HttpStatusCode.OK, $"GET {path} answered {answer.Status} {answer.ErrorCode} to a {role}.");
        }
    }

    // US-2 · "The order of the answers": a request with no token answers 401 before anything, whatever the organization id says.
    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task NoToken_Gets401_BeforeAnything(string method, string template, string code)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        foreach (var organizationId in new[] { organization.Id.ToString(), "abc", Guid.CreateVersion7().ToString() })
        {
            var answer = await SendAsync(method, template, organizationId, token: null);

            Assert.True(answer.Status == HttpStatusCode.Unauthorized, $"{method} {template} ({code}) answered {answer.Status} to no token.");
            Assert.Equal("http.unauthorized", answer.ErrorCode);
        }
    }

    // api-contract § 0.1: the check runs before the body is read — a non-member who sends a malformed JSON body gets the 404, not
    // the 400; a member whose role lacks the code gets the 403, not the 400. An admin gets the 400.
    [Theory]
    [InlineData("POST", "/api/organizations/{organizationId}/members")]
    [InlineData("PUT", "/api/organizations/{organizationId}/members/{userId}/role")]
    [InlineData("POST", "/api/organizations/{organizationId}/teams/{teamId}/members")]
    public async Task MalformedBody_NonMemberGets404_ViewerGets403_BeforeTheBodyIsRead(string method, string template)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organizationId = structure.OrganizationId.ToString();
        var viewer = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Viewer);
        var admin = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Admin);
        var path = template
            .Replace("{organizationId}", organizationId, StringComparison.Ordinal)
            .Replace("{projectId}", structure.ProjectId.ToString(), StringComparison.Ordinal)
            .Replace("{teamId}", structure.Platform.Id.ToString(), StringComparison.Ordinal)
            .Replace("{userId}", admin.Id, StringComparison.Ordinal);

        async Task<HttpStatusCode> StatusAsync(string token)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path)
            {
                Content = new StringContent("{ this is not json", Encoding.UTF8, "application/json"),
            };
            request.WithBearer(token);
            using var response = await factory.CreateClient().SendAsync(request);

            return response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(MembershipTestData.Outsider()));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(viewer.Token));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(admin.Token));
    }

    // api-contract § 0.1: "Instance administrators hold the instance codes only. With no membership they get 404 on everything
    // organization-scoped." A token carrying every code of the catalog — instance and organization — is still a non-member's.
    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task ATokenCarryingEveryCode_ButNoMembership_Gets404(string method, string template, string code)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var everything = KeycloakTestRealm.CreateToken([.. PermissionCatalog.AllCodes], options => options.Subject = OrganizationsTokens.NewSubject());

        var answer = await SendAsync(method, template, organization.Id.ToString(), everything);

        Assert.True(answer.Status == HttpStatusCode.NotFound, $"{method} {template} ({code}) answered {answer.Status} {answer.ErrorCode}.");
        Assert.Equal(NotFoundCode, answer.ErrorCode);
    }

    // doc-006 US-4 scenario 4 · "No token-carried role grants an organization code": a viewer whose token carries the code is
    // still refused — the code is derived from the membership, never read from the token.
    [Fact]
    public async Task AViewersTokenCarryingTheCode_StillGets403()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var viewerId = MembershipTestData.NewUserId();
        await MembershipTestData.AddMemberAsync(factory, organization.Id, viewerId, OrganizationRole.Viewer);
        var token = KeycloakTestRealm.CreateToken(
            [PermissionCatalog.OrganizationsMembershipWrite, PermissionCatalog.OrganizationsTeamWrite],
            options => options.Subject = viewerId);

        var answer = await SendAsync("POST", "/api/organizations/{organizationId}/members", organization.Id.ToString(), token);

        Assert.Equal(HttpStatusCode.Forbidden, answer.Status);
        Assert.Equal(ForbiddenCode, answer.ErrorCode);
    }

    // US-2 / doc-006 US-4 scenario 7 · "a granted role works on the next request, with no re-login", and (BR-M1-6) a withdrawn one
    // stops working on the next: the same token, the membership read on every request.
    [Fact]
    public async Task ARoleChange_AppliesOnTheNextRequest_WithTheSameToken()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var user = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Viewer);
        var candidate = $"/api/organizations/{organization.Id}/member-candidates?text=ab";

        var asViewer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, candidate, token: user.Token);
        await SetRoleAsync(organization.Id, user.Id, OrganizationRole.Admin);
        var asAdmin = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, candidate, token: user.Token);
        await SetRoleAsync(organization.Id, user.Id, OrganizationRole.Member);
        var asMember = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, candidate, token: user.Token);
        await MembershipTestData.RemoveMemberAsync(factory, organization.Id, user.Id);
        var removed = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, candidate, token: user.Token);

        Assert.Equal(HttpStatusCode.Forbidden, asViewer.Status);
        Assert.Equal(HttpStatusCode.OK, asAdmin.Status);
        Assert.Equal(HttpStatusCode.Forbidden, asMember.Status);
        Assert.Equal(HttpStatusCode.NotFound, removed.Status);
        Assert.Equal(NotFoundCode, removed.ErrorCode);
    }

    // BR-M1-32 / mod-014 BR-UP-12: access is read from the membership alone — a member whom the user projection has never received
    // administers like any other, and the projection's status of a member does not change an access answer.
    [Fact]
    public async Task AccessIsReadFromTheMembershipAlone_NotFromTheUserProjection()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var unknown = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Admin);
        var disabled = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Admin);
        await MembershipTestData.ProjectUserAsync(factory, disabled.Id, "Отключённый", "Администратор", active: false);

        foreach (var caller in new[] { unknown, disabled })
        {
            var answer = await OrganizationsApi.SendAsync(
                factory, HttpMethod.Get, $"{OrganizationsApi.Organization(organization.Id)}/members", token: caller.Token);

            Assert.Equal(HttpStatusCode.OK, answer.Status);
        }
    }

    // US-2 (mod-002 US-2, BR-M1-20) · "A deactivated organization takes no change and answers not found": its own administrator,
    // on every route but its restore, gets the one not-found answer, and the organization is left exactly as it was.
    [Theory]
    [MemberData(nameof(RoutesRefusingADeactivatedOrganization))]
    public async Task EveryRoute_ADeactivatedOrganization_AnswersNotFoundToItsAdministrator_ChangesNothing(string method, string template, string code)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, status: OrganizationStatus.Deactivated);

        var answer = await SendAsync(method, template, organization.Id.ToString(), OrganizationsTokens.SignedIn());

        Assert.True(answer.Status == HttpStatusCode.NotFound, $"{method} {template} ({code}) answered {answer.Status} {answer.ErrorCode}.");
        Assert.Equal(NotFoundCode, answer.ErrorCode);
        Assert.Equal("Организация не найдена.", answer.Detail);

        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<Mustask.Application.Features.Organizations.IOrganizationStructure>()
            .FindOrganizationAsync(organization.Id, CancellationToken.None);
        Assert.Equal((organization.Name, OrganizationStatus.Deactivated), (stored!.Name, stored.Status));
    }

    // US-2 · "the restore of the organization, when it is deactivated" (BR-M1-35): the one request a deactivated organization
    // accepts — its administrator restores it, a member of it is refused with 403 (the role lacks the deactivate code) and a
    // non-member is told it does not exist; the organization changes only for the administrator.
    [Fact]
    public async Task Restore_OfADeactivatedOrganization_IsTheAdministrators_ANonMemberGets404_AMemberGets403()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, status: OrganizationStatus.Deactivated, founded: false);
        var admin = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Admin);
        var member = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);

        var asStranger = await SendAsync("POST", "/api/organizations/{organizationId}/restore", organization.Id.ToString(), MembershipTestData.Outsider());
        var asMember = await SendAsync("POST", "/api/organizations/{organizationId}/restore", organization.Id.ToString(), member.Token);
        Assert.Equal((HttpStatusCode.NotFound, NotFoundCode), (asStranger.Status, asStranger.ErrorCode));
        Assert.Equal((HttpStatusCode.Forbidden, ForbiddenCode), (asMember.Status, asMember.ErrorCode));
        Assert.Equal(OrganizationStatus.Deactivated, await StatusOfAsync(organization.Id));

        var asAdmin = await SendAsync("POST", "/api/organizations/{organizationId}/restore", organization.Id.ToString(), admin.Token);

        Assert.Equal(HttpStatusCode.OK, asAdmin.Status);
        Assert.Equal(OrganizationStatus.Active, await StatusOfAsync(organization.Id));
    }

    private async Task<OrganizationStatus> StatusOfAsync(Guid organizationId)
    {
        using var scope = factory.Services.CreateScope();

        return (await scope.ServiceProvider.GetRequiredService<Mustask.Application.Features.Organizations.IOrganizationStructure>()
            .FindOrganizationAsync(organizationId, CancellationToken.None))!.Status;
    }

    private async Task SetRoleAsync(Guid organizationId, string userId, OrganizationRole role) =>
        await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var memberships = services.GetRequiredService<IMembershipRepository>();
            var membership = await memberships.LockMemberAsync(organizationId, userId, ct);
            membership!.ChangeRole(role, OrganizationsTestData.Creator, OrganizationsTestData.Now);
            await memberships.SaveAsync(ct);

            return true;
        });

    private async Task<long> MembershipCountAsync(Guid organizationId)
    {
        await using var connection = new Npgsql.NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            "SELECT (SELECT count(*) FROM organization_memberships WHERE organization_id = @org) "
            + "+ (SELECT count(*) FROM team_memberships WHERE organization_id = @org)",
            connection);
        command.Parameters.AddWithValue("org", organizationId);

        return (long)(await command.ExecuteScalarAsync())!;
    }
}
