namespace Mustask.Api.IntegrationTests;

/// <summary>
/// The collection every test class that boots Mustask.Api through
/// <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/>
/// names.
///
/// <see cref="ApiTestCollectionFactory"/> runs it as several shards in
/// parallel (tech-047). Each shard is a collection of its own with its own
/// <see cref="MustaskApiFactory"/>, so its own database on the assembly's one
/// PostgreSQL server; the classes of a shard run one after another, as the
/// whole collection did before. Code in <c>Program.cs</c> that runs before
/// <c>WebApplicationBuilder.Build()</c> reads process-wide environment
/// variables — <c>WebApplicationFactory</c>'s own
/// <c>ConfigureWebHost</c>/<c>UseSetting</c> overrides are applied too late
/// for it (see <see cref="MustaskApiFactory"/>) — so the factory builds every
/// host under one process-wide lock with its shard's connection string set,
/// and a class that sets such a variable itself runs in
/// <see cref="MustaskApiSerialCollection"/>, alone.
/// </summary>
[CollectionDefinition(Name)]
public sealed class MustaskApiCollection : ICollectionFixture<MustaskApiFactory>
{
    public const string Name = "Mustask.Api";
}

/// <summary>
/// The part of <see cref="MustaskApiCollection"/> that runs alone, after its
/// shards (tech-047): the classes <see cref="ApiTestCollectionFactory.SerialClasses"/>
/// names, and every <c>*VolumeTests</c> class. A class never names this
/// collection itself — it names <see cref="MustaskApiCollection.Name"/>, and
/// the factory moves it here.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MustaskApiSerialCollection : ICollectionFixture<MustaskApiFactory>
{
    public const string Name = "Mustask.Api (serial)";
}
