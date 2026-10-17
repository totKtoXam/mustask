using Mustask.Application.Features.Organizations.GetTeam;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.GetTeam;

/// <summary>
/// <c>GetTeam</c> on fakes (feat-028 api-contract § 2.1; US-1 "a team page does not depend on a project", US-8): one active team of the
/// organization, read at the organization's address. Rewritten from the project-nested form (mod-017 BR-OT-4, BR-OT-11; D-16).
/// </summary>
public sealed class GetTeamHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Mars = Store.AddTeam(Organization, "MARS");
            Handler = new GetTeamHandler(new FakeTeamReadModel(Store));
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public Team Mars { get; }

        public GetTeamHandler Handler { get; }

        public GetTeamQuery Query(string? teamId = null, string? organizationId = null) =>
            new(organizationId ?? Organization.Id.ToString(), teamId ?? Mars.Id.ToString());

        public Task<GetTeamResult> HandleAsync(GetTeamQuery query) => Handler.Handle(query, CancellationToken.None);
    }

    // US-1 · "a team page does not depend on a project": a team linked to nothing is read, with no project field.
    [Fact]
    public async Task Handle_ATeamLinkedToNothing_IsRead_WithItsDescription()
    {
        var f = new Fixture();
        Assert.True(EntityDescription.TryCreate("Платформа и инструменты", out var description));
        f.Mars.Describe(description);

        var result = await f.HandleAsync(f.Query());

        Assert.Equal(new TeamResponse(f.Mars.Id, "MARS", "Платформа и инструменты"), result.Response);
    }

    [Fact]
    public async Task Handle_RenamedTeam_ReturnsTheNewName()
    {
        var f = new Fixture();
        f.Mars.Rename(InMemoryOrganizations.Name("VENUS"));

        var result = await f.HandleAsync(f.Query());

        Assert.Equal("VENUS", result.Response.Name);
        Assert.Null(result.Response.Description);
    }

    [Fact]
    public async Task Handle_IdsInUpperCase_FindsTheTeam()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Query(f.Mars.Id.ToString().ToUpperInvariant(), f.Organization.Id.ToString().ToUpperInvariant()));

        Assert.Equal(f.Mars.Id, result.Response.Id);
    }

    // US-8 · a team of another organization, a deleted team, an unknown or malformed id: one answer.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("deleted")]
    [InlineData("of another organization")]
    public async Task Handle_TeamNotAnActiveTeamOfTheOrganization_IsTeamNotFound(string how)
    {
        var f = new Fixture();
        var foreign = f.Store.AddTeam(f.Store.AddOrganization("Другая организация"), "Чужая");
        var id = how switch
        {
            "malformed" => "not-a-team",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another organization" => foreign.Id.ToString(),
            _ => f.Mars.Id.ToString(),
        };

        if (how == "deleted")
        {
            f.Mars.Delete();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Query(id)));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }

    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound_BeforeTheTeam()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Query("not-a-team", "abc")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }
}
