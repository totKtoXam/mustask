using Xunit;

// Test classes run in parallel, each collection on a database of its own
// (tech-047). The classes that read or set the process-wide
// "ConnectionStrings__mustask" environment variable (the same one Aspire uses
// at runtime) share ProcessEnvironmentCollection, which runs alone. At most four at a
// time: the assembly runs beside Mustask.Api.IntegrationTests and its shards
// on the same machine.

[assembly: CollectionBehavior(MaxParallelThreads = 4)]
