using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.Monitoring.Providers;

public class ProcessHealthCheckProvider : IHealthCheckProvider
{
    public ResourceType TargetType => ResourceType.Process;

    public Task<HealthCheckResult> CheckHealthAsync(Resource resource, System.Threading.CancellationToken cancellationToken = default)
    {
        var result = new HealthCheckResult();

        if (string.IsNullOrWhiteSpace(resource.StartCommand))
        {
            result.HealthState = HealthState.Unknown;
            result.Message = "No executable specified.";
            return Task.FromResult(result);
        }

        try
        {
            var processName = System.IO.Path.GetFileNameWithoutExtension(resource.StartCommand);
            var processes = Process.GetProcessesByName(processName);

            if (processes.Any())
            {
                result.HealthState = HealthState.Healthy;
                result.Message = $"Process {processName} is running.";
            }
            else
            {
                result.HealthState = HealthState.Unhealthy;
                result.Message = $"Process {processName} is not running.";
            }
        }
        catch (Exception ex)
        {
            result.HealthState = HealthState.Unhealthy;
            result.Message = $"Failed to check process: {ex.Message}";
        }

        return Task.FromResult(result);
    }
}
