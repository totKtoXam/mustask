using Xunit;

// MustaskDbContextFactoryTests and MigrationsTests both read/mutate the process-wide
// "ConnectionStrings__mustask" environment variable (the same one Aspire uses at
// runtime) — running test classes in parallel would race on it.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
