using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.Monitoring.Providers;

public class DockerHealthCheckProvider : IHealthCheckProvider
{
    public ResourceType TargetType => ResourceType.Docker;

    public async Task<HealthCheckResult> CheckHealthAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        var result = new HealthCheckResult();

        if (string.IsNullOrWhiteSpace(resource.DockerIdentifier))
        {
            result.HealthState = HealthState.Unknown;
            result.Message = "Missing DockerIdentifier.";
            return result;
        }

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"inspect --format=\"{{{{.State.Status}}}}\" {resource.DockerIdentifier}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync(cancellationToken);
                var output = (await process.StandardOutput.ReadToEndAsync(cancellationToken)).Trim();

                if (process.ExitCode == 0 && output.Equals("running", StringComparison.OrdinalIgnoreCase))
                {
                    result.HealthState = HealthState.Healthy;
                    result.Message = $"Docker container {resource.DockerIdentifier} is running.";
                }
                else
                {
                    result.HealthState = HealthState.Unhealthy;
                    result.Message = $"Docker container {resource.DockerIdentifier} status is {output}.";
                }
                result.Output = output;
            }
            else
            {
                 result.HealthState = HealthState.Unhealthy;
                 result.Message = "Failed to start docker process.";
            }
        }
        catch (Exception ex)
        {
            result.HealthState = HealthState.Unhealthy;
            result.Message = $"Docker check error: {ex.Message}";
        }

        return result;
    }
}
