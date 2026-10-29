using Mustask.Api.IntegrationTests.Features.Authorization;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.Session;
using Mustask.Api.IntegrationTests.Features.UserDirectory;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: CollectionBehavior(
    "Mustask.Api.IntegrationTests.ApiTestCollectionFactory",
    "Mustask.Api.IntegrationTests")]

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// Splits <see cref="MustaskApiCollection"/> into shards that run in parallel
/// (tech-047). Every class still names <see cref="MustaskApiCollection.Name"/>;
/// this factory, not the class, decides which shard it lands in, so a new test
/// class needs nothing of its own. Each shard is an xUnit collection with its
/// own <see cref="MustaskApiFactory"/> and so its own database (a copy of the
/// migrated template on the assembly's one server): classes inside a shard run
/// one after another over one database, exactly as the whole collection did
/// before, and shards share nothing but the server.
///
/// A class that leans on process-wide state goes to
/// <see cref="MustaskApiSerialCollection"/> instead, which runs alone after the
/// shards: see <see cref="SerialClasses"/>. Every other class without the
/// collection gets a collection of its own, as xUnit's default does.
///
/// <c>MUSTASK_API_TEST_SHARDS</c> sets the number of shards; 1 runs the
/// shared collection as one, as before.
/// </summary>
public sealed class ApiTestCollectionFactory : IXunitTestCollectionFactory
{
    /// <summary>
    /// Classes that cannot run beside another shard. Each reason is
    /// process-wide state a host of another shard would see or change:
    /// <list type="bullet">
    /// <item>the console, captured with <see cref="ConsoleCapture"/> or
    /// <c>Console.SetOut</c> — another shard's log lines would land in it;</item>
    /// <item>environment variables set by the test, read by every host built
    /// while they are set;</item>
    /// <item>Serilog's static <c>Log.Logger</c>, which every host build and
    /// dispose replaces (tech-006): a test that asserts on log entries of a
    /// host it builds sees another host's logger in between;</item>
    /// <item>a host built straight from <c>WebApplicationFactory&lt;Program&gt;</c>,
    /// which reads whichever connection string the environment holds;</item>
    /// <item>a static flag another class resets;</item>
    /// <item>a timing budget, which a loaded machine misses (tech-011 t4).
    /// Every class named <c>*VolumeTests</c> goes here without a line.</item>
    /// </list>
    /// </summary>
    internal static readonly IReadOnlySet<Type> SerialClasses = new HashSet<Type>
    {
        // the console
        typeof(ApiExplorerTests),
        typeof(SessionLogoutEndpointTests),
        typeof(DeletionAuditLogQaTests),
        typeof(DeletionAuditLogTests),
        typeof(PushEnvelopeQaTests),
        typeof(PushNulCharacterQaTests),
        typeof(UserDirectoryLoggingTests),
        typeof(LoggingTests),
        typeof(ProductionLoggingTests),
        typeof(RealmReadinessTests),
        typeof(TokenLeakTests),

        // the console and environment variables
        typeof(PreviousSecretAuditTests),
        typeof(PushChannelAuthenticationTests),

        // environment variables
        typeof(CorsTests),
        typeof(KeycloakOptionsTests),

        // log entries of a host the test builds
        typeof(ErrorHandlingTests),
        typeof(PurgeWorkItemEndpointTests),
        typeof(PushSecretAuditQaTests),

        // a host straight from WebApplicationFactory<Program>
        typeof(ProductionCorsTests),

        // TestEndpointsStartupFilter.DenyAllProbeHandlerExecuted, shared with RealmReadinessTests
        typeof(DenyAllRuntimeTests),

        // timing budgets outside the *VolumeTests names
        typeof(GetAreaTreeEndpointTests),
        typeof(GetTeamsEndpointTests),
        typeof(GetWorkItemsEndpointTests),
        typeof(UserDirectoryQaTests),
    };

    private const string ShardsVariable = "MUSTASK_API_TEST_SHARDS";

    private readonly CollectionPerClassTestCollectionFactory _perClass;
    private readonly ITestCollection[] _shards;
    private readonly ITestCollection _serial;

    public ApiTestCollectionFactory(ITestAssembly testAssembly, IMessageSink diagnosticMessageSink)
    {
        _perClass = new CollectionPerClassTestCollectionFactory(testAssembly, diagnosticMessageSink);

        var definitions = TestCollectionFactoryHelper.GetTestCollectionDefinitions(testAssembly.Assembly, diagnosticMessageSink);
        var shards = ShardCount();

        _shards = Enumerable.Range(1, shards)
            .Select(shard => (ITestCollection)new TestCollection(
                testAssembly,
                definitions[MustaskApiCollection.Name],
                shards == 1 ? MustaskApiCollection.Name : $"{MustaskApiCollection.Name} {shard}/{shards}"))
            .ToArray();
        _serial = new TestCollection(testAssembly, definitions[MustaskApiSerialCollection.Name], MustaskApiSerialCollection.Name);
    }

    public string DisplayName => "Mustask.Api in shards";

    public ITestCollection Get(ITypeInfo testClass)
    {
        var collection = testClass.GetCustomAttributes(typeof(CollectionAttribute))
            .Select(attribute => attribute.GetConstructorArguments().FirstOrDefault() as string)
            .FirstOrDefault();

        if (collection != MustaskApiCollection.Name)
        {
            return _perClass.Get(testClass);
        }

        var type = testClass.ToRuntimeType();

        if (SerialClasses.Contains(type) || type.Name.EndsWith("VolumeTests", StringComparison.Ordinal))
        {
            return _serial;
        }

        return _shards[StableHash(testClass.Name) % (uint)_shards.Length];
    }

    // Half the cores: every shard runs a host and its share of the database
    // work on the same machine. Eight shards already leave the longest class,
    // not the split, as the bound.
    private static int ShardCount() =>
        int.TryParse(Environment.GetEnvironmentVariable(ShardsVariable), out var shards) && shards > 0
            ? shards
            : Math.Clamp(Environment.ProcessorCount / 2, 1, 8);

    // FNV-1a over the full name: string.GetHashCode differs per process, and a
    // class must land in the same shard on every run.
    private static uint StableHash(string value)
    {
        var hash = 2166136261;
        foreach (var character in value)
        {
            hash = (hash ^ character) * 16777619;
        }

        return hash;
    }
}
