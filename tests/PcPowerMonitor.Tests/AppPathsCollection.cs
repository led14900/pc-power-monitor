namespace PcPowerMonitor.Tests;

/// <summary>
/// xUnit runs test classes in different collections in parallel. Any test that calls
/// <c>AppPaths.OverrideRoot</c> mutates a process-wide static field, so two such tests
/// running concurrently in different classes can race (one test's Save/reload observes
/// another test's override mid-flight). Every test class that touches
/// <c>AppPaths.OverrideRoot</c> must opt into this collection so xUnit serializes them
/// relative to each other instead of running them in parallel.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AppPathsCollection
{
    public const string Name = "AppPaths root override";
}
