namespace Mustask.Infrastructure.Tests;

/// <summary>
/// The classes that read or set the process-wide <c>ConnectionStrings__mustask</c>
/// environment variable. Running them beside each other would race on it, so
/// the collection runs after the parallel ones, alone (tech-047).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessEnvironmentCollection
{
    public const string Name = "Process environment";
}
