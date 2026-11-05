using System.Net;
using System.Text;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// mod-016 US-2 and US-4 (pass B) · the scenarios of the retrofit that are about <b>who</b> and <b>in which order</b> rather than
/// about one route: the order of the answers on routes that already existed, the deactivated lists and restores that are the
/// administrators', and the instance code that opens no organization. The routes themselves are covered one by one by
/// <c>OrganizationAccessInventoryTests</c> (every route with every role) and <c>ScopedAccessTests</c> (404 and 401 on every route).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class RetrofitAccessTests(MustaskApiFactory factory)
{
    private const string NotFoundCode = "organizations.organization.not_found";
    private const string ForbiddenCode = "organizations.access.forbidden";

    // US-2 · "The order of the answers": a viewer who writes to a project of A that does not exist gets 403 «Недостаточно прав»,
    // not «Проект не найден»; a member's read of that project answers «Проект не найден»; and a request with no token answers 401
    // before anything else.
    [Fact]
    public async Task Order_AViewersWriteToAMissingProject_Is403_AMembersReadOfItIsProjectNotFound_NoTokenIs401()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, founded: false);
        var viewer = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Viewer);
        var member = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);
        var missing = $"{OrganizationsApi.Organization(organization.Id)}/projects/{Guid.CreateVersion7()}";
        var write = new { requestId = Guid.NewGuid().ToString(), typeId = Guid.NewGuid().ToString(), title = "Задача" };

        var viewerWrites = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{missing}/workitems", write, viewer.Token);
        var viewerRenames = await OrganizationsApi.SendAsync(factory, HttpMethod.Put, $"{missing}/name", new { name = "Новое" }, viewer.Token);
        var memberReads = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{missing}/workitems", token: member.Token);
        var memberWrites = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{missing}/workitems", write, member.Token);
        var noToken = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{missing}/workitems", write);

        Assert.Equal((HttpStatusCode.Forbidden, ForbiddenCode), (viewerWrites.Status, viewerWrites.ErrorCode));
        Assert.Equal((HttpStatusCode.Forbidden, ForbiddenCode), (viewerRenames.Status, viewerRenames.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, "organizations.project.not_found"), (memberReads.Status, memberReads.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, "organizations.project.not_found"), (memberWrites.Status, memberWrites.ErrorCode));
        Assert.Equal((HttpStatusCode.Unauthorized, "http.unauthorized"), (noToken.Status, noToken.ErrorCode));
    }

    // US-2 / api-contract § 0.1: the check runs before the body is read, on the routes that already existed too — a non-member who
    // sends malformed JSON to the work item create gets the 404, a viewer the 403, and a member the 400.
    [Theory]
    [InlineData("POST", "/api/organizations/{o}/projects/{p}/workitems")]
    [InlineData("PATCH", "/api/organizations/{o}/projects/{p}/workitems/MSK-TASK-1")]
    [InlineData("POST", "/api/organizations/{o}/projects/{p}/teams")]
    [InlineData("PUT", "/api/organizations/{o}/name")]
    public async Task MalformedBody_NonMemberGets404_ViewerGets403_BeforeTheBodyIsRead_MemberOrAdminGetsThe400(string method, string template)
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var viewer = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        var admin = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Admin);
        var path = template
            .Replace("{o}", world.OrganizationId.ToString(), StringComparison.Ordinal)
            .Replace("{p}", world.ProjectId.ToString(), StringComparison.Ordinal);

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

    // US-2 · "Deactivated lists and restore are the administrators'" (BR-M1-35, D-13): a user who is admin in A and member in B, both
    // with a deactivated project, opens A's deactivated projects and restores A's project; B's list and B's restore answer
    // «Недостаточно прав», and B's project stays deactivated.
    [Fact]
    public async Task DeactivatedProjects_AreTheAdministratorsOnly_AdminOfAOpensAndRestores_MemberOfBIsRefused()
    {
        var a = await OrganizationsTestData.CreateStructureAsync(factory, founded: false);
        var b = await OrganizationsTestData.CreateStructureAsync(factory, founded: false);
        var user = await MembershipTestData.NewMemberAsync(factory, a.OrganizationId, OrganizationRole.Admin);
        await MembershipTestData.AddMemberAsync(factory, b.OrganizationId, user.Id, OrganizationRole.Member);
        var aOld = await OrganizationsTestData.CreateProjectAsync(factory, a.OrganizationId, "Старый A", "OLA", ProjectStatus.Deactivated);
        var bOld = await OrganizationsTestData.CreateProjectAsync(factory, b.OrganizationId, "Старый B", "OLB", ProjectStatus.Deactivated);

        var listA = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{OrganizationsApi.Organization(a.OrganizationId)}/projects/deactivated", token: user.Token);
        var listB = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{OrganizationsApi.Organization(b.OrganizationId)}/projects/deactivated", token: user.Token);
        var restoreB = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{OrganizationsApi.Project(b.OrganizationId, bOld.Project.Id)}/restore", token: user.Token);
        var restoreA = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{OrganizationsApi.Project(a.OrganizationId, aOld.Project.Id)}/restore", token: user.Token);
        var readB = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{OrganizationsApi.Organization(b.OrganizationId)}/projects", token: user.Token);

        Assert.Equal(HttpStatusCode.OK, listA.Status);
        Assert.Contains(listA.Body.EnumerateArray(), item => item.GetProperty("id").GetGuid() == aOld.Project.Id);
        Assert.Equal((HttpStatusCode.Forbidden, ForbiddenCode), (listB.Status, listB.ErrorCode));
        Assert.Equal((HttpStatusCode.Forbidden, ForbiddenCode), (restoreB.Status, restoreB.ErrorCode));
        Assert.Equal(HttpStatusCode.OK, restoreA.Status);

        // B's project is still deactivated: it is not among B's active projects, and a member reads the active list as usual.
        Assert.Equal(HttpStatusCode.OK, readB.Status);
        Assert.DoesNotContain(readB.Body.EnumerateArray(), item => item.GetProperty("id").GetGuid() == bOld.Project.Id);
    }

    // US-2 · "member | rename or deactivate the organization" / "create, rename, deactivate or restore a project": a member is refused
    // each with 403, and the organization and its project are as they were.
    [Fact]
    public async Task Member_CannotRenameOrDeactivateTheOrganization_OrDeactivateAProject_NothingChanges()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory, founded: false);
        var member = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);
        var organization = OrganizationsApi.Organization(structure.OrganizationId);
        var project = OrganizationsApi.Project(structure.OrganizationId, structure.ProjectId);

        var attempts = new[]
        {
            await OrganizationsApi.SendAsync(factory, HttpMethod.Put, $"{organization}/name", new { name = "Взломанная" }, member.Token),
            await OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{organization}/deactivate", token: member.Token),
            await OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{project}/deactivate", token: member.Token),
            await OrganizationsApi.SendAsync(factory, HttpMethod.Put, $"{project}/name", new { name = "Взломанный" }, member.Token),
            await OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{organization}/projects", new { name = "Новый", key = "NEW" }, member.Token),
        };

        Assert.All(attempts, answer => Assert.Equal((HttpStatusCode.Forbidden, ForbiddenCode), (answer.Status, answer.ErrorCode)));

        var read = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, organization, token: member.Token);
        var readProject = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, project, token: member.Token);
        Assert.Equal(structure.Organization.Name, read.Body.GetProperty("name").GetString());
        Assert.Equal("Пример проекта", readProject.Body.GetProperty("name").GetString());
    }

    // US-6 · "Everyone else is a non-member until added" (D-3, BR-M1-25): an organization created before the migration has the
    // creator as its one administrator — seeded by the migration's own statement, run here on an organization built without a
    // membership — and everybody else gets «Организация не найдена» until the creator adds them on «Участники».
    [Fact]
    public async Task AnOrganizationCreatedBeforeTheMigration_IsOpenToItsCreatorAlone_UntilTheyAddOthers()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, founded: false);
        var stranger = MembershipTestData.NewUserId();
        await MembershipTestData.ProjectUserAsync(factory, stranger, "Пётр", "Новичков");
        var creatorToken = OrganizationsTokens.SignedIn();
        var strangerToken = OrganizationsTokens.SignedIn(stranger);

        // Before the seed nobody belongs to it — not even the creator the row records.
        var before = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, OrganizationsApi.Organization(organization.Id), token: creatorToken);
        Assert.Equal((HttpStatusCode.NotFound, NotFoundCode), (before.Status, before.ErrorCode));

        // The migration's own statement (plan-backend § 3.3): one admin per organization, the recorded creator, added by themselves.
        await WorkItems.WorkItemsTestData.ExecuteAsync(
            factory,
            $"INSERT INTO organization_memberships (organization_id, user_id, role, added_by, added_at) SELECT id, created_by, 'admin', created_by, created_at FROM organizations WHERE id = {organization.Id}");

        var creator = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, OrganizationsApi.Organization(organization.Id), token: creatorToken);
        var outsider = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, OrganizationsApi.Organization(organization.Id), token: strangerToken);
        var added = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{OrganizationsApi.Organization(organization.Id)}/members", new { userId = stranger, role = "viewer" }, creatorToken);
        var afterwards = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, OrganizationsApi.Organization(organization.Id), token: strangerToken);

        Assert.Equal(HttpStatusCode.OK, creator.Status);
        Assert.Equal((HttpStatusCode.NotFound, NotFoundCode), (outsider.Status, outsider.ErrorCode));
        Assert.Equal(HttpStatusCode.Created, added.Status);
        Assert.Equal(HttpStatusCode.OK, afterwards.Status);
    }

    // US-4 · "Instance codes still come from the token": the right to create an organization is an instance code, read from the token
    // — a user holding it and no membership creates an organization and administers it at once; the same right opens nothing of
    // another organization (404), and a user without it is refused with the create right's own code, not the scoped one.
    [Fact]
    public async Task TheCreateRight_ComesFromTheToken_AndOpensNoOrganization()
    {
        var other = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var subject = OrganizationsTokens.NewSubject();
        var token = OrganizationsTokens.WithCreateRight(subject);

        var created = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, "/api/organizations", new { name = OrganizationsTestData.UniqueName() }, token);
        var opensNothing = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, OrganizationsApi.Organization(other.Id), token: token);
        var withoutTheRight = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, "/api/organizations", new { name = OrganizationsTestData.UniqueName() }, OrganizationsTokens.SignedIn(subject));

        Assert.Equal(HttpStatusCode.Created, created.Status);
        var access = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, $"{OrganizationsApi.Organization(created.Body.GetProperty("id").GetGuid())}/access", token: token);
        Assert.Equal("admin", access.Body.GetProperty("role").GetString());
        Assert.Equal((HttpStatusCode.NotFound, NotFoundCode), (opensNothing.Status, opensNothing.ErrorCode));
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.organization.create_forbidden"), (withoutTheRight.Status, withoutTheRight.ErrorCode));
    }
}
