using System.Net;
using System.Text.Json;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/description</c> (feat-028 api-contract § 0.2, § 2.4; US-2 "edit a
/// project's description") through the real host and a real PostgreSQL: kept exactly as typed, cleared by the empty string, <c>null</c>
/// and an absent field, refused over 2 000 characters, and surviving a deactivation and a restore. Every test builds its own organization.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SetProjectDescriptionEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> DescribeAsync(Guid organizationId, string projectId, object? body, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"/api/organizations/{organizationId}/projects/{Uri.EscapeDataString(projectId)}/description",
            body,
            token ?? OrganizationsTokens.SignedIn());

    private Task<Answer> DescribeAsync(OrganizationStructure structure, object? body, string? token = null) =>
        DescribeAsync(structure.OrganizationId, structure.ProjectId.ToString(), body, token);

    private Task<string?> StoredAsync(Guid projectId) => ProjectDescriptionAsync(factory, projectId);

    // feat-028 US-2 · "Describe_SavesClearsAndKeepsAsTyped": the text is kept as typed and the answer is the `Project` of § 1.4 with it.
    [Fact]
    public async Task Describe_KeptAsTyped_AnswersTheProject()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var typed = "  Единая платформа\nи её инструменты  ";

        var answer = await DescribeAsync(structure, new { description = typed });
        var read = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, OrganizationsApi.Project(structure.OrganizationId, structure.ProjectId), token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(["description", "id", "key", "name", "organizationId"], PropertyNames(answer.Body));
        Assert.Equal(structure.ProjectId, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal(typed, answer.Body.GetProperty("description").GetString());
        Assert.Equal(typed, read.Body.GetProperty("description").GetString());
        Assert.Equal(typed, await StoredAsync(structure.ProjectId));
    }

    // feat-028 § 0.2 · the empty string, null, `{}` and no body all clear it.
    [Theory]
    [InlineData("empty")]
    [InlineData("null")]
    [InlineData("absent")]
    [InlineData("nobody")]
    public async Task Describe_ClearedByEmptyNullAbsentAndNoBody(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await DescribeAsync(structure, new { description = "Было" });
        object? body = how switch
        {
            "empty" => new { description = "" },
            "null" => new { description = (string?)null },
            "absent" => new { },
            _ => null,
        };

        var answer = await DescribeAsync(structure, body);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(JsonValueKind.Null, answer.Body.GetProperty("description").ValueKind);
        Assert.Null(await StoredAsync(structure.ProjectId));
    }

    // feat-028 § 0.2 · "Describe_Over2000_Returns400": 2 000 passes, 2 001 is refused under `description`, the stored text is unchanged.
    [Fact]
    public async Task Describe_Over2000_Returns400_Unchanged_2000Passes()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await DescribeAsync(structure, new { description = "Было" });

        var refused = await DescribeAsync(structure, new { description = new string('я', 2001) });
        Assert.Equal("Было", await StoredAsync(structure.ProjectId));
        var longest = await DescribeAsync(structure, new { description = new string('я', 2000) });

        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);
        Assert.Equal("organizations.validation_failed", refused.ErrorCode);
        Assert.Equal("organizations.description_length", ReasonOf(refused, "description"));
        Assert.Equal(HttpStatusCode.OK, longest.Status);
    }

    // feat-028 US-2 · "Describe_SurvivesDeactivateAndRestore": the description, the links and the setting are untouched by a
    // deactivation and a restore, and a write meanwhile answers project.not_found.
    [Fact]
    public async Task Describe_SurvivesDeactivateAndRestore()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await DescribeAsync(structure, new { description = "Платформа" });
        await OrganizationsTestData.SetDefaultTeamAsync(factory, structure.OrganizationId, structure.ProjectId, structure.Platform.Id);
        var links = (await LinksOfProjectAsync(factory, structure.ProjectId)).Select(link => link.TeamId).ToList();

        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);
        var whileDeactivated = await DescribeAsync(structure, new { description = "Другое" });
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Active);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.project.not_found"), (whileDeactivated.Status, whileDeactivated.ErrorCode));
        Assert.Equal("Платформа", await StoredAsync(structure.ProjectId));
        Assert.Equal(links, (await LinksOfProjectAsync(factory, structure.ProjectId)).Select(link => link.TeamId).ToList());
        Assert.Equal(structure.Platform.Id, await SettingAsync(factory, structure.ProjectId));
    }

    // feat-028 US-2 · roles: organizations.project.write is the administrator's alone; the 403 comes before the project and the body.
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Describe_MemberAndViewer_Return403_ChangeNothing(OrganizationRole role)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, role);

        var answer = await DescribeAsync(structure, new { description = "Новое" }, caller.Token);
        var missing = await DescribeAsync(structure.OrganizationId, Guid.CreateVersion7().ToString(), new { description = "Новое" }, caller.Token);

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (answer.Status, answer.ErrorCode));
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (missing.Status, missing.ErrorCode));
        Assert.Null(await StoredAsync(structure.ProjectId));
    }

    // US-1 · "A visitor who is not signed in sees nothing".
    [Fact]
    public async Task Describe_NoToken_Returns401()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Put, $"{OrganizationsApi.Project(structure.OrganizationId, structure.ProjectId)}/description", new { description = "Новое" });

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
    }

    // api-contract § 0.4 · a project that is malformed, unknown, another organization's or deactivated answers project.not_found
    // before the body is looked at.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task Describe_ProjectNotOfTheOrganization_Returns404ProjectNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var projectId = how switch
        {
            "malformed" => "PRM",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => other.ProjectId.ToString(),
        };

        var answer = await DescribeAsync(structure.OrganizationId, projectId, new { description = new string('я', 2001) });

        Assert.Equal((HttpStatusCode.NotFound, "organizations.project.not_found"), (answer.Status, answer.ErrorCode));
        Assert.Null(await StoredAsync(other.ProjectId));
    }
}
