using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Authorization;

/// <summary>
/// mod-019 review C-7 (audit): a grant given, changed and revoked each leaves one information line with the actor, the grantee, the
/// scope, the object and the role — ids only, no name.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GrantAuditLogTests(MustaskApiFactory factory)
{
    [Fact]
    public async Task GivingChangingAndRevokingAGrant_EachLogsOneLine_WithActorGranteeScopeObjectAndRole()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var admin = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Admin);
        var vera = await WorkItemsTestData.AddUserAsync(factory, "Вера", "Аудит");
        await MembershipTestData.AddMemberAsync(factory, structure.OrganizationId, vera, OrganizationRole.Viewer);
        var logs = new CapturingLoggerProvider();
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<ILoggerProvider>(logs)));
        var path = $"{OrganizationsApi.Project(structure.OrganizationId, structure.ProjectId)}/grants";

        Assert.Equal(HttpStatusCode.Created, (await OrganizationsApi.SendAsync(host, HttpMethod.Post, path, new { userId = vera, role = "member" }, admin.Token)).Status);
        Assert.Equal(HttpStatusCode.OK, (await OrganizationsApi.SendAsync(host, HttpMethod.Put, $"{path}/{vera}", new { role = "manager" }, admin.Token)).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await OrganizationsApi.SendAsync(host, HttpMethod.Delete, $"{path}/{vera}", null, admin.Token)).Status);

        var lines = logs.Entries.Select(entry => entry.Message).Where(message => message.StartsWith("Grant ", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, lines.Count);
        Assert.Equal(["Grant given", "Grant changed", "Grant revoked"], lines.Select(line => line[..line.IndexOf(':', StringComparison.Ordinal)]));

        foreach (var line in lines)
        {
            Assert.Contains(structure.ProjectId.ToString(), line, StringComparison.Ordinal);
            Assert.Contains(structure.OrganizationId.ToString(), line, StringComparison.Ordinal);
            Assert.Contains(vera, line, StringComparison.Ordinal);
            Assert.Contains(admin.Id, line, StringComparison.Ordinal);
            Assert.DoesNotContain("Вера", line, StringComparison.Ordinal);
        }

        Assert.Contains("Member", lines[0], StringComparison.Ordinal);
        Assert.Contains("Manager", lines[1], StringComparison.Ordinal);
    }
}
