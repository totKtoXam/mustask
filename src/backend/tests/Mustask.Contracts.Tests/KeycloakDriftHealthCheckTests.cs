using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Mustask.AppHost.Provisioning;
using Mustask.AppHost.Stand;

namespace Mustask.Contracts.Tests;

/// <summary>
/// mod-014 on the stand (ADR-0025 § 7; QA D-3): the drift check the way Aspire
/// runs it — registered as the AppHost registers it and probed again and again
/// through the health-check service — against the fake Keycloak of
/// <see cref="KeycloakStandDriftCheckTests"/>. No Docker, no Keycloak.
/// </summary>
public sealed class KeycloakDriftHealthCheckTests
{
    private const string MatchesLine = "Keycloak store: the live realm matches the realm import.";

    private static async Task<HealthStatus> ProbeAsync(HealthCheckService health) =>
        (await health.CheckHealthAsync()).Entries[UserProjectionIdentifiers.DriftHealthCheckKey].Status;

    private static ServiceProvider Stand(KeycloakStandDriftCheckTests.FakeKeycloak live, RecordingLogger log)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        KeycloakDriftHealthCheck.Register(services, _ => new KeycloakDriftHealthCheck(
            new Uri("http://keycloak.test"),
            _ => Task.FromResult(new KeycloakDriftCheckOptions
            {
                Import = KeycloakStandDriftCheckTests.Import(),
                AdminUsername = "admin",
                AdminPassword = KeycloakStandDriftCheckTests.AdminPassword,
                SecretClientId = "mustask-panel",
                SecretClientSecret = KeycloakStandDriftCheckTests.PanelSecret,
            }),
            log,
            live));

        return services.BuildServiceProvider();
    }

    private static KeycloakStandDriftCheckTests.FakeKeycloak LiveRealmFromTheImport() =>
        KeycloakStandDriftCheckTests.FakeKeycloak.FromImport(
            KeycloakStandDriftCheckTests.Import(), KeycloakStandDriftCheckTests.PanelSecret, KeycloakStandDriftCheckTests.AdminPassword);

    // ADR-0025 § 7 "A passing result is cached for the AppHost process": the admin surface is read once, the pass logged once.
    [Fact]
    public async Task APass_IsCachedForTheProcess_AcrossProbes()
    {
        var live = LiveRealmFromTheImport();
        var log = new RecordingLogger();
        await using var stand = Stand(live, log);
        var health = stand.GetRequiredService<HealthCheckService>();

        for (var probe = 0; probe < 5; probe++)
        {
            Assert.Equal(HealthStatus.Healthy, await ProbeAsync(health));
        }

        Assert.Equal(1, live.AdminSignIns);
        Assert.Single(log.Entries, entry => entry.Message == MatchesLine);
    }

    // US-5 lets an operator remove the listener from the realm during a run; the start check has passed by then,
    // so the running stand stays healthy and nothing tells anyone to reset it.
    [Fact]
    public async Task AListenerRemovedAfterThePass_LeavesTheRunningStandHealthy()
    {
        var live = LiveRealmFromTheImport();
        var log = new RecordingLogger();
        await using var stand = Stand(live, log);
        var health = stand.GetRequiredService<HealthCheckService>();

        Assert.Equal(HealthStatus.Healthy, await ProbeAsync(health));

        live.Listeners.Remove(UserProjectionIdentifiers.ProviderId);

        for (var probe = 0; probe < 3; probe++)
        {
            Assert.Equal(HealthStatus.Healthy, await ProbeAsync(health));
        }

        Assert.DoesNotContain(log.Entries, entry => entry.Level >= LogLevel.Error);
    }

    // The class's own promise: "each new finding is logged once". A failing check keeps checking, so restoring
    // the old value (the remedy for a changed secret or port) lets the stand come up without a restart.
    [Fact]
    public async Task AFinding_IsLoggedOnceAcrossProbes_AndARestoredRealmPasses()
    {
        var live = LiveRealmFromTheImport();
        live.Clients["mustask-api"].Roles.Add("work-items.read");
        var log = new RecordingLogger();
        await using var stand = Stand(live, log);
        var health = stand.GetRequiredService<HealthCheckService>();

        for (var probe = 0; probe < 4; probe++)
        {
            Assert.Equal(HealthStatus.Unhealthy, await ProbeAsync(health));
        }

        var finding = Assert.Single(log.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Contains("`mustask-api/work-items.read`", finding.Message, StringComparison.Ordinal);

        live.Clients["mustask-api"].Roles.Remove("work-items.read");

        Assert.Equal(HealthStatus.Healthy, await ProbeAsync(health));
        Assert.Single(log.Entries, entry => entry.Message == MatchesLine);
    }

    // The probe before Keycloak has imported the realm: unhealthy, nothing logged, nothing cached.
    [Fact]
    public async Task ARealmNotAnsweringYet_IsUnhealthyWithoutAFinding()
    {
        var live = LiveRealmFromTheImport();
        live.RealmExists = false;
        var log = new RecordingLogger();
        await using var stand = Stand(live, log);
        var health = stand.GetRequiredService<HealthCheckService>();

        Assert.Equal(HealthStatus.Unhealthy, await ProbeAsync(health));
        Assert.Empty(log.Entries);

        live.RealmExists = true;

        Assert.Equal(HealthStatus.Healthy, await ProbeAsync(health));
    }

    private sealed class RecordingLogger : ILogger
    {
        private readonly List<(LogLevel Level, string Message)> _entries = [];

        public IReadOnlyList<(LogLevel Level, string Message)> Entries
        {
            get
            {
                lock (_entries)
                {
                    return [.. _entries];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_entries)
            {
                _entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}
