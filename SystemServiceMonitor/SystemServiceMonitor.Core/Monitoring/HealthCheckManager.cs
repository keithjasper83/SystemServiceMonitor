using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.Monitoring;

public class HealthCheckManager : IHealthCheckManager
{
    private readonly IEnumerable<IHealthCheckProvider> _providers;
    private readonly ILogger<HealthCheckManager> _logger;

    public HealthCheckManager(IEnumerable<IHealthCheckProvider> providers, ILogger<HealthCheckManager> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    public async Task<HealthCheckResult> ExecuteCheckAsync(Resource resource)
    {
        var provider = _providers.FirstOrDefault(p => p.TargetType == resource.Type);
        if (provider == null)
        {
            _logger.LogWarning("No health check provider found for target type: {Type}", resource.Type);
            return new HealthCheckResult
            {
                HealthState = HealthState.Unknown,
                Message = $"No provider for target type {resource.Type}"
            };
        }

        try
        {
            return await provider.CheckHealthAsync(resource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute health check for resource: {ResourceId}", resource.Id);
            return new HealthCheckResult
            {
                HealthState = HealthState.Unhealthy,
                Message = $"Health check threw an exception: {ex.Message}"
            };
        }
    }
}
