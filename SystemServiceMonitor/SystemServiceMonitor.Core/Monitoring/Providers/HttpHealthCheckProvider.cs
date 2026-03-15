using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.Monitoring.Providers;

public class HttpHealthCheckProvider : IHealthCheckProvider
{
    private readonly HttpClient _httpClient;

    public HttpHealthCheckProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public ResourceType TargetType => ResourceType.Http;

    public async Task<HealthCheckResult> CheckHealthAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        var result = new HealthCheckResult();

        if (string.IsNullOrWhiteSpace(resource.HealthcheckCommand))
        {
            result.HealthState = HealthState.Unknown;
            result.Message = "No URL specified in HealthcheckCommand.";
            return result;
        }

        try
        {
            var timeoutSeconds = resource.TimeoutSeconds;
            if (timeoutSeconds <= 0)
            {
                timeoutSeconds = 30;
            }
            else if (timeoutSeconds > 300)
            {
                timeoutSeconds = 300;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            var response = await _httpClient.GetAsync(resource.HealthcheckCommand, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                result.HealthState = HealthState.Healthy;
                result.Message = $"HTTP check successful ({response.StatusCode}).";
            }
            else
            {
                result.HealthState = HealthState.Unhealthy;
                result.Message = $"HTTP check failed ({response.StatusCode}).";
            }
        }
        catch (Exception ex)
        {
            result.HealthState = HealthState.Unhealthy;
            result.Message = $"HTTP check error: {ex.Message}";
        }

        return result;
    }
}
