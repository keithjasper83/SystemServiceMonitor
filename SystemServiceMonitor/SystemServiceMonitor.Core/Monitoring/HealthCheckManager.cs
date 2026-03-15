using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.Monitoring;

public class HealthCheckManager : IHealthCheckManager
{
    private readonly IReadOnlyDictionary<ResourceType, IHealthCheckProvider> _providerCache;
    private readonly ILogger<HealthCheckManager> _logger;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ResourceType, AsyncCircuitBreakerPolicy> _circuitBreakerPolicies = new();
    private readonly AsyncPolicy _timeoutPolicy;

    public HealthCheckManager(IEnumerable<IHealthCheckProvider> providers, ILogger<HealthCheckManager> logger)
    {
        // Cache providers to prevent repetitive LINQ FirstOrDefault allocations
        _providerCache = providers.ToDictionary(p => p.TargetType, p => p);
        _logger = logger;

        // Implement resilience with Polly:
        // Timeout after 15 seconds to prevent freezing on unresponsive WMI/HTTP calls
        _timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromSeconds(15));

        // Break circuit after 5 consecutive exceptions, wait 30 seconds before retrying
        // We create these policies per-provider/type lazily below
    }

    private AsyncCircuitBreakerPolicy GetCircuitBreakerPolicy(ResourceType type)
    {
        return _circuitBreakerPolicies.GetOrAdd(type, t =>
        {
            return Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (ex, breakDelay) =>
                    {
                        _logger.LogWarning(ex, "Health check circuit broken for type {ResourceType}. Halting checks for {Duration} seconds.", t, breakDelay.TotalSeconds);
                    },
                    onReset: () => _logger.LogInformation("Health check circuit reset for type {ResourceType}. Checks resuming.", t),
                    onHalfOpen: () => _logger.LogInformation("Health check circuit half-open for type {ResourceType}. Testing next check.", t)
                );
        });
    }

    public async Task<HealthCheckResult> ExecuteCheckAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        if (!_providerCache.TryGetValue(resource.Type, out var provider))
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
            var circuitBreakerPolicy = GetCircuitBreakerPolicy(resource.Type);

            // Execute with Polly resilience policies and propagate the policy CancellationToken into the provider
            return await circuitBreakerPolicy
                .WrapAsync(_timeoutPolicy)
                .ExecuteAsync(
                    (ct) => provider.CheckHealthAsync(resource, ct),
                    cancellationToken);
        }
        catch (Polly.Timeout.TimeoutRejectedException ex)
        {
            _logger.LogWarning(ex, "Health check timed out for resource: {ResourceId}", resource.Id);
            return new HealthCheckResult
            {
                HealthState = HealthState.Unknown,
                Message = "Health check timed out."
            };
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning("Circuit breaker open. Health check aborted for resource: {ResourceId}. Exception: {Message}", resource.Id, ex.Message);
            return new HealthCheckResult
            {
                HealthState = HealthState.Unknown,
                Message = "Circuit breaker open, check aborted."
            };
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
