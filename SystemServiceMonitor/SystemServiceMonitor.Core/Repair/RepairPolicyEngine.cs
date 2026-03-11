using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SystemServiceMonitor.Core.Data;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.Repair;

public class RepairPolicyEngine : IRepairPolicyEngine
{
    private readonly IEnumerable<IResourceController> _controllers;
    private readonly ILogger<RepairPolicyEngine> _logger;
    private readonly IServiceProvider _serviceProvider;

    // Track consecutive failures per resource ID
    private readonly Dictionary<string, int> _failureCounts = new();
    private readonly Dictionary<string, DateTime> _lastRepairAttempt = new();

    public RepairPolicyEngine(IEnumerable<IResourceController> controllers, ILogger<RepairPolicyEngine> logger, IServiceProvider serviceProvider)
    {
        _controllers = controllers;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task HandleUnhealthyResourceAsync(Resource resource)
    {
        if (!resource.AutoRepairEnabled)
        {
            _logger.LogInformation("Auto-repair is disabled for resource {Id}.", resource.Id);
            return;
        }

        if (resource.RepairState == RepairState.Quarantined)
        {
            _logger.LogWarning("Resource {Id} is quarantined. Skipping repair.", resource.Id);
            return;
        }

        // Apply Cooldown Backoff
        if (_lastRepairAttempt.TryGetValue(resource.Id, out var lastAttempt))
        {
            if ((DateTime.UtcNow - lastAttempt).TotalSeconds < resource.CooldownSeconds)
            {
                _logger.LogInformation("Resource {Id} is in cooldown. Skipping repair.", resource.Id);
                return;
            }
        }

        // --- NEW: Check Dependencies ---
        if (!string.IsNullOrWhiteSpace(resource.DependencyIds))
        {
            var deps = resource.DependencyIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(d => d.Trim());
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            foreach (var depId in deps)
            {
                var depResource = await db.Resources.FirstOrDefaultAsync(r => r.Id == depId);
                if (depResource != null && depResource.HealthState != HealthState.Healthy)
                {
                    _logger.LogWarning("Cannot restart resource {Id} because dependency {DepId} is not healthy.", resource.Id, depId);
                    return; // Wait for next cycle
                }
            }
        }
        // -------------------------------

        var controller = _controllers.FirstOrDefault(c => c.TargetType == resource.Type);
        if (controller == null)
        {
            _logger.LogWarning("No controller found to repair resource type {Type}.", resource.Type);
            return;
        }

        var currentFailures = _failureCounts.GetValueOrDefault(resource.Id, 0);

        if (currentFailures >= resource.MaxRetries)
        {
            _logger.LogError("Resource {Id} exceeded max retries ({Max}). Transitioning to Quarantined.", resource.Id, resource.MaxRetries);
            resource.RepairState = RepairState.Quarantined;
            resource.ObservedState = ResourceState.Error;
            return;
        }

        // Attempt Repair
        _logger.LogInformation("Attempting to restart resource {Id}. Attempt {Attempt} of {Max}", resource.Id, currentFailures + 1, resource.MaxRetries);
        resource.RepairState = RepairState.Retrying;
        _lastRepairAttempt[resource.Id] = DateTime.UtcNow;

        var success = await controller.RestartAsync(resource);

        if (success)
        {
            _logger.LogInformation("Successfully restarted resource {Id}.", resource.Id);
            _failureCounts[resource.Id] = currentFailures + 1; // Count increments to prevent crash loops
        }
        else
        {
            _logger.LogError("Failed to restart resource {Id}.", resource.Id);
            _failureCounts[resource.Id] = currentFailures + 1;
        }
    }

    public void ResetFailures(string resourceId)
    {
        _failureCounts.Remove(resourceId);
        _lastRepairAttempt.Remove(resourceId);
    }
}
