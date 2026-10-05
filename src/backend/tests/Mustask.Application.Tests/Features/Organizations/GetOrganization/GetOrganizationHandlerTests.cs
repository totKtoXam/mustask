using Mustask.Application.Features.Organizations.GetOrganization;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.GetOrganization;

/// <summary>
/// <c>GET /api/organizations/{organizationId}</c> on fakes (api-contract § 1.4):
/// the organization as stored, and the organization level's not-found answer
/// (BR-M1-20, ADR-0026 rule 5). The handler answers it itself, besides the
/// organization-context filter that stands before it in the host.
/// </summary>
public sealed class GetOrganizationHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Handler = new GetOrganizationHandler(new FakeOrganizationReadModel(Store));
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public GetOrganizationHandler Handler { get; }

        public Task<GetOrganizationResult> HandleAsync(string? organizationId) =>
            Handler.Handle(new GetOrganizationQuery(organizationId), CancellationToken.None);
    }

    // US-2 · "Organization settings": the id and the name as stored, trimmed.
    [Fact]
    public async Task Handle_ActiveOrganization_ReturnsIdAndStoredName()
    {
        var f = new Fixture();
        var padded = f.Store.AddOrganization("  Организация «Ёлка»  ");

        var result = await f.HandleAsync(padded.Id.ToString());

        Assert.Equal(new OrganizationResponse(padded.Id, "Организация «Ёлка»"), result.Response);
    }

    // US-1 · "Every signed-in user sees every organization": the query carries no user and no right, so each organization
    // is served by its id alone; and a read takes no lock (plan-backend § 8.2).
    [Fact]
    public async Task Handle_SeveralOrganizations_ServesTheAddressedOneByIdAlone_TakingNoLock()
    {
        var f = new Fixture();
        var other = f.Store.AddOrganization("Чужая организация");

        var own = await f.HandleAsync(f.Organization.Id.ToString());
        var foreign = await f.HandleAsync(other.Id.ToString());

        Assert.Equal(new OrganizationResponse(f.Organization.Id, f.Organization.Name), own.Response);
        Assert.Equal(new OrganizationResponse(other.Id, "Чужая организация"), foreign.Response);
        Assert.Empty(f.Store.Calls);
    }

    // api-contract § 0.2: the canonical form is accepted in either letter case.
    [Fact]
    public async Task Handle_UpperCaseId_IsTheSameOrganization()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Organization.Id.ToString().ToUpperInvariant());

        Assert.Equal(f.Organization.Id, result.Response.Id);
    }

    // US-2 · "Renamed": the read shows the stored name now, not the one the organization was created with.
    [Fact]
    public async Task Handle_AfterRename_ReturnsTheNewName()
    {
        var f = new Fixture();

        f.Organization.Rename(InMemoryOrganizations.Name("Новое название"));
        var result = await f.HandleAsync(f.Organization.Id.ToString());

        Assert.Equal("Новое название", result.Response.Name);
    }

    // US-2 · "An unknown organization answers not found" (BR-M1-20): null, empty, padded, braced, undashed, all zeros and
    // a canonical id that names nothing all answer alike.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("unknown")]
    [InlineData("malformed-padded")]
    [InlineData("malformed-braced")]
    [InlineData("malformed-undashed")]
    public async Task Handle_MalformedOrUnknownId_IsOrganizationNotFound(string? how)
    {
        var f = new Fixture();
        var id = f.Organization.Id;
        var organizationId = how switch
        {
            "unknown" => Guid.CreateVersion7().ToString(),
            "malformed-padded" => $" {id} ",
            "malformed-braced" => "{" + id + "}",
            "malformed-undashed" => id.ToString("N"),
            _ => how,
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(organizationId));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }

    // US-2 · "A deactivated organization answers not found" (ADR-0026 rule 5): never revealed as deactivated through an
    // addressed read, and the answer is the same as for an unknown id.
    [Fact]
    public async Task Handle_DeactivatedOrganization_IsOrganizationNotFound_ThenServedAgainWhenRestored()
    {
        var f = new Fixture();
        f.Organization.Deactivate();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Organization.Id.ToString()));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);

        f.Organization.Restore();
        var restored = await f.HandleAsync(f.Organization.Id.ToString());

        Assert.Equal(f.Organization.Id, restored.Response.Id);
    }
}
