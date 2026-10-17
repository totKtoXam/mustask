using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/name</c> (api-contract § 1.5)
/// through the real host and a real PostgreSQL. Every test builds its own
/// organizations (<see cref="OrganizationsTestData"/>) under names made unique
/// with a suffix: the database is shared, never emptied, and organization
/// names are unique instance-wide. The tests read the stored state through the
/// repositories and read models, not through another lane's routes. The
/// organization level of the not-found answers (malformed, unknown, deactivated, a
/// non-member) is the scoped check's, once for all routes (<c>ScopedAccessTests</c>,
/// mod-016 ADR-0027).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class RenameOrganizationEndpointTests(MustaskApiFactory factory)
{
    private const string NameLengthDetail = "Название должно содержать от 1 до 100 символов";

    // The caller is the creator, the administrator of every organization the test data builds, who holds
    // organizations.organization.write (mod-016 BR-M1-7); the token has no create right and needs none.
    private Task<Answer> SendAsync(Guid organizationId, object? body, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Organization(organizationId)}/name",
            body,
            OrganizationsTokens.SignedIn(),
            gate);

    private Task<Answer> RenameAsync(Guid organizationId, string? name, CommitGate? gate = null) =>
        SendAsync(organizationId, new { name }, gate);

    // US-2 · "The organization is renamed": «Пример организации 2» (padded), a name free instance-wide. It answers the stored,
    // trimmed name, and the creator, a member, sees it listed under the new name.
    [Fact]
    public async Task Rename_FreeName_Returns200_ListShowsNewName()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, OrganizationsTestData.UniqueName("Пример организации"));
        var newName = OrganizationsTestData.UniqueName("Пример организации 2");

        var answer = await RenameAsync(organization.Id, $"  {newName}  ");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(["id", "name"], PropertyNames(answer.Body));
        Assert.Equal(organization.Id, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal(newName, answer.Body.GetProperty("name").GetString());

        var listed = await ActiveListAsync();
        Assert.Equal(newName, Assert.Single(listed, row => row.Id == organization.Id).Name);
        Assert.DoesNotContain(listed, row => row.Name == organization.Name);
    }

    // US-2 · "its projects, teams and areas are unchanged": the project (name, key), the teams, the area tree
    // and the process are as they were.
    [Fact]
    public async Task Rename_LeavesProjectsTeamsAreasUnchanged()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.AddAreaAsync(
            factory, structure.ProjectId, structure.RootId, "Отчёты");
        var before = await SnapshotAsync(structure);

        var answer = await RenameAsync(structure.OrganizationId, OrganizationsTestData.UniqueName("Пример организации 2"));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(before, await SnapshotAsync(structure));
    }

    // US-2 · "renaming it to «пример организации 2», its own name in another letter case, is allowed (BR-M1-1)": a name never
    // conflicts with the organization's own current name, in another case or in the same one.
    [Fact]
    public async Task Rename_OwnNameInAnotherCase_Allowed()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, OrganizationsTestData.UniqueName("Пример организации"));
        var upper = organization.Name.ToUpperInvariant();
        Assert.NotEqual(organization.Name, upper);

        var inAnotherCase = await RenameAsync(organization.Id, $" {upper} ");
        var asItIs = await RenameAsync(organization.Id, upper);

        Assert.Equal(HttpStatusCode.OK, inAnotherCase.Status);
        Assert.Equal(upper, inAnotherCase.Body.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.OK, asItIs.Status);
        Assert.Equal(upper, await StoredNameAsync(organization.Id));
    }

    // US-2 · the length rule's upper edge: exactly 100 characters after trimming are accepted.
    [Fact]
    public async Task Rename_NameOfOneHundredCharacters_Returns200()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var name = new string('я', 68) + Guid.CreateVersion7().ToString("N");
        Assert.Equal(100, name.Length);

        var answer = await RenameAsync(organization.Id, $"  {name}  ");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(name, await StoredNameAsync(organization.Id));
    }

    // US-2 · "A rename to a taken ... name is refused": «Бета» renamed to «альфа» is refused with «Название «Альфа» уже занято»
    // (the holder's stored name, whatever the submitted case and spaces), and the answer is the same for an active and a
    // deactivated holder (BR-M1-1, BR-M1-20). Both keep their names.
    [Theory]
    [InlineData(OrganizationStatus.Active)]
    [InlineData(OrganizationStatus.Deactivated)]
    public async Task Rename_ToTakenNameAnyCase_Returns409WithStoredName_NameUnchanged(OrganizationStatus holderStatus)
    {
        var alpha = await OrganizationsTestData.CreateOrganizationAsync(factory, OrganizationsTestData.UniqueName("Альфа"), holderStatus);
        var beta = await OrganizationsTestData.CreateOrganizationAsync(factory, OrganizationsTestData.UniqueName("Бета"));
        var submitted = $"  {alpha.Name.ToLowerInvariant()} ";
        Assert.NotEqual(alpha.Name, submitted.Trim());

        var answer = await RenameAsync(beta.Id, submitted);

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.organization.name_taken", answer.ErrorCode);
        Assert.Equal($"Название «{alpha.Name}» уже занято", answer.Detail);
        var name = answer.Body.GetProperty("params").GetProperty("name");
        Assert.Equal(alpha.Name, name.GetProperty("value").GetString());
        Assert.Equal("string", name.GetProperty("type").GetString());
        Assert.Equal(beta.Name, await StoredNameAsync(beta.Id));
        Assert.Equal(alpha.Name, await StoredNameAsync(alpha.Id));
    }

    // US-2 · "refused with «Название должно содержать от 1 до 100 символов»": a null, an empty, a spaces-only and a
    // 101-character name. «Бета» keeps its name.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("101")]
    public async Task Rename_InvalidLength_Returns400NameLength_NameUnchanged(string? name)
    {
        var beta = await OrganizationsTestData.CreateOrganizationAsync(factory, OrganizationsTestData.UniqueName("Бета"));

        var answer = await RenameAsync(beta.Id, name == "101" ? new string('я', 101) : name);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("organizations.validation_failed", answer.ErrorCode);
        var reason = Assert.Single(answer.Body.GetProperty("errors").GetProperty("name").EnumerateArray());
        Assert.Equal("organizations.name_length", reason.GetProperty("code").GetString());
        Assert.Equal(NameLengthDetail, reason.GetProperty("detail").GetString());
        Assert.Equal(beta.Name, await StoredNameAsync(beta.Id));
    }

    // US-1 · "A visitor who is not signed in sees nothing": the challenge comes before anything else, and nothing changes.
    [Fact]
    public async Task Rename_NoToken_Returns401_ChangesNothing()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Organization(organization.Id)}/name",
            new { name = OrganizationsTestData.UniqueName() });

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal(organization.Name, await StoredNameAsync(organization.Id));
    }

    // US-2 · "A deactivated organization takes no change": a rename answers organization.not_found and the name stays. (The
    // filter answers for every route; this pins the effect for this one, and that the name it would have taken stays free.)
    [Fact]
    public async Task Rename_DeactivatedOrganization_Returns404_NameUnchanged()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(
            factory, OrganizationsTestData.UniqueName("Старая"), OrganizationStatus.Deactivated);
        var wanted = OrganizationsTestData.UniqueName("Новая");

        var answer = await RenameAsync(organization.Id, wanted);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.organization.not_found", answer.ErrorCode);
        Assert.Equal(organization.Name, await StoredNameAsync(organization.Id));
        Assert.DoesNotContain(await ActiveListAsync(), row => row.Name == wanted);
    }

    // US-2 · "Two renames to one free name keep one" (NFR Concurrency): «Альфа» and «Бета» both go to «Гамма». Request A is held
    // at its commit with its update written; B passes the pre-check (A is not committed), then blocks on the name index until A
    // commits, and loses with the pre-check's code, naming the submitted name (plan-backend § 6.3, § 8.3). Two different rows,
    // so no row lock is involved: the index is what closes it.
    [Fact]
    public async Task Rename_TwoConcurrentToSameFreeName_OneWins()
    {
        var alpha = await OrganizationsTestData.CreateOrganizationAsync(factory, OrganizationsTestData.UniqueName("Альфа"));
        var beta = await OrganizationsTestData.CreateOrganizationAsync(factory, OrganizationsTestData.UniqueName("Бета"));
        var gamma = OrganizationsTestData.UniqueName("Гамма");
        using var gate = factory.CommitGates.Open();

        var first = RenameAsync(alpha.Id, gamma, gate);
        await Task.WhenAny(gate.Reached, first);
        Assert.False(first.IsCompleted, "The first rename finished before reaching its commit gate.");

        var second = RenameAsync(beta.Id, $" {gamma} ");
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "The second rename did not wait for the first one's uncommitted name.");

        gate.Release();
        var (a, b) = (await first, await second);

        Assert.Equal(HttpStatusCode.OK, a.Status);
        Assert.Equal(gamma, a.Body.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.Conflict, b.Status);
        Assert.Equal("organizations.organization.name_taken", b.ErrorCode);
        Assert.Equal($"Название «{gamma}» уже занято", b.Detail);
        Assert.Equal(gamma, b.Body.GetProperty("params").GetProperty("name").GetProperty("value").GetString());

        Assert.Equal(gamma, await StoredNameAsync(alpha.Id));
        Assert.Equal(beta.Name, await StoredNameAsync(beta.Id));
        Assert.Single(await ActiveListAsync(), row => row.Name == gamma);
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);

    /// <summary>The organization's name as stored now, whatever its status.</summary>
    private async Task<string> StoredNameAsync(Guid organizationId) =>
        (await OrganizationsTestData.InUnitOfWorkAsync(
            factory,
            async (services, ct) => await services.GetRequiredService<IOrganizationRepository>().LockAsync(organizationId, ct)))!.Name;

    /// <summary>The active organizations as the list read serves them to the test data's creator, who administers them (mod-016 BR-M1-7).</summary>
    private async Task<IReadOnlyList<OrganizationRow>> ActiveListAsync()
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IOrganizationReadModel>()
            .ListAsync(OrganizationStatus.Active, OrganizationsTestData.Creator, onlyRoles: null, CancellationToken.None);
    }

    /// <summary>
    /// Everything beneath the organization and its process, as stored now: one line per fact, so two snapshots compare by
    /// value.
    /// </summary>
    private async Task<List<string>> SnapshotAsync(OrganizationStructure structure)
    {
        var organization = await OrganizationsTestData.InUnitOfWorkAsync(
            factory,
            async (services, ct) => await services.GetRequiredService<IOrganizationRepository>().LockAsync(structure.OrganizationId, ct));

        using var scope = factory.Services.CreateScope();
        var project = await scope.ServiceProvider.GetRequiredService<IProjectReadModel>()
            .FindReachableAsync(structure.OrganizationId, structure.ProjectId, CancellationToken.None);
        var teams = await scope.ServiceProvider.GetRequiredService<ITeamReadModel>()
            .ListActiveAsync(structure.OrganizationId, structure.ProjectId, "nobody", CancellationToken.None);
        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);

        return
        [
            $"process {organization!.ProcessId.Value}",
            $"project {project}",
            .. teams.Select(team => $"team {team}"),
            .. tree.PreOrder().Select(node => $"area {node}"),
        ];
    }
}
