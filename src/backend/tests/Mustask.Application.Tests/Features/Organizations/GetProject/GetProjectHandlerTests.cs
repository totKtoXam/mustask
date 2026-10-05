using Mustask.Application.Features.Organizations.GetProject;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.GetProject;

/// <summary>
/// <c>GetProject</c> on fakes: what the answer carries, and that every way a
/// project can be unreachable answers the project level's not-found code
/// (plan-backend § 5.3, api-contract § 0.3).
/// </summary>
public sealed class GetProjectHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Handler = new GetProjectHandler(new FakeProjectReadModel(Store));
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public GetProjectHandler Handler { get; }

        public GetProjectQuery Query(string? projectId = null, string? organizationId = null) =>
            new(organizationId ?? Organization.Id.ToString(), projectId ?? Project.Project.Id.ToString());

        public Task<GetProjectResult> HandleAsync(GetProjectQuery query) => Handler.Handle(query, CancellationToken.None);
    }

    // US-3 · "The project settings": its name and its key, read-only (BR-M1-23).
    [Fact]
    public async Task Handle_ReachableProject_ReturnsIdOrganizationNameAndKey()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Query());

        Assert.Equal(new ProjectResponse(f.Project.Project.Id, f.Organization.Id, "Пример проекта", "PRM"), result.Response);
    }

    // US-3 · "The project settings": the project addressed is the one answered, whatever else the organization holds.
    [Fact]
    public async Task Handle_SeveralProjects_ReturnsTheAddressedOneWithItsOwnKey()
    {
        var f = new Fixture();
        var second = f.Store.AddProject(f.Organization, "Платформа данных", "DATA");
        f.Store.AddProject(f.Organization, "Отчётность", "RPT");

        var result = await f.HandleAsync(f.Query(projectId: second.Project.Id.ToString().ToUpperInvariant()));

        Assert.Equal(new ProjectResponse(second.Project.Id, f.Organization.Id, "Платформа данных", "DATA"), result.Response);
    }

    // BR-M1-20: behind the filter the organization id is well formed; if it were not, the outermost level answers,
    // even when the project id is wrong too (api-contract § 0.3, "from the outside in").
    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound_EvenWithAMalformedProject()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Query(projectId: "abc", organizationId: "abc")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }

    // US-3 · "A project identifier that names no project of the organization" and "A project is deactivated" (BR-M1-20,
    // BR-M1-22, ADR-0026 rule 5): malformed, absent, unknown, of another organization, deactivated, or under a
    // deactivated organization, all answer the project level's code and never reveal a deactivation.
    [Theory]
    [InlineData("malformed")]
    [InlineData("absent")]
    [InlineData("all zeros")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deactivated")]
    [InlineData("under a deactivated organization")]
    public async Task Handle_ProjectNotReachable_IsProjectNotFound(string how)
    {
        var f = new Fixture();
        var other = f.Store.AddProject(f.Store.AddOrganization("Другая организация"), "Чужой", "OTH");
        var projectId = how switch
        {
            "malformed" => "PRM",
            "absent" => null,
            "all zeros" => Guid.Empty.ToString(),
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another organization" => other.Project.Id.ToString(),
            _ => f.Project.Project.Id.ToString(),
        };

        if (how == "deactivated")
        {
            f.Project.Project.Deactivate();
        }

        if (how == "under a deactivated organization")
        {
            f.Organization.Deactivate();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(
            () => f.HandleAsync(new GetProjectQuery(f.Organization.Id.ToString(), projectId)));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
    }

    // plan-backend § 8.2: a read takes no lock and writes nothing, so it never waits for a writer of the project.
    [Fact]
    public async Task Handle_TakesNoLockAndSavesNothing()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Query());

        Assert.Empty(f.Store.Calls);
    }

    // ADR-0026 rule 5, BR-M1-22: the address names the container, so the same project id under another organization
    // is not found, while under its own it still answers.
    [Fact]
    public async Task Handle_ProjectUnderAnotherOrganization_IsProjectNotFound_UnderItsOwnItAnswers()
    {
        var f = new Fixture();
        var otherOrganization = f.Store.AddOrganization("Другая организация");

        var refusal = await Assert.ThrowsAsync<NotFoundException>(
            () => f.HandleAsync(f.Query(organizationId: otherOrganization.Id.ToString())));
        var result = await f.HandleAsync(f.Query());

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Equal(f.Project.Project.Id, result.Response.Id);
    }
}
