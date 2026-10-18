using Mustask.Application.Features.Organizations.SetTeamDescription;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.SetTeamDescription;

/// <summary>
/// <c>SetTeamDescription</c> on fakes (feat-028 api-contract § 2.1; US-1 "rename and description", BR-OL-3): kept as typed, cleared by
/// the empty string and null, refused over 2 000 characters; the team locked <c>FOR NO KEY UPDATE</c>.
/// </summary>
public sealed class SetTeamDescriptionHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Mars = Store.AddTeam(Organization, "MARS");
            Handler = new SetTeamDescriptionHandler(new FakeTeamRepository(Store), new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public Team Mars { get; }

        public SetTeamDescriptionHandler Handler { get; }

        public Task<SetTeamDescriptionResult> HandleAsync(string? description, string? teamId = null) =>
            Handler.Handle(new SetTeamDescriptionCommand(Organization.Id.ToString(), teamId ?? Mars.Id.ToString(), description), CancellationToken.None);
    }

    [Fact]
    public async Task Describe_KeepsTheTextAsTyped_AnswersTheTeam()
    {
        var f = new Fixture();

        var result = await f.HandleAsync("  Платформа\nи инструменты ");

        Assert.Equal(new TeamResponse(f.Mars.Id, "MARS", "  Платформа\nи инструменты "), result.Response);
        Assert.Equal("  Платформа\nи инструменты ", f.Mars.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Describe_EmptyStringAndNull_ClearIt(string? cleared)
    {
        var f = new Fixture();
        await f.HandleAsync("текст");

        var result = await f.HandleAsync(cleared);

        Assert.Null(result.Response.Description);
        Assert.Null(f.Mars.Description);
    }

    [Fact]
    public async Task Describe_Exactly2000_IsAccepted_2001_IsValidationFailed_Unchanged()
    {
        var f = new Fixture();
        await f.HandleAsync(new string('я', 2000));

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(new string('я', 2001)));

        Assert.Equal(OrganizationErrorCodes.DescriptionLength, Assert.Single(refusal.Errors["description"]).Code);
        Assert.Equal(2000, f.Mars.Description!.Length);
    }

    [Theory]
    [InlineData("deleted")]
    [InlineData("foreign")]
    [InlineData("malformed")]
    public async Task Describe_TeamNotActiveInTheOrganization_IsTeamNotFound_BeforeTheBody(string how)
    {
        var f = new Fixture();
        var foreign = f.Store.AddTeam(f.Store.AddOrganization("Другая"), "Чужая");

        if (how == "deleted")
        {
            f.Mars.Delete();
        }

        var id = how switch { "foreign" => foreign.Id.ToString(), "malformed" => "abc", _ => f.Mars.Id.ToString() };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(new string('я', 2001), id));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }

    [Fact]
    public async Task Describe_LocksTheTeamForNoKeyUpdate_ThenSavesOnce()
    {
        var f = new Fixture();

        await f.HandleAsync("текст");

        Assert.Equal([$"lock team no-key-update {f.Mars.Id}", "save"], f.Store.Calls);
    }
}
