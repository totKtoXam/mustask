using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mustask.Api.Authorization;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Authorization;

/// <summary>
/// The inventory of organization-scoped access (mod-016 BR-M1-33, D-11, plan-backend § 4.2), fail-closed as the category inventory
/// is. It replaces mod-002's organization-context inventory: a route under <c>/api/organizations/{organizationId}</c> must declare
/// an organization code <b>and</b> the scoped check that enforces that very code; a route without the parameter declares none; a
/// write route never declares a read code; and — so that a route that declares the right code but enforces another fails — each
/// route is called with each role and refused exactly when the role's bundle lacks its code. A line that merges after this task and
/// adds a route without a code, or with the wrong one, turns <c>scripts/verify.sh</c> red.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class OrganizationAccessInventoryTests(MustaskApiFactory factory)
{
    private const string OrganizationParameter = "organizationId";

    /// <summary>
    /// The routes that may write with a read code, with the reason (BR-M1-33: "unless the test lists that route as an exception
    /// with its reason"). <b>Empty today</b>: every write route holds a write or deactivate code. A GET with a write code (the
    /// assignee candidates, api-contract § 4.2 row 34) is not an exception, it is legitimate.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> AllowedWriteWithReadCode = new Dictionary<string, string>(StringComparer.Ordinal);

    private static string KeyOf(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];

        // A group's "/" route keeps its trailing slash in the raw text; the lists name routes without it.
        return $"{string.Join("/", methods.Order(StringComparer.Ordinal))} /{endpoint.RoutePattern.RawText!.Trim('/')}";
    }

    private List<RouteEndpoint> RealEndpoints()
    {
        using var scope = factory.Services.CreateScope();

        return scope.ServiceProvider.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => !endpoint.RoutePattern.RawText!.StartsWith("/__test/", StringComparison.Ordinal))
            .ToList();
    }

    private List<RouteEndpoint> AddressedEndpoints() =>
        RealEndpoints().Where(endpoint => endpoint.RoutePattern.GetParameter(OrganizationParameter) is not null).ToList();

    private static EndpointCategoryMetadata? CategoryOf(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetOrderedMetadata<EndpointCategoryMetadata>().SingleOrDefault();

    // Canary: the scan reaches the routes of every module, or the tests below would pass on an empty set.
    [Fact]
    public void TheScan_ReachesTheRoutesOfEveryModule()
    {
        var addressed = AddressedEndpoints().Select(KeyOf).ToList();

        Assert.True(addressed.Count >= 40, $"Only {addressed.Count} organization-addressed routes were found.");
        Assert.Contains(addressed, key => key.Contains("/workitems", StringComparison.Ordinal));
        Assert.Contains(addressed, key => key.Contains("/backlogs/", StringComparison.Ordinal));
        Assert.Contains(addressed, key => key.EndsWith("/process", StringComparison.Ordinal));
        Assert.Contains(addressed, key => key.EndsWith("/access", StringComparison.Ordinal));
    }

    // BR-M1-33 · 1: every route with {organizationId} has the OrganizationPermission category, an organization code of the catalog,
    // and an authorization policy built from that very code — the code it declares is the code it enforces.
    [Fact]
    public void EveryRouteWithAnOrganizationId_DeclaresAnOrganizationCode_AndTheScopedCheckThatEnforcesIt()
    {
        var problems = new List<string>();

        foreach (var endpoint in AddressedEndpoints())
        {
            var key = KeyOf(endpoint);
            var categories = endpoint.Metadata.GetOrderedMetadata<EndpointCategoryMetadata>();

            if (categories.Count != 1 || categories[0].Kind != EndpointCategoryKind.OrganizationPermission || categories[0].PermissionCode is not { } code)
            {
                problems.Add($"{key}: declares no organization code (category: {string.Join(", ", categories.Select(category => category.Kind))}).");
                continue;
            }

            if (!PermissionCatalog.OrganizationCodes.Contains(code))
            {
                problems.Add($"{key}: declares {code}, which is not an organization code of the catalog.");
            }

            var expectedPolicy = PermissionPolicies.OrganizationPolicyName(code, categories[0].AcceptsDeactivated);
            var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy).ToList();

            if (!policies.Contains(expectedPolicy))
            {
                problems.Add($"{key}: declares {code} but its authorization policy is [{string.Join(", ", policies)}], not {expectedPolicy}.");
            }

            if (policies.Any(policy => policy is not null && policy != expectedPolicy))
            {
                problems.Add($"{key}: carries another policy than the scoped check of its code: [{string.Join(", ", policies)}].");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    // BR-M1-33 · 2: a route without {organizationId} is not scoped — it has neither the category nor an organization code (the two
    // lists, GET /api/organizations and /deactivated, narrow by membership inside their reads and say why).
    [Fact]
    public void NoRouteWithoutAnOrganizationId_HasTheOrganizationCategory_OrDeclaresAnOrganizationCode()
    {
        var problems = new List<string>();

        foreach (var endpoint in RealEndpoints().Where(endpoint => endpoint.RoutePattern.GetParameter(OrganizationParameter) is null))
        {
            if (CategoryOf(endpoint) is { } category
                && (category.Kind == EndpointCategoryKind.OrganizationPermission
                    || (category.PermissionCode is { } code && PermissionCatalog.OrganizationCodes.Contains(code))))
            {
                problems.Add($"{KeyOf(endpoint)}: has no {{{OrganizationParameter}}} but declares an organization code ({category.PermissionCode}).");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    // D-11 · "the authenticated-only reasons naming organization codes go away": a route that only needs a signed-in user says why in
    // its declaration, and the reason never names an organization code — a code in the words was a promise nothing kept (mod-003's
    // «declares X, which nothing checks yet»). An organization code is declared with RequiresOrganizationPermission or not at all.
    [Fact]
    public void NoAuthenticatedOnlyReason_NamesAnOrganizationCode()
    {
        var problems = RealEndpoints()
            .Select(endpoint => (Key: KeyOf(endpoint), Category: CategoryOf(endpoint)))
            .Where(route => route.Category is { Kind: EndpointCategoryKind.Authenticated })
            .SelectMany(route => PermissionCatalog.OrganizationCodes
                .Where(code => route.Category!.Reason!.Contains(code, StringComparison.Ordinal))
                .Select(code => $"{route.Key} names {code}"))
            .ToList();

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
        Assert.Contains(RealEndpoints(), endpoint => CategoryOf(endpoint) is { Kind: EndpointCategoryKind.Authenticated });
    }

    // The two lists are the exception that the rule above names: authenticated-only, with the reason stated in the declaration.
    [Fact]
    public void TheTwoOrganizationLists_AreAuthenticatedOnly_AndSayWhyTheyNarrowByMembership()
    {
        var lists = RealEndpoints()
            .Where(endpoint => KeyOf(endpoint) is "GET /api/organizations" or "GET /api/organizations/deactivated")
            .ToList();

        Assert.Equal(2, lists.Count);
        Assert.All(lists, endpoint =>
        {
            var category = CategoryOf(endpoint)!;
            Assert.Equal(EndpointCategoryKind.Authenticated, category.Kind);
            Assert.Contains("membership", category.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("any signed-in user, until mod-016", category.Reason, StringComparison.Ordinal);
        });
    }

    // BR-M1-33 · 3: no POST, PUT, PATCH or DELETE declares a code ending in .read, unless it is listed with its reason. A GET with a
    // write code (row 34) is fine.
    [Fact]
    public void NoWriteRoute_DeclaresAReadCode_UnlessListedWithItsReason()
    {
        var problems = WriteRoutesWithReadCodes(
            AddressedEndpoints().Select(endpoint => (
                Key: KeyOf(endpoint),
                Methods: (IReadOnlyCollection<string>)endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods,
                Code: CategoryOf(endpoint)?.PermissionCode)),
            AllowedWriteWithReadCode);

        Assert.True(problems.Count == 0, "Write routes declaring a read code: " + string.Join(", ", problems));
    }

    // The rule above is not vacuous: it names a write route that declares a read code, and spares the listed one and a GET.
    [Fact]
    public void TheWriteWithReadCodeRule_NamesAWriteRoute_SparesAGet_AndSparesAListedException()
    {
        (string Key, IReadOnlyCollection<string> Methods, string? Code)[] routes =
        [
            ("PUT /api/organizations/{organizationId}/x", ["PUT"], PermissionCatalog.OrganizationsProjectRead),
            ("GET /api/organizations/{organizationId}/y", ["GET"], PermissionCatalog.OrganizationsProjectRead),
            ("GET /api/organizations/{organizationId}/assignee", ["GET"], PermissionCatalog.WorkItemsWorkItemWrite),
            ("DELETE /api/organizations/{organizationId}/z", ["DELETE"], PermissionCatalog.OrganizationsAreaWrite),
            ("POST /api/organizations/{organizationId}/listed", ["POST"], PermissionCatalog.WorkItemsWorkItemRead),
        ];
        var allowed = new Dictionary<string, string>(StringComparer.Ordinal) { ["POST /api/organizations/{organizationId}/listed"] = "a stated reason" };

        Assert.Equal(["PUT /api/organizations/{organizationId}/x"], WriteRoutesWithReadCodes(routes, allowed));
        Assert.Empty(AllowedWriteWithReadCode);
    }

    private static List<string> WriteRoutesWithReadCodes(
        IEnumerable<(string Key, IReadOnlyCollection<string> Methods, string? Code)> routes, IReadOnlyDictionary<string, string> allowed) =>
        routes
            .Where(route => route.Methods.Any(method => method is "POST" or "PUT" or "PATCH" or "DELETE")
                && route.Code is { } code
                && code.EndsWith(".read", StringComparison.Ordinal)
                && !allowed.ContainsKey(route.Key))
            .Select(route => route.Key)
            .ToList();

    // BR-M1-33 · 4 / api-contract § 4.1 row 7: the organization's restore is the one request that accepts a deactivated organization.
    [Fact]
    public void OnlyTheOrganizationsRestore_AcceptsADeactivatedOrganization()
    {
        var accepting = AddressedEndpoints()
            .Where(endpoint => CategoryOf(endpoint) is { AcceptsDeactivated: true })
            .Select(KeyOf);

        Assert.Equal([OrganizationAddressedRoutes.Restore], accepting);
    }

    // BR-M1-33 · 5: the routes of the category are exactly the list the tests use — no more, no less — each with the code it
    // declares. Adding a route means adding it to OrganizationAddressedRoutes with its code.
    [Fact]
    public void TheScopedRoutes_AreExactlyTheListedOnes_EachWithItsListedCode()
    {
        var actual = AddressedEndpoints().ToDictionary(KeyOf, endpoint => CategoryOf(endpoint)?.PermissionCode, StringComparer.Ordinal);
        var expected = OrganizationAddressedRoutes.All.ToDictionary(route => route.Key, route => (string?)route.Code, StringComparer.Ordinal);

        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        Assert.All(expected, pair => Assert.Equal(pair.Value, actual[pair.Key]));
        Assert.Equal(49, expected.Count);
    }

    // Every organization code of the catalog is declared by at least one route — a code with no route is a decision nobody enforces
    // (and a new code without a route fails here and not silently).
    [Fact]
    public void EveryOrganizationCode_IsDeclaredByAtLeastOneRoute()
    {
        var declared = OrganizationAddressedRoutes.All.Select(route => route.Code).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(PermissionCatalog.OrganizationCodes.Order(StringComparer.Ordinal), declared.Order(StringComparer.Ordinal));
    }

    public static TheoryData<string, string, OrganizationRole> EveryRouteForEveryRole()
    {
        var data = new TheoryData<string, string, OrganizationRole>();

        foreach (var route in OrganizationAddressedRoutes.All)
        {
            foreach (var role in OrganizationRoles.All)
            {
                data.Add(route.Method, route.Template, role);
            }
        }

        return data;
    }

    // BR-M1-33 · 6, the behavioural proof: a route that declares the right code but enforces a wrong one fails here. Each route is
    // called by a member of each role, with fake nested ids and a well-formed body: the answer is 403 «Недостаточно прав» exactly
    // when the role's bundle lacks the route's code, and otherwise anything but that 403 and anything but the organization's 404.
    [Theory]
    [MemberData(nameof(EveryRouteForEveryRole))]
    public async Task EveryRoute_ForEveryRole_IsRefusedWith403_ExactlyWhenTheRolesBundleLacksTheRoutesCode(string method, string template, OrganizationRole role)
    {
        var route = OrganizationAddressedRoutes.All.Single(candidate => candidate.Method == method && candidate.Template == template);
        var organization = await OrganizationsTestData.CreateOrganizationAsync(
            factory,
            status: route.AcceptsDeactivated ? OrganizationStatus.Deactivated : OrganizationStatus.Active,
            founded: false);
        var caller = await MembershipTestData.NewMemberAsync(factory, organization.Id, role);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            new HttpMethod(method),
            OrganizationAddressedRoutes.Path(template, organization.Id.ToString()),
            OrganizationAddressedRoutes.Body(method),
            caller.Token);

        if (OrganizationRoleBundles.Holds(role, route.Code))
        {
            Assert.True(answer.Status != HttpStatusCode.Forbidden, $"{route.Key} refused a {role}, whose bundle holds {route.Code}.");
            Assert.True(
                answer.ErrorCode != OrganizationErrorCodes.OrganizationNotFound,
                $"{route.Key} answered a {role} of the organization as if it did not exist.");
        }
        else
        {
            Assert.True(
                answer.Status == HttpStatusCode.Forbidden && answer.ErrorCode == OrganizationErrorCodes.AccessForbidden,
                $"{route.Key} answered {(int)answer.Status} {answer.ErrorCode} to a {role}, whose bundle lacks {route.Code}.");
            Assert.Equal("Недостаточно прав", answer.Detail);
        }
    }

    // BR-M1-33 · 6, the fourth caller: a user of no role at all gets the byte-identical organization 404 on every route.
    [Fact]
    public async Task EveryRoute_ForANonMember_AnswersTheOrganizationsNotFound_WithTheSameShape()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, founded: false);
        var stranger = MembershipTestData.Outsider();
        var shapes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var route in OrganizationAddressedRoutes.All.Where(route => !route.AcceptsDeactivated))
        {
            var answer = await OrganizationsApi.SendAsync(
                factory,
                new HttpMethod(route.Method),
                OrganizationAddressedRoutes.Path(route.Template, organization.Id.ToString()),
                OrganizationAddressedRoutes.Body(route.Method),
                stranger);

            Assert.True(
                answer.Status == HttpStatusCode.NotFound && answer.ErrorCode == OrganizationErrorCodes.OrganizationNotFound,
                $"{route.Key} answered {(int)answer.Status} {answer.ErrorCode} to a non-member.");
            shapes.Add($"{(int)answer.Status} {answer.MediaType} {answer.Detail}");
        }

        Assert.Single(shapes);
    }

    // BR-M1-33 · 8: the published API description lists 403 on every organization-scoped operation (the two lists have no
    // organization to be forbidden from).
    [Fact]
    public async Task OpenApiDocument_ListsA403_OnEveryOrganizationScopedOperation()
    {
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();

        var scoped = document.GetProperty("paths").EnumerateObject()
            .Where(path => path.Name.Contains("{organizationId}", StringComparison.Ordinal))
            .SelectMany(path => path.Value.EnumerateObject()
                .Where(operation => operation.Value.ValueKind == JsonValueKind.Object && operation.Value.TryGetProperty("responses", out _))
                .Select(operation => (Key: $"{operation.Name.ToUpperInvariant()} {path.Name}", Responses: operation.Value.GetProperty("responses"))))
            .ToList();

        Assert.Equal(OrganizationAddressedRoutes.All.Count, scoped.Count);
        var without403 = scoped.Where(operation => !operation.Responses.TryGetProperty("403", out _)).Select(operation => operation.Key).ToList();
        Assert.True(without403.Count == 0, "Organization-scoped operations without a 403 in the API description: " + string.Join(", ", without403));
    }

    // plan-backend § 3.5 · a code in the wrong place would be a permanent silent 403, so the mapping refuses it at once.
    [Fact]
    public void ADeclarationWithACodeOfTheOtherScope_IsRefusedAtMappingTime()
    {
        var builder = new RecordingBuilder();

        var instanceAsOrganization = Assert.Throws<InvalidOperationException>(
            () => builder.RequiresOrganizationPermission(PermissionCatalog.DictionariesLanguagesRead));
        var organizationAsInstance = Assert.Throws<InvalidOperationException>(
            () => builder.RequiresPermission(PermissionCatalog.OrganizationsProjectRead));
        var unknown = Assert.Throws<ArgumentException>(() => builder.RequiresOrganizationPermission("organizations.nothing.read"));

        Assert.Contains("RequiresPermission", instanceAsOrganization.Message, StringComparison.Ordinal);
        Assert.Contains("RequiresOrganizationPermission", organizationAsInstance.Message, StringComparison.Ordinal);
        Assert.Contains("organizations.nothing.read", unknown.Message, StringComparison.Ordinal);
        Assert.Equal(0, builder.Conventions);
    }

    // The policy of an organization route: authenticated, and the requirement with the code; the restore family accepts a
    // deactivated organization — and only that family does.
    [Theory]
    [InlineData(false, "org:")]
    [InlineData(true, "org+restore:")]
    public async Task TheOrganizationPolicy_RequiresTheAuthenticatedUser_AndItsCode(bool acceptsDeactivated, string prefix)
    {
        var provider = new PermissionPolicies(Microsoft.Extensions.Options.Options.Create(new AuthorizationOptions()));
        var code = PermissionCatalog.OrganizationsProjectWrite;

        var name = PermissionPolicies.OrganizationPolicyName(code, acceptsDeactivated);
        var policy = await provider.GetPolicyAsync(name);

        Assert.Equal(prefix + code, name);
        Assert.NotNull(policy);
        Assert.Contains(policy.Requirements, requirement => requirement is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
        var scoped = Assert.Single(policy.Requirements.OfType<OrganizationPermissionRequirement>());
        Assert.Equal((code, acceptsDeactivated), (scoped.Code, scoped.AcceptsDeactivated));
    }

    /// <summary>An endpoint convention builder that only counts what is added to it.</summary>
    private sealed class RecordingBuilder : IEndpointConventionBuilder
    {
        public int Conventions { get; private set; }

        public void Add(Action<EndpointBuilder> convention) => Conventions++;

        public void Finally(Action<EndpointBuilder> finallyConvention)
        {
        }
    }
}
