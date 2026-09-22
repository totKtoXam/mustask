namespace Mustask.Api.IntegrationTests;

/// <summary>
/// Groups every test class that boots Mustask.Api through
/// <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/>
/// into a single xUnit collection.
///
/// Two reasons this matters:
/// 1. <see cref="MustaskApiFactory"/> is registered here as the one
///    <c>ICollectionFixture</c> instance for the whole collection, so all
///    test classes that need a real Api + Postgres share a single
///    Testcontainers container instead of each <c>IClassFixture</c>
///    spinning up its own.
/// 2. xUnit runs different collections in parallel by default, but classes
///    within the same collection run sequentially. Both <see cref="MustaskApiFactory"/>
///    and any other test class in this collection that needs to influence
///    <c>Program.cs</c>'s top-level statements (e.g. a custom
///    <c>Cors:AllowedOrigins</c> value) has to do so via a process-wide
///    environment variable — <c>WebApplicationFactory</c>'s own
///    <c>ConfigureWebHost</c>/<c>UseSetting</c> overrides are applied too late
///    to affect code that runs before <c>WebApplicationBuilder.Build()</c>
///    (see <see cref="MustaskApiFactory"/> for why). Sharing this collection
///    keeps every such environment-variable mutation sequential, removing
///    the race between test classes that the previous per-class
///    <c>IClassFixture</c> setup had.
/// </summary>
[CollectionDefinition(Name)]
public sealed class MustaskApiCollection : ICollectionFixture<MustaskApiFactory>
{
    public const string Name = "Mustask.Api";
}
