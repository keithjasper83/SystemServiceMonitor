using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.Monitoring.Providers;

public class WindowsServiceHealthCheckProvider : IHealthCheckProvider
{
    public ResourceType TargetType => ResourceType.WindowsService;

    public async Task<HealthCheckResult> CheckHealthAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        var result = new HealthCheckResult();

        if (string.IsNullOrWhiteSpace(resource.StartCommand))
        {
            result.HealthState = HealthState.Unknown;
            result.Message = "No service name specified.";
            return result;
        }

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"query {resource.StartCommand}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = await outputTask;

            if (output.Contains("STATE") && output.Contains("RUNNING"))
            {
                result.HealthState = HealthState.Healthy;
                result.Message = $"Service {resource.StartCommand} is RUNNING.";
            }
            else
            {
                result.HealthState = HealthState.Unhealthy;
                result.Message = $"Service {resource.StartCommand} is NOT RUNNING.";
            }
            result.Output = output;
        }
        catch (Exception ex)
        {
            result.HealthState = HealthState.Unhealthy;
            result.Message = $"Failed to query Windows Service: {ex.Message}";
        }

        return result;
    }
}
