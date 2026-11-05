using Mustask.Application.Features.Organizations.SetProjectDescription;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.SetProjectDescription;

/// <summary>
/// <c>SetProjectDescription</c> on fakes (feat-028 api-contract § 2.4; US-2 "edit a project's description", BR-OL-3): kept as typed,
/// cleared by the empty string and null, refused over 2 000 characters; the project row locked <c>FOR NO KEY UPDATE</c>.
/// </summary>
public sealed class SetProjectDescriptionHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Handler = new SetProjectDescriptionHandler(new FakeProjectRepository(Store), new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public SetProjectDescriptionHandler Handler { get; }

        public Task<SetProjectDescriptionResult> HandleAsync(string? description, string? projectId = null, string? organizationId = null) =>
            Handler.Handle(
                new SetProjectDescriptionCommand(organizationId ?? Organization.Id.ToString(), projectId ?? Project.Project.Id.ToString(), description),
                CancellationToken.None);
    }

    [Fact]
    public async Task Describe_KeepsTheTextAsTyped_AnswersTheProject()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(" О проекте\n");

        Assert.Equal(new ProjectResponse(f.Project.Project.Id, f.Organization.Id, "Пример проекта", "PRM", " О проекте\n"), result.Response);
        Assert.Equal(" О проекте\n", f.Project.Project.Description);
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
    }

    [Fact]
    public async Task Describe_2001Characters_IsValidationFailed_Unchanged()
    {
        var f = new Fixture();
        await f.HandleAsync("прежнее");

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(new string('я', 2001)));

        Assert.Equal(OrganizationErrorCodes.DescriptionLength, Assert.Single(refusal.Errors["description"]).Code);
        Assert.Equal("прежнее", f.Project.Project.Description);
    }

    // The address first: a deactivated project (also a foreign or malformed one) is project.not_found, before the body.
    [Theory]
    [InlineData("deactivated")]
    [InlineData("foreign")]
    [InlineData("malformed")]
    public async Task Describe_ProjectNotActiveInTheOrganization_IsProjectNotFound_BeforeTheBody(string how)
    {
        var f = new Fixture();
        var foreign = f.Store.AddProject(f.Store.AddOrganization("Другая"), "Чужой", "OTH");

        if (how == "deactivated")
        {
            f.Project.Project.Deactivate();
        }

        var id = how switch { "foreign" => foreign.Project.Id.ToString(), "malformed" => "abc", _ => f.Project.Project.Id.ToString() };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(new string('я', 2001), id));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
    }

    [Fact]
    public async Task Describe_LocksTheProjectRow_ThenSavesOnce()
    {
        var f = new Fixture();

        await f.HandleAsync("текст");

        Assert.Equal([$"lock project {f.Project.Project.Id}", "save"], f.Store.Calls);
    }
}
