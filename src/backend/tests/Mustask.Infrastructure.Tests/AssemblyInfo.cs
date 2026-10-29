using Xunit;

// Test classes run in parallel, each collection on a database of its own
// (tech-047). The classes that read or set the process-wide
// "ConnectionStrings__mustask" environment variable (the same one Aspire uses
// at runtime) share ProcessEnvironmentCollection, which runs alone.
