using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using SystemServiceMonitor.Core.Data;
using SystemServiceMonitor.Core.Models;
using SystemServiceMonitor.Core.Repair;
using SystemServiceMonitor.Core.GitHub;

namespace SystemServiceMonitor.Core.Monitoring;

public class MonitoringEngine : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MonitoringEngine> _logger;

    public MonitoringEngine(IServiceProvider serviceProvider, ILogger<MonitoringEngine> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Monitoring Engine starting.");

        var configuration = _serviceProvider.GetRequiredService<IConfiguration>();
        var pollingIntervalStr = configuration["Monitoring:PollingIntervalSeconds"];
        if (!int.TryParse(pollingIntervalStr, out int pollingInterval))
        {
            pollingInterval = 10;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunMonitoringCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during monitoring cycle.");
            }

            await Task.Delay(TimeSpan.FromSeconds(pollingInterval), stoppingToken);
        }

        _logger.LogInformation("Monitoring Engine stopped.");
    }

    private async Task RunMonitoringCycleAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var healthCheckManager = scope.ServiceProvider.GetRequiredService<IHealthCheckManager>();
        var repairPolicyEngine = scope.ServiceProvider.GetService<IRepairPolicyEngine>();
        var gitHubMonitor = scope.ServiceProvider.GetService<IGitHubChangeMonitor>();
        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();

        // Check if resources are cached, if not fetch and cache them
        // Limit caching to a single polling cycle (15 seconds, or based on pollingInterval)
        // to prevent stale state. Better yet, invalidate it when a new resource is added.
        if (!cache.TryGetValue("MonitoredResources", out System.Collections.Generic.List<Resource>? resources) || resources == null)
        {
            resources = await dbContext.Resources.AsNoTracking().ToListAsync(stoppingToken);

            // Getting the polling interval here
            var configuration = _serviceProvider.GetRequiredService<IConfiguration>();
            var pollingIntervalStr = configuration["Monitoring:PollingIntervalSeconds"];
            if (!int.TryParse(pollingIntervalStr, out int pollingInterval))
            {
                pollingInterval = 10;
            }
            cache.Set("MonitoredResources", resources, TimeSpan.FromSeconds(pollingInterval));
        }

        // Keep track of modified resources to save to DB
        var modifiedResources = new System.Collections.Concurrent.ConcurrentBag<Resource>();

        // Cap parallelism for external checks to avoid overloading external systems
        const int MaxParallelHealthChecks = 8;
        var maxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, MaxParallelHealthChecks);

        await Parallel.ForEachAsync(resources, new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism, CancellationToken = stoppingToken }, async (resource, token) =>
        {
            // Pause monitoring if target desired state is Stopped
            if (resource.DesiredState == ResourceState.Stopped)
            {
                return;
            }

            var result = await healthCheckManager.ExecuteCheckAsync(resource, token);
            bool hasChanges = false;

            if (resource.HealthState != result.HealthState)
            {
                _logger.LogInformation("Resource {ResourceId} ({Name}) state changed from {OldState} to {NewState}: {Message}",
                    resource.Id, resource.DisplayName, resource.HealthState, result.HealthState, result.Message);

                resource.HealthState = result.HealthState;
                hasChanges = true;

                // Sync ObservedState based on HealthState
                if (result.HealthState == HealthState.Healthy)
                {
                    resource.ObservedState = ResourceState.Running;
                    resource.RepairState = RepairState.None;

                    repairPolicyEngine?.ResetFailures(resource.Id);
                }
                else if (result.HealthState == HealthState.Unhealthy)
                {
                    resource.ObservedState = ResourceState.Error;
                }
                else if (result.HealthState == HealthState.Unknown)
                {
                    resource.ObservedState = ResourceState.Unknown;
                }
            }

            // Trigger Repair Policy if Unhealthy
            if (result.HealthState == HealthState.Unhealthy && resource.DesiredState == ResourceState.Running && repairPolicyEngine != null)
            {
                await repairPolicyEngine.HandleUnhealthyResourceAsync(resource);
                // Assume repair policy changes state, let's mark it
                hasChanges = true;
            }

            // Optional GitHub monitoring
            if (gitHubMonitor != null && !string.IsNullOrWhiteSpace(resource.GitHubRepoUrl))
            {
                await gitHubMonitor.CheckForChangesAsync(resource);
            }

            if (hasChanges)
            {
                modifiedResources.Add(resource);
            }
        });

        // Save modifications to database in a thread-safe manner
        if (!modifiedResources.IsEmpty)
        {
            foreach (var modResource in modifiedResources)
            {
                // Attach and update existing entities or rely on update
                var trackedResource = await dbContext.Resources.FindAsync(new object[] { modResource.Id }, stoppingToken);
                if (trackedResource != null)
                {
                    trackedResource.HealthState = modResource.HealthState;
                    trackedResource.ObservedState = modResource.ObservedState;
                    trackedResource.RepairState = modResource.RepairState;
                }
            }
            await dbContext.SaveChangesAsync(stoppingToken);

            // Update cache to reflect changes
            var configuration = _serviceProvider.GetRequiredService<IConfiguration>();
            var pollingIntervalStr = configuration["Monitoring:PollingIntervalSeconds"];
            if (!int.TryParse(pollingIntervalStr, out int pollingInterval))
            {
                pollingInterval = 10;
            }
            cache.Set("MonitoredResources", await dbContext.Resources.AsNoTracking().ToListAsync(stoppingToken), TimeSpan.FromSeconds(pollingInterval));
        }
    }
}
