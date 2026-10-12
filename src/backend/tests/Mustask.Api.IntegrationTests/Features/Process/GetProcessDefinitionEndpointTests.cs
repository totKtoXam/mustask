using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Features.Process;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Process;

/// <summary>
/// US-2 (mod-001-process-seed): <c>GET /api/organizations/{organizationId}/process</c>
/// through the real host and a real PostgreSQL, the contract of api-contract.md § 1.
/// Since mod-002 the read runs behind the organization-context filter (mod-002
/// D-g, its api-contract § 9), so every test creates a real organization: its
/// process seeded through the seam (<see cref="IProcessSeed"/> inside
/// <see cref="IUnitOfWork"/>) and its row stored, as organization creation does.
/// No test counts processes globally: the database is shared.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetProcessDefinitionEndpointTests(MustaskApiFactory factory)
{
    private const string NotFoundCode = "organizations.organization.not_found";

    // api-contract.md § 1.4, as written: key, name, appearance, level and the states "order name -> code / label".
    private static readonly (string Key, string Name, string Appearance, string Level, string States)[] ExpectedTypes =
    [
        ("EPIC", "Эпик", "epic", "Эпики", "1 Новая Proposed Новые; 2 Активна InProgress В работе; 3 Разрешена Resolved Решённые; 4 Закрыта Completed Завершённые; 5 Удалена Removed Удалённые"),
        ("FEAT", "Фича", "feature", "Фичи", "1 Новая Proposed Новые; 2 Активна InProgress В работе; 3 Разрешена Resolved Решённые; 4 Закрыта Completed Завершённые; 5 Удалена Removed Удалённые"),
        ("US", "Пользовательская история", "story", "Истории", "1 Новая Proposed Новые; 2 Активна InProgress В работе; 3 Разрешена Resolved Решённые; 4 Закрыта Completed Завершённые; 5 Удалена Removed Удалённые"),
        ("BUG", "Баг", "bug", "Истории", "1 Новая Proposed Новые; 2 Активна InProgress В работе; 3 Разрешена Resolved Решённые; 4 Закрыта Completed Завершённые; 5 Удалена Removed Удалённые"),
        ("TASK", "Задача", "task", "Задачи", "1 Новая Proposed Новые; 2 Активна InProgress В работе; 3 Закрыта Completed Завершённые; 4 Удалена Removed Удалённые"),
    ];

    private static string Path(string organizationId) => $"/api/organizations/{Uri.EscapeDataString(organizationId)}/process";

    private static HttpRequestMessage Request(string organizationId, string? accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Path(organizationId));

        if (accessToken is not null)
        {
            request.WithBearer(accessToken);
        }

        return request;
    }

    private static string SignedIn() => KeycloakTestRealm.CreateToken([]);

    private async Task<(Guid OrganizationId, ProcessId Id)> SeedOrganizationAsync()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        return (organization.Id, organization.ProcessId);
    }

    private async Task<(HttpResponseMessage Response, JsonElement Body)> GetAsync(string organizationId, string? accessToken)
    {
        var response = await factory.CreateClient().SendAsync(Request(organizationId, accessToken));
        var text = await response.Content.ReadAsStringAsync();

        return (response, JsonDocument.Parse(text).RootElement.Clone());
    }

    private static string DescribeStates(JsonElement type) =>
        string.Join(
            "; ",
            type.GetProperty("states").EnumerateArray().Select(state =>
                $"{state.GetProperty("order").GetInt32()} {state.GetProperty("name").GetString()} "
                + $"{state.GetProperty("category").GetProperty("code").GetString()} {state.GetProperty("category").GetProperty("label").GetString()}"));

    private static JsonElement TypeByKey(JsonElement body, string key) =>
        body.GetProperty("workItemTypes").EnumerateArray().Single(type => type.GetProperty("key").GetString() == key);

    private static IEnumerable<string> PropertyNames(JsonElement element) => element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);

    // US-2 · "A seeded process is shown in full" (backend half): the body equals api-contract § 1.4, ids aside.
    [Fact]
    public async Task Get_SeededOrganization_ReturnsFiveTypesInBacklogOrderWithStatesTokensAndLevels()
    {
        var (organizationId, processId) = await SeedOrganizationAsync();

        var (response, body) = await GetAsync(organizationId.ToString(), SignedIn());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(processId.Value, body.GetProperty("id").GetGuid());

        var types = body.GetProperty("workItemTypes").EnumerateArray().ToList();
        Assert.Equal(
            ExpectedTypes.Select(type => (type.Key, type.Name, type.Appearance, type.Level)),
            types.Select(type => (
                type.GetProperty("key").GetString()!,
                type.GetProperty("name").GetString()!,
                type.GetProperty("appearance").GetString()!,
                type.GetProperty("backlogLevel").GetProperty("name").GetString()!)));
        Assert.Equal(ExpectedTypes.Select(type => type.States), types.Select(DescribeStates));

        var levels = body.GetProperty("backlogLevels").EnumerateArray().ToList();
        Assert.Equal(
            [("Эпики", 4, "portfolio"), ("Фичи", 3, "portfolio"), ("Истории", 2, "requirement"), ("Задачи", 1, "task")],
            levels.Select(level => (level.GetProperty("name").GetString()!, level.GetProperty("rank").GetInt32(), level.GetProperty("kind").GetString()!)));

        // Each type's level is one entry of backlogLevels, by identity and name.
        Assert.All(types, type =>
        {
            var reference = type.GetProperty("backlogLevel");
            Assert.Contains(levels, level =>
                level.GetProperty("id").GetGuid() == reference.GetProperty("id").GetGuid()
                && level.GetProperty("name").GetString() == reference.GetProperty("name").GetString());
        });
    }

    // US-2 · "A seeded process is shown in full": the initial state is the first, and only the first.
    [Fact]
    public async Task Get_SeededOrganization_MarksOnlyTheFirstStateOfEachTypeAsInitial()
    {
        var (organizationId, _) = await SeedOrganizationAsync();

        var (_, body) = await GetAsync(organizationId.ToString(), SignedIn());

        foreach (var type in body.GetProperty("workItemTypes").EnumerateArray())
        {
            var states = type.GetProperty("states").EnumerateArray().ToList();

            Assert.Equal(Enumerable.Range(1, states.Count), states.Select(state => state.GetProperty("order").GetInt32()));
            Assert.Equal([true], states.Select(state => state.GetProperty("isInitial").GetBoolean()).Where(initial => initial));
            Assert.True(states[0].GetProperty("isInitial").GetBoolean());
            Assert.Equal("Удалена", states[^1].GetProperty("name").GetString());
        }
    }

    // api-contract § 1.2: every value is always present and nothing is null; the shape holds only what the contract names,
    // so the response carries no organization name, audit stamp, icon or colour.
    [Fact]
    public async Task Get_SeededOrganization_HasExactlyTheContractShapeAndNoNulls()
    {
        var (organizationId, _) = await SeedOrganizationAsync();

        var (_, body) = await GetAsync(organizationId.ToString(), SignedIn());

        Assert.Equal(["backlogLevels", "id", "workItemTypes"], PropertyNames(body));
        Assert.All(body.GetProperty("backlogLevels").EnumerateArray(), level => Assert.Equal(["id", "kind", "name", "rank"], PropertyNames(level)));
        Assert.All(body.GetProperty("workItemTypes").EnumerateArray(), type =>
        {
            Assert.Equal(["appearance", "backlogLevel", "id", "key", "name", "states"], PropertyNames(type));
            Assert.Equal(["id", "name"], PropertyNames(type.GetProperty("backlogLevel")));
            Assert.All(type.GetProperty("states").EnumerateArray(), state =>
            {
                Assert.Equal(["category", "id", "isInitial", "name", "order"], PropertyNames(state));
                Assert.Equal(["code", "label"], PropertyNames(state.GetProperty("category")));
            });
        });

        Assert.Equal(0, CountNulls(body));

        var ids = new List<Guid> { body.GetProperty("id").GetGuid() };
        ids.AddRange(body.GetProperty("backlogLevels").EnumerateArray().Select(level => level.GetProperty("id").GetGuid()));
        ids.AddRange(body.GetProperty("workItemTypes").EnumerateArray().Select(type => type.GetProperty("id").GetGuid()));
        ids.AddRange(body.GetProperty("workItemTypes").EnumerateArray()
            .SelectMany(type => type.GetProperty("states").EnumerateArray()).Select(state => state.GetProperty("id").GetGuid()));
        Assert.Equal(1 + 4 + 5 + 24, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    // US-2 · "Every signed-in user sees the same definition": one token holds every realm code, the other holds none.
    [Fact]
    public async Task Get_UserWithEveryRealmCodeAndUserWithNone_SeeIdenticalDefinitions()
    {
        var (organizationId, _) = await SeedOrganizationAsync();
        var everything = KeycloakTestRealm.CreateToken([.. PermissionCatalog.AllCodes]);
        var nothing = KeycloakTestRealm.CreateToken([]);

        var withEverything = await factory.CreateClient().SendAsync(Request(organizationId.ToString(), everything));
        var withNothing = await factory.CreateClient().SendAsync(Request(organizationId.ToString(), nothing));

        Assert.Equal(HttpStatusCode.OK, withEverything.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withNothing.StatusCode);
        Assert.Equal(await withEverything.Content.ReadAsStringAsync(), await withNothing.Content.ReadAsStringAsync());
    }

    // US-2 · "Every signed-in user sees the same definition": a token with no resource roles at all, and a token
    // that carries the declared code itself, are served the same (the code is declared, not checked: BR-M2-10).
    [Fact]
    public async Task Get_TokenCarryingTheDeclaredCodeAndTokenWithoutResourceRoles_SeeIdenticalDefinitions()
    {
        var (organizationId, _) = await SeedOrganizationAsync();
        var carrying = KeycloakTestRealm.CreateToken([OrganizationPermissionCatalog.ProcessDefinitionRead]);
        var bare = KeycloakTestRealm.CreateToken(configure: options => options.Permissions = null);

        var first = await factory.CreateClient().SendAsync(Request(organizationId.ToString(), carrying));
        var second = await factory.CreateClient().SendAsync(Request(organizationId.ToString(), bare));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
    }

    // US-2 · "Categories are shown by their Russian labels" (backend half): the label comes with the code, never alone.
    [Fact]
    public async Task Get_SeededOrganization_TaskStatesCarryRussianCategoryLabels()
    {
        var (organizationId, _) = await SeedOrganizationAsync();

        var (_, body) = await GetAsync(organizationId.ToString(), SignedIn());

        var states = TypeByKey(body, "TASK").GetProperty("states").EnumerateArray()
            .ToDictionary(state => state.GetProperty("name").GetString()!, state => state.GetProperty("category").GetProperty("label").GetString());
        Assert.Equal("Новые", states["Новая"]);
        Assert.Equal("В работе", states["Активна"]);
        Assert.Equal("Завершённые", states["Закрыта"]);
        Assert.Equal("Удалённые", states["Удалена"]);
        Assert.Equal(4, states.Count);
    }

    // US-2 · "One state name has one category" (bug-004 D-3, superseding doc-010 D-4's two categories of «Разрешена»):
    // «Разрешена» is Resolved, «Решённые», on every type that has it, and no name of the process has two categories.
    [Fact]
    public async Task Get_SeededOrganization_EveryStateNameHasOneCategoryOnEveryType()
    {
        var (organizationId, _) = await SeedOrganizationAsync();

        var (_, body) = await GetAsync(organizationId.ToString(), SignedIn());

        string Resolved(string key) => TypeByKey(body, key).GetProperty("states").EnumerateArray()
            .Single(state => state.GetProperty("name").GetString() == "Разрешена")
            .GetProperty("category").GetProperty("code").GetString()!;
        Assert.All(new[] { "EPIC", "FEAT", "US", "BUG" }, key => Assert.Equal("Resolved", Resolved(key)));

        var categoriesOfName = body.GetProperty("workItemTypes").EnumerateArray()
            .SelectMany(type => type.GetProperty("states").EnumerateArray())
            .GroupBy(state => state.GetProperty("name").GetString()!, state => state.GetProperty("category").GetProperty("code").GetString()!);
        Assert.All(categoriesOfName, sameName => Assert.Single(sameName.Distinct()));
    }

    // Canonical form in either letter case names the same organization (api-contract § 1.1).
    [Fact]
    public async Task Get_UppercaseCanonicalId_ReturnsTheSameDefinition()
    {
        var (organizationId, processId) = await SeedOrganizationAsync();

        var (response, body) = await GetAsync(organizationId.ToString().ToUpperInvariant(), SignedIn());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(processId.Value, body.GetProperty("id").GetGuid());
    }

    // US-2 · "An identifier with no process is not found": 404, the registered code, its text, no process data.
    [Fact]
    public async Task Get_UnknownOrganization_Returns404WithNotFoundCodeAndNoProcessData()
    {
        var (response, body) = await GetAsync(Guid.NewGuid().ToString(), SignedIn());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(NotFoundCode, body.GetProperty("errorCode").GetString());
        Assert.Equal("Организация не найдена.", body.GetProperty("detail").GetString());
        Assert.Equal(404, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()), "api-contract § 1.3: every error body carries traceId.");
        Assert.False(body.TryGetProperty("workItemTypes", out _));
        Assert.False(body.TryGetProperty("backlogLevels", out _));
    }

    // US-2 · "...for the unknown and the malformed identifier alike" (BR-M2-11, status.md D-9): the same answer, whatever the malformation.
    [Theory]
    [InlineData("abc")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("d00000000000400080000000000000001")]
    [InlineData("{d0000000-0000-4000-8000-000000000001}")]
    [InlineData("d0000000-0000-4000-8000-00000000001")]
    [InlineData("d0000000-0000-4000-8000-000000000001a")]
    [InlineData("d0000000-0000-4000-8000-000000000001 ")]
    // QA pass 1 (qa.md A-2, A-4): the other spellings of the seeded demonstration id, and values that only the HTTP
    // path can carry: Cyrillic text, SQL, an encoded slash inside the segment.
    [InlineData(" d0000000-0000-4000-8000-000000000001")]
    [InlineData("(d0000000-0000-4000-8000-000000000001)")]
    [InlineData("организация")]
    [InlineData("'; DROP TABLE processes; --")]
    [InlineData("d0000000-0000-4000-8000-000000000001/x")]
    public async Task Get_MalformedOrganizationId_ReturnsTheSameAnswer(string malformed)
    {
        var (_, unknown) = await GetAsync(Guid.NewGuid().ToString(), SignedIn());

        var (response, body) = await GetAsync(malformed, SignedIn());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(NotFoundCode, body.GetProperty("errorCode").GetString());
        Assert.Equal(unknown.GetProperty("detail").GetString(), body.GetProperty("detail").GetString());
        Assert.Equal(unknown.GetProperty("status").GetInt32(), body.GetProperty("status").GetInt32());
        Assert.Equal(
            PropertyNames(unknown).Where(name => name != "exception"),
            PropertyNames(body).Where(name => name != "exception"));
    }

    // QA pass 1 (qa.md A-4): a long value is judged like any other malformed one, not refused by the server as a 400 or 414.
    [Fact]
    public async Task Get_VeryLongOrganizationId_ReturnsTheNotFoundCode()
    {
        var (response, body) = await GetAsync(new string('d', 2000), SignedIn());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(NotFoundCode, body.GetProperty("errorCode").GetString());
    }

    // QA pass 1 (qa.md A-8) · api-contract § 2: "Until the caller commits, no other session sees the process." A read
    // racing an organization's creation sees nothing, never a half-built process, and the whole process after the commit.
    // Since mod-002 the open operation is the organization's creation itself: the seed, then the organization's row.
    [Fact]
    public async Task Get_WhileTheSeedingOperationIsOpen_IsNotFoundUntilItCommits()
    {
        var organizationId = Guid.CreateVersion7();
        var seeded = new TaskCompletionSource<ProcessId>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var operation = Task.Run(async () =>
        {
            using var scope = factory.Services.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var seed = scope.ServiceProvider.GetRequiredService<IProcessSeed>();
            var organizations = scope.ServiceProvider.GetRequiredService<IOrganizationRepository>();

            return await unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    var id = await seed.SeedAsync(organizationId, ct);
                    organizations.Add(Organization.Create(
                        organizationId,
                        OrganizationsTestData.Name(OrganizationsTestData.UniqueName()),
                        id,
                        OrganizationsTestData.Now,
                        OrganizationsTestData.Creator));
                    await organizations.SaveAsync(ct);
                    seeded.SetResult(id);
                    await release.Task;

                    return id;
                },
                CancellationToken.None);
        });

        try
        {
            await await Task.WhenAny(seeded.Task, operation);

            var (during, duringBody) = await GetAsync(organizationId.ToString(), SignedIn());

            Assert.Equal(HttpStatusCode.NotFound, during.StatusCode);
            Assert.Equal(NotFoundCode, duringBody.GetProperty("errorCode").GetString());
        }
        finally
        {
            release.TrySetResult();
        }

        var committed = await operation;
        var (after, afterBody) = await GetAsync(organizationId.ToString(), SignedIn());

        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Equal(committed.Value, afterBody.GetProperty("id").GetGuid());
        Assert.Equal(5, afterBody.GetProperty("workItemTypes").GetArrayLength());
    }

    // QA pass 1 (qa.md A-5) · US-1 "No person can seed or change a process", over HTTP rather than in the OpenAPI
    // document: every write method on the route is refused as a method the route does not have, and seeds nothing.
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task WriteMethod_OnTheProcessRoute_Returns405AndSeedsNothing(string method)
    {
        var organizationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(new HttpMethod(method), Path(organizationId))
        {
            Content = method == "DELETE" ? null : JsonContent.Create(new { }),
        };
        request.WithBearer(KeycloakTestRealm.CreateToken([.. PermissionCatalog.AllCodes]));

        using var response = await factory.CreateClient().SendAsync(request);
        var (read, body) = await GetAsync(organizationId, SignedIn());

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(NotFoundCode, body.GetProperty("errorCode").GetString());
    }

    // QA pass 1 · US-2 "A visitor who is not signed in sees nothing": "without a valid sign-in" covers a token that is
    // present but not valid, not only a missing one.
    [Theory]
    [InlineData("expired")]
    [InlineData("foreign issuer")]
    [InlineData("foreign audience")]
    [InlineData("not a token")]
    public async Task Get_InvalidToken_Returns401WithNoProcessData(string kind)
    {
        var (organizationId, _) = await SeedOrganizationAsync();
        var token = kind switch
        {
            "expired" => KeycloakTestRealm.CreateToken([], options => options.Expires = DateTimeOffset.UtcNow.AddMinutes(-10)),
            "foreign issuer" => KeycloakTestRealm.CreateToken([], options => options.Issuer = KeycloakTestRealm.ForeignIssuer),
            "foreign audience" => KeycloakTestRealm.CreateToken([], options => options.Audience = KeycloakTestRealm.ForeignClient),
            _ => "not-a-token",
        };

        using var response = await factory.CreateClient().SendAsync(Request(organizationId.ToString(), token));
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("workItemTypes", text, StringComparison.Ordinal);
        Assert.DoesNotContain("backlogLevels", text, StringComparison.Ordinal);
    }

    // US-2 · "A failed read is not taken for a missing organization": a failing read model gives 500, never the not-found code.
    [Fact]
    public async Task Get_ReadFails_Returns500WithoutTheNotFoundCode()
    {
        var (organizationId, _) = await SeedOrganizationAsync();

        // The derived host replaces Serilog's static logger and disposes it: put it back (see the guard).
        using var loggerGuard = new StaticLoggerGuard();
        using var failing = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IProcessDefinitionReadModel, ThrowingReadModel>()));

        var response = await failing.CreateClient().SendAsync(Request(organizationId.ToString(), SignedIn()));
        var text = await response.Content.ReadAsStringAsync();
        var body = JsonDocument.Parse(text).RootElement;

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("http.internal_error", body.GetProperty("errorCode").GetString());
        Assert.DoesNotContain(NotFoundCode, text, StringComparison.Ordinal);
        Assert.False(body.TryGetProperty("workItemTypes", out _));
    }

    // US-2 · "A visitor who is not signed in sees nothing": refused, with no process data in the answer (BR-M2-10).
    [Fact]
    public async Task Get_NoToken_Returns401WithNoProcessData()
    {
        var (organizationId, _) = await SeedOrganizationAsync();

        var (response, body) = await GetAsync(organizationId.ToString(), accessToken: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("http.unauthorized", body.GetProperty("errorCode").GetString());
        Assert.False(body.TryGetProperty("workItemTypes", out _));
        Assert.False(body.TryGetProperty("backlogLevels", out _));
    }

    // api-contract § 1.3: the read never answers 400 or 403, and an unsigned request is refused before the id is judged.
    [Fact]
    public async Task Get_NoToken_AndMalformedId_StillReturns401()
    {
        var (response, body) = await GetAsync("abc", accessToken: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("http.unauthorized", body.GetProperty("errorCode").GetString());
    }

    // US-1 · "No person can seed or change a process": the published API description holds exactly one operation of the
    // module, the read (BR-M2-13).
    [Fact]
    public async Task OpenApiDocument_ProcessOperations_AreOnlyTheRead()
    {
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();

        var operations = document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(operation => (Path: path.Name, Method: operation.Name.ToUpperInvariant(), Operation: operation.Value)))
            .ToList();
        Assert.NotEmpty(operations);

        bool Tagged(JsonElement operation) =>
            operation.TryGetProperty("tags", out var tags) && tags.EnumerateArray().Any(tag => tag.GetString() == "Process");

        var ofTheModule = operations
            .Where(operation => Tagged(operation.Operation) || operation.Path.Contains("process", StringComparison.OrdinalIgnoreCase))
            .Select(operation => $"{operation.Method} {operation.Path}")
            .ToList();
        Assert.Equal(["GET /api/organizations/{organizationId}/process"], ofTheModule);

        // Nothing changes a process: every operation on a process path is a read. (Since mod-002, the organizations
        // module writes under /api/organizations, so the rule is about the process paths, not the whole prefix.)
        Assert.All(
            operations.Where(operation => operation.Path.Contains("/process", StringComparison.OrdinalIgnoreCase)),
            operation => Assert.Equal("GET", operation.Method));
    }

    // mod-002 US-2 · "A deactivated organization answers not found" (its D-g): the process read answers
    // organizations.organization.not_found for a deactivated organization, and the process again once it is restored.
    [Fact]
    public async Task Get_DeactivatedOrganization_Returns404_RestoredAnswersAgain()
    {
        var (organizationId, processId) = await SeedOrganizationAsync();

        await OrganizationsTestData.SetOrganizationStatusAsync(factory, organizationId, OrganizationStatus.Deactivated);
        var (deactivated, deactivatedBody) = await GetAsync(organizationId.ToString(), SignedIn());

        await OrganizationsTestData.SetOrganizationStatusAsync(factory, organizationId, OrganizationStatus.Active);
        var (restored, restoredBody) = await GetAsync(organizationId.ToString(), SignedIn());

        Assert.Equal(HttpStatusCode.NotFound, deactivated.StatusCode);
        Assert.Equal(NotFoundCode, deactivatedBody.GetProperty("errorCode").GetString());
        Assert.False(deactivatedBody.TryGetProperty("workItemTypes", out _));
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.Equal(processId.Value, restoredBody.GetProperty("id").GetGuid());
    }

    // mod-002 api-contract § 9: a process with no organization row (as the old demonstration seed left one) is not found.
    [Fact]
    public async Task Get_ProcessWithoutAnOrganization_Returns404()
    {
        var organizationId = Guid.CreateVersion7();
        using (var scope = factory.Services.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var seed = scope.ServiceProvider.GetRequiredService<IProcessSeed>();
            await unitOfWork.ExecuteInTransactionAsync(ct => seed.SeedAsync(organizationId, ct), CancellationToken.None);
        }

        var (response, body) = await GetAsync(organizationId.ToString(), SignedIn());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(NotFoundCode, body.GetProperty("errorCode").GetString());
    }

    private static int CountNulls(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null => 1,
        JsonValueKind.Object => element.EnumerateObject().Sum(property => CountNulls(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().Sum(CountNulls),
        _ => 0,
    };

    /// <summary>A read model whose storage does not answer.</summary>
    private sealed class ThrowingReadModel : IProcessDefinitionReadModel
    {
        public Task<ProcessDefinitionRow?> FindByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The database does not answer.");
    }
}
