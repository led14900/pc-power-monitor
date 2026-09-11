using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PcPowerMonitor.App;

namespace PcPowerMonitor.Tests.App;

/// <summary>
/// Regression tests for the application DI container.
/// These tests verify that the service graph can be constructed without circular dependencies
/// or missing registrations. No WPF types are instantiated (constructors not called), so no UI thread access.
/// </summary>
public class AppServiceGraphTests
{
    /// <summary>
    /// Verifies that ConfigureAppServices builds a valid DI container graph with no circular
    /// dependencies or missing service registrations. This test would have caught the
    /// AlertService → ITrayNotifier → TrayIconHost → MainViewModel cycle that shipped in
    /// an earlier version before Lazy&lt;TrayIconHost&gt; was introduced.
    /// </summary>
    [Fact]
    public void App_service_graph_builds_with_no_circular_or_missing_dependencies()
    {
        var services = new ServiceCollection();

        // Add logging provider so ILogger<T> call sites resolve.
        services.AddLogging();

        // Configure the application services graph.
        HostBuilderExtensions.ConfigureAppServices(services);

        // ValidateOnBuild scans every call site and detects circular dependencies and
        // missing registrations WITHOUT instantiating types (so no Dispatcher/UI access).
        var options = new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        };

        // This should not throw. If a cycle or missing service exists, it throws
        // InvalidOperationException during Build. ValidateOnBuild alone validates the graph
        // without running constructors, so this test does not access the UI dispatcher.
        var provider = services.BuildServiceProvider(options);

        // Verify the provider was created successfully (proves the graph is valid).
        Assert.NotNull(provider);
    }
}
