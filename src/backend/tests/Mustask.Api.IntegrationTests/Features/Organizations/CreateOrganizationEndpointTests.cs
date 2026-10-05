using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Process;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations;
using Mustask.Application.Features.Process;
using Mustask.Application.Features.Process.SeedProcess;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>POST /api/organizations</c> (api-contract § 1.3) through the real host and a
/// real PostgreSQL: the create right read from the token, the name rules, the
/// instance-wide name, mod-001's seed in the same transaction, and the race on
/// one name. The route has no address, so it has no not-found theories. The
/// database is shared and never emptied and organization names are unique
/// instance-wide, so every test makes its own names (<see cref="OrganizationsTestData.UniqueName"/>)
/// and asserts about them; the only counts are of what a refusal must not
/// change, taken before and after, which holds because the collection runs
/// sequentially (plan-backend § 13).
/// </summary>
/// <remarks>
/// What a created organization is checked against is read through the
/// repositories and the seam, and the story's "appears in the list" through
/// <c>GET /api/organizations</c> and <c>GET /api/organizations/deactivated</c>
/// themselves.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class CreateOrganizationEndpointTests(MustaskApiFactory factory)
{
    private const string Path = "/api/organizations";

    private Task<Answer> CreateAsync(object body, string? token = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Post, Path, body, token ?? OrganizationsTokens.WithCreateRight(), gate);

    // US-1 · "An organization is created with its process": 201 with its location, active and listed, its process
    // recorded and answering mod-001's read, and the creator and the time kept (BR-M1-3, BR-M1-21).
    [Fact]
    public async Task Create_WithRight_Returns201_ListedActive_ProcessRecorded_CreatorKept()
    {
        var name = OrganizationsTestData.UniqueName("Пример организации");
        var subject = OrganizationsTokens.NewSubject();
        var startedAt = DateTimeOffset.UtcNow;

        var answer = await CreateAsync(new { name }, OrganizationsTokens.WithCreateRight(subject));
        var answeredAt = DateTimeOffset.UtcNow;

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        var id = answer.Body.GetProperty("id").GetGuid();
        Assert.Equal($"/api/organizations/{id}", answer.Location?.OriginalString);
        Assert.Equal(["id", "name"], PropertyNames(answer.Body));
        Assert.Equal(name, answer.Body.GetProperty("name").GetString());

        Assert.Contains((id, name), await ListAsync(Path));
        Assert.DoesNotContain((id, name), await ListAsync($"{Path}/deactivated"));

        using var scope = factory.Services.CreateScope();
        var info = await scope.ServiceProvider.GetRequiredService<IOrganizationStructure>()
            .FindOrganizationAsync(id, CancellationToken.None);
        Assert.NotNull(info);
        Assert.Equal(OrganizationStatus.Active, info.Status);
        Assert.Equal(name, info.Name);
        Assert.Equal(subject, info.CreatedBy);
        Assert.InRange(info.CreatedAt, startedAt, answeredAt);

        var process = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, $"{OrganizationsApi.Organization(id)}/process", token: OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.OK, process.Status);
        Assert.Equal(info.ProcessId.Value, process.Body.GetProperty("id").GetGuid());
    }

    // US-1 · "The name is trimmed and its length checked": «  Пример организации  » is stored as «Пример организации».
    [Fact]
    public async Task Create_PaddedName_StoresTrimmed()
    {
        var name = OrganizationsTestData.UniqueName("Пример организации");

        var answer = await CreateAsync(new { name = $"  {name}  " });

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(name, answer.Body.GetProperty("name").GetString());
        using var scope = factory.Services.CreateScope();
        Assert.Equal(name, await scope.ServiceProvider.GetRequiredService<IOrganizationRepository>()
            .FindNameHolderAsync(EntityName.Normalize(name), exceptId: null, CancellationToken.None));
    }

    // US-1 · "The name is trimmed and its length checked": the length is checked after trimming, so a name of exactly 100
    // characters with spaces around it is accepted.
    [Fact]
    public async Task Create_HundredCharactersPaddedWithSpaces_Returns201()
    {
        var name = new string('я', 68) + Guid.CreateVersion7().ToString("N");
        Assert.Equal(100, name.Length);

        var answer = await CreateAsync(new { name = $"  {name}  " });

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(name, answer.Body.GetProperty("name").GetString());
    }

    // US-1 · "The name is trimmed and its length checked": an empty name, one of spaces only, and one over 100 characters
    // are refused with «Название должно содержать от 1 до 100 символов», and no organization or process is created.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("101")]
    public async Task Create_InvalidName_Returns400NameLength_CreatesNothing(string? name)
    {
        var before = await CountsAsync();

        var answer = await CreateAsync(new { name = name == "101" ? new string('я', 101) : name });

        AssertNameLengthRefusal(answer);
        Assert.Equal(before, await CountsAsync());
    }

    // api-contract § 1.3: a body with no name is the same refusal as a null one.
    [Fact]
    public async Task Create_NameMissing_Returns400NameLength_CreatesNothing()
    {
        var before = await CountsAsync();

        var answer = await CreateAsync(new { });

        AssertNameLengthRefusal(answer);
        Assert.Equal(before, await CountsAsync());
    }

    // US-1 · "The name is unique instance-wide, deactivated organizations included": a name differing only in letter case and
    // surrounding spaces is refused with the holder's stored name, the answer is the same for an active and a deactivated
    // holder, and says nothing else about it (BR-M1-1).
    [Fact]
    public async Task Create_NameOfActiveOrDeactivated_Returns409WithStoredName_SameBodyForBoth()
    {
        var activeName = OrganizationsTestData.UniqueName("Пример организации");
        var deactivatedName = OrganizationsTestData.UniqueName("Пример организации");
        await OrganizationsTestData.CreateOrganizationAsync(factory, activeName);
        await OrganizationsTestData.CreateOrganizationAsync(factory, deactivatedName, OrganizationStatus.Deactivated);
        var before = await CountsAsync();

        var active = await CreateAsync(new { name = $"  {activeName.ToLowerInvariant()} " });
        var deactivated = await CreateAsync(new { name = deactivatedName.ToUpperInvariant() });

        foreach (var (answer, stored) in new[] { (active, activeName), (deactivated, deactivatedName) })
        {
            Assert.Equal(HttpStatusCode.Conflict, answer.Status);
            Assert.Equal("organizations.organization.name_taken", answer.ErrorCode);
            Assert.Equal($"Название «{stored}» уже занято", answer.Detail);
            var parameter = answer.Body.GetProperty("params").GetProperty("name");
            Assert.Equal(stored, parameter.GetProperty("value").GetString());
            Assert.Equal("string", parameter.GetProperty("type").GetString());
        }

        Assert.Equal(PropertyNames(active.Body), PropertyNames(deactivated.Body));
        Assert.Equal(PropertyNames(active.Body.GetProperty("params")), PropertyNames(deactivated.Body.GetProperty("params")));
        Assert.Equal(
            active.Detail!.Replace(activeName, "{name}", StringComparison.Ordinal),
            deactivated.Detail!.Replace(deactivatedName, "{name}", StringComparison.Ordinal));

        // Nothing was created for either, and each holder is unchanged.
        Assert.Equal(before, await CountsAsync());
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM organizations WHERE normalized_name = @p", EntityName.Normalize(activeName)));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM organizations WHERE normalized_name = @p", EntityName.Normalize(deactivatedName)));
    }

    // US-1 · "Two simultaneous creations of one name produce one organization" (NFR Concurrency): request A is held at its
    // commit with its organization and its seeded process written; B passes the pre-check (A is not committed), seeds its
    // own process, then blocks on the name index until A commits, and loses with the pre-check's code naming the submitted,
    // trimmed name (plan-backend § 6.3, § 8.3). B's transaction rolls back, and its seeded process goes with it.
    [Fact]
    public async Task Create_TwoConcurrentSameName_OneCreated_OtherRefused_NoOrphanProcess()
    {
        var name = OrganizationsTestData.UniqueName("Пример организации");
        var orphansBefore = await CountOrphanProcessesAsync();
        using var gate = factory.CommitGates.Open();

        var first = CreateAsync(new { name }, gate: gate);
        await Task.WhenAny(gate.Reached, first);
        Assert.False(first.IsCompleted, "The first creation finished before reaching its commit gate.");

        var second = CreateAsync(new { name = $" {name} " });
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "The second creation did not wait for the first one's uncommitted row.");

        gate.Release();
        var (a, b) = (await first, await second);

        Assert.Equal(HttpStatusCode.Created, a.Status);
        Assert.Equal(HttpStatusCode.Conflict, b.Status);
        Assert.Equal("organizations.organization.name_taken", b.ErrorCode);
        Assert.Equal(name, b.Body.GetProperty("params").GetProperty("name").GetProperty("value").GetString());
        Assert.Equal($"Название «{name}» уже занято", b.Detail);

        Assert.Equal(1, await CountAsync("SELECT count(*) FROM organizations WHERE normalized_name = @p", EntityName.Normalize(name)));
        Assert.Equal(a.Body.GetProperty("id").GetGuid(), await IdOfAsync(name));
        Assert.Equal(orphansBefore, await CountOrphanProcessesAsync());
    }

    // US-1 · "A failed process seed leaves nothing behind": the seed writes its process and then fails. The answer is the
    // create-failed 500 without the seed's own message, no organization and no process remain, the name is free, and a
    // retry succeeds once the seed works (BR-M1-3, status.md D-t).
    [Fact]
    public async Task Create_SeedFails_Returns500CreateFailed_NothingLeft_RetrySucceeds()
    {
        var name = OrganizationsTestData.UniqueName("Пример организации");
        var token = OrganizationsTokens.WithCreateRight();
        var orphansBefore = await CountOrphanProcessesAsync();
        var seed = new SeedSwitch { Fail = true };

        // The derived host replaces Serilog's static logger and disposes it: put it back (see the guard).
        using var loggerGuard = new StaticLoggerGuard();
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<SeedProcessHandler>();
                services.AddScoped<IProcessSeed>(provider =>
                    new SeedThenFailProcessSeed(provider.GetRequiredService<SeedProcessHandler>(), seed));
            }));
        var client = host.CreateClient();

        var failed = await PostAsync(client, new { name }, token);

        Assert.Equal(HttpStatusCode.InternalServerError, failed.Status);
        Assert.Equal("application/problem+json", failed.MediaType);
        Assert.Equal("organizations.organization.create_failed", failed.ErrorCode);
        Assert.Equal("Не удалось создать организацию. Попробуйте ещё раз", failed.Detail);
        Assert.False(failed.Body.TryGetProperty("params", out _));
        Assert.DoesNotContain(SeedThenFailProcessSeed.Message, failed.Body.GetRawText(), StringComparison.Ordinal);

        using (var scope = factory.Services.CreateScope())
        {
            Assert.Null(await scope.ServiceProvider.GetRequiredService<IOrganizationRepository>()
                .FindNameHolderAsync(EntityName.Normalize(name), exceptId: null, CancellationToken.None));
        }

        Assert.Equal(orphansBefore, await CountOrphanProcessesAsync());

        seed.Fail = false;
        var retry = await PostAsync(client, new { name }, token);

        Assert.Equal(HttpStatusCode.Created, retry.Status);
        Assert.Equal(name, retry.Body.GetProperty("name").GetString());
        Assert.Equal(retry.Body.GetProperty("id").GetGuid(), await IdOfAsync(name));
        Assert.Equal(orphansBefore, await CountOrphanProcessesAsync());
    }

    // US-1 · "Without the right the action is neither offered nor accepted" (backend half): refused with the create endpoint's
    // own code and text, never http.forbidden, and nothing is created.
    [Fact]
    public async Task Create_WithoutRight_Returns403CreateForbidden_CreatesNothing()
    {
        var name = OrganizationsTestData.UniqueName("Пример организации");

        var answer = await CreateAsync(new { name }, OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.Forbidden, answer.Status);
        Assert.Equal("application/problem+json", answer.MediaType);
        Assert.Equal("organizations.organization.create_forbidden", answer.ErrorCode);
        Assert.Equal("Недостаточно прав", answer.Detail);
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM organizations WHERE normalized_name = @p", EntityName.Normalize(name)));
    }

    // US-1 · "Without the right ... is refused with «Недостаточно прав»" (QA pass 1): every other code of the catalog
    // together does not stand in for the create right (BR-M1-2, BR-M1-19), and nothing is created.
    [Fact]
    public async Task Create_EveryCodeButTheCreateRight_Returns403CreateForbidden_CreatesNothing()
    {
        var name = OrganizationsTestData.UniqueName("Пример организации");
        var otherCodes = Mustask.Domain.Identity.PermissionCatalog.AllCodes
            .Where(code => code != Mustask.Domain.Identity.PermissionCatalog.OrganizationsOrganizationCreate)
            .ToArray();
        Assert.NotEmpty(otherCodes);

        var answer = await CreateAsync(
            new { name },
            KeycloakTestRealm.CreateToken(otherCodes, options => options.Subject = OrganizationsTokens.NewSubject()));

        Assert.Equal(HttpStatusCode.Forbidden, answer.Status);
        Assert.Equal("organizations.organization.create_forbidden", answer.ErrorCode);
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM organizations WHERE normalized_name = @p", EntityName.Normalize(name)));
    }

    // US-1 · "The right is granted and revoked in the Keycloak console": the right is read from the token alone. The same user
    // is refused with a token without it, creates with one that carries it, and is refused again once it is revoked; the
    // organization created meanwhile survives, unchanged (BR-M1-2).
    [Fact]
    public async Task Create_RightIsReadFromTheTokenOnly_EarlierOrganizationSurvives()
    {
        var subject = OrganizationsTokens.NewSubject();
        var earlier = OrganizationsTestData.UniqueName("Организация Б");
        var later = OrganizationsTestData.UniqueName("Организация В");

        var beforeGrant = await CreateAsync(new { name = earlier }, OrganizationsTokens.SignedIn(subject));
        var granted = await CreateAsync(new { name = earlier }, OrganizationsTokens.WithCreateRight(subject));
        var afterRevoke = await CreateAsync(new { name = later }, OrganizationsTokens.SignedIn(subject));

        Assert.Equal(HttpStatusCode.Forbidden, beforeGrant.Status);
        Assert.Equal("organizations.organization.create_forbidden", beforeGrant.ErrorCode);
        Assert.Equal(HttpStatusCode.Created, granted.Status);
        Assert.Equal(HttpStatusCode.Forbidden, afterRevoke.Status);
        Assert.Equal("organizations.organization.create_forbidden", afterRevoke.ErrorCode);

        var id = granted.Body.GetProperty("id").GetGuid();
        using var scope = factory.Services.CreateScope();
        var info = await scope.ServiceProvider.GetRequiredService<IOrganizationStructure>().FindOrganizationAsync(id, CancellationToken.None);
        Assert.NotNull(info);
        Assert.Equal((earlier, OrganizationStatus.Active, subject), (info.Name, info.Status, info.CreatedBy));
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM organizations WHERE normalized_name = @p", EntityName.Normalize(later)));
    }

    // US-1 · "An anonymous request is refused": 401 before any other check, and nothing is created.
    [Fact]
    public async Task Create_NoToken_Returns401_CreatesNothing()
    {
        var name = OrganizationsTestData.UniqueName("Пример организации");

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, Path, new { name });

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal("http.unauthorized", answer.ErrorCode);
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM organizations WHERE normalized_name = @p", EntityName.Normalize(name)));
    }

    // api-contract § 1.3: the length refusal, with the reason's own text.
    private static void AssertNameLengthRefusal(Answer answer)
    {
        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("organizations.validation_failed", answer.ErrorCode);
        var reason = Assert.Single(answer.Body.GetProperty("errors").GetProperty("name").EnumerateArray());
        Assert.Equal("organizations.name_length", reason.GetProperty("code").GetString());
        Assert.Equal("Название должно содержать от 1 до 100 символов", reason.GetProperty("detail").GetString());
        Assert.Single(answer.Body.GetProperty("errors").EnumerateObject());
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);

    /// <summary>A list of organizations as the panel reads it (api-contract § 1.1, § 1.2), by any signed-in user.</summary>
    private async Task<IReadOnlyList<(Guid Id, string Name)>> ListAsync(string path)
    {
        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, answer.Status);

        return answer.Body.EnumerateArray()
            .Select(item => (Id: item.GetProperty("id").GetGuid(), Name: item.GetProperty("name").GetString()!))
            .ToList();
    }

    /// <summary>A request on a derived host's own client, which <see cref="OrganizationsApi"/> (bound to the shared factory) cannot send.</summary>
    private static async Task<Answer> PostAsync(HttpClient client, object body, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = JsonContent.Create(body, body.GetType()) };
        request.WithBearer(token);

        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var json = string.IsNullOrEmpty(text) ? default : JsonDocument.Parse(text).RootElement.Clone();

        return new Answer(response.StatusCode, response.Headers.Location, response.Content.Headers.ContentType?.MediaType, json);
    }

    /// <summary>The organizations and the processes no organization holds, as stored now.</summary>
    private async Task<(long Organizations, long OrphanProcesses)> CountsAsync() =>
        (await CountAsync("SELECT count(*) FROM organizations"), await CountOrphanProcessesAsync());

    /// <summary>A process no organization points at: what a creation that failed or lost a race must not leave (BR-M1-3).</summary>
    private Task<long> CountOrphanProcessesAsync() =>
        CountAsync("SELECT count(*) FROM processes p WHERE NOT EXISTS (SELECT 1 FROM organizations o WHERE o.process_id = p.id)");

    private async Task<Guid?> IdOfAsync(string name)
    {
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT id FROM organizations WHERE normalized_name = @p", connection);
        command.Parameters.AddWithValue("p", EntityName.Normalize(name));

        return (Guid?)await command.ExecuteScalarAsync();
    }

    private async Task<long> CountAsync(string sql, string? parameter = null)
    {
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);

        if (parameter is not null)
        {
            command.Parameters.AddWithValue("p", parameter);
        }

        return (long)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Whether the decorated seed fails; flipped between the failed creation and its retry.</summary>
    private sealed class SeedSwitch
    {
        public bool Fail { get; set; }
    }

    /// <summary>
    /// Lets the real seed write its rows, then throws while <see cref="SeedSwitch.Fail"/> is set: the nearest a test gets
    /// to a seed that fails late, with its rows already in the transaction (US-1, BR-M1-3).
    /// </summary>
    private sealed class SeedThenFailProcessSeed(IProcessSeed inner, SeedSwitch seed) : IProcessSeed
    {
        public const string Message = "the seed failed after it wrote its rows";

        public async Task<ProcessId> SeedAsync(Guid organizationId, CancellationToken cancellationToken)
        {
            var processId = await inner.SeedAsync(organizationId, cancellationToken);

            return seed.Fail ? throw new InvalidOperationException(Message) : processId;
        }
    }
}
