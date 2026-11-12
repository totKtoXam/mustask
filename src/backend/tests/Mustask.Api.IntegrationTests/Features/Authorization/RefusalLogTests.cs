using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Authorization;

/// <summary>
/// mod-019 NFR Observability (review C-2): each refusal is logged once with the route, the code, the resource scope and the user id —
/// a policy refusal and a 403 raised inside a handler alike — and never with a name or a token.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class RefusalLogTests(MustaskApiFactory factory)
{
    private static List<string> RefusalLines(CapturingLoggerProvider logs) =>
        logs.Entries.Where(entry => entry.Message.StartsWith("Access refused", StringComparison.Ordinal)).Select(entry => entry.Message).ToList();

    private WebApplicationFactoryHost HostWith(CapturingLoggerProvider logs) =>
        new(factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<ILoggerProvider>(logs))));

    // A policy refusal: a viewer gives a grant on a project (scope Project, code project.grant): one line.
    [Fact]
    public async Task APolicyRefusal_LogsOneLine_WithRouteCodeScopeAndUser()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var viewer = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Viewer);
        var logs = new CapturingLoggerProvider();
        await using var host = HostWith(logs);

        var answer = await OrganizationsApi.SendAsync(
            host.Factory, HttpMethod.Post, $"{OrganizationsApi.Project(structure.OrganizationId, structure.ProjectId)}/grants", new { userId = viewer.Id, role = "reader" }, viewer.Token);

        Assert.Equal(HttpStatusCode.Forbidden, answer.Status);
        var line = Assert.Single(RefusalLines(logs));
        Assert.Contains("RoleLacksCode", line, StringComparison.Ordinal);
        Assert.Contains("/projects/{projectId}/grants", line, StringComparison.Ordinal);
        Assert.Contains("Project", line, StringComparison.Ordinal);
        Assert.Contains(viewer.Id, line, StringComparison.Ordinal);
        Assert.Contains(structure.OrganizationId.ToString(), line, StringComparison.Ordinal);
        Assert.DoesNotContain(viewer.Token, line, StringComparison.Ordinal);
    }

    // A 403 raised inside the handler (the place of a new project is judged there): still one line, with the route and the user.
    [Fact]
    public async Task AHandlerRefusal_LogsOneLine_WithRouteCodeScopeAndUser()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var viewer = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Viewer);
        var logs = new CapturingLoggerProvider();
        await using var host = HostWith(logs);

        var answer = await OrganizationsApi.SendAsync(
            host.Factory, HttpMethod.Post, $"/api/organizations/{organization.Id}/projects", new { name = "Проект читателя", key = "RLG" }, viewer.Token);

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (answer.Status, answer.ErrorCode));
        var line = Assert.Single(RefusalLines(logs));
        Assert.Contains("RaisedInHandler", line, StringComparison.Ordinal);
        Assert.Contains("/api/organizations/{organizationId}/projects", line, StringComparison.Ordinal);
        Assert.Contains(viewer.Id, line, StringComparison.Ordinal);
        Assert.Contains(organization.Id.ToString(), line, StringComparison.Ordinal);
        Assert.Contains("organizations.access.forbidden", line, StringComparison.Ordinal);
        Assert.DoesNotContain("Проект читателя", line, StringComparison.Ordinal);
    }

    /// <summary>Disposes the derived host with the test.</summary>
    private sealed class WebApplicationFactoryHost(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> inner) : IAsyncDisposable
    {
        public Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory => inner;

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
