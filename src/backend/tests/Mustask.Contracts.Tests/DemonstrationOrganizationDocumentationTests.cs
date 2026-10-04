using Mustask.Api.Features.Process;

namespace Mustask.Contracts.Tests;

/// <summary>
/// US-2 (mod-001-process-seed) · "The Development stand shows the demonstration
/// process" (status.md D-4): the demonstration organization identifier lives in
/// two places, the constant the Development seed uses and the line of
/// <c>src/backend/AGENTS.md</c> that tells a developer which address to open.
/// This is the drift guard between them.
/// </summary>
public sealed class DemonstrationOrganizationDocumentationTests
{
    [Fact]
    public void BackendAgentsMd_NamesTheDemonstrationOrganizationId()
    {
        var guide = File.ReadAllText(Path.Combine(RepositoryLayout.Source.FullName, "AGENTS.md"));
        var id = DemonstrationProcessSeed.OrganizationId.ToString("D");

        Assert.Contains(id, guide, StringComparison.Ordinal);
        Assert.Contains($"/organizations/{id}/settings/process", guide, StringComparison.Ordinal);
        Assert.Contains($"GET /api/organizations/{id}/process", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void BackendAgentsMd_NamesTheThreeOutcomeEventsOfTheDemonstrationSeed()
    {
        var guide = File.ReadAllText(Path.Combine(RepositoryLayout.Source.FullName, "AGENTS.md"));

        foreach (var name in new[]
        {
            DemonstrationProcessSeed.ReadyEvent.Name,
            DemonstrationProcessSeed.SkippedEvent.Name,
            DemonstrationProcessSeed.FailedEvent.Name,
        })
        {
            Assert.Contains($"`{name}`", guide, StringComparison.Ordinal);
        }
    }
}
