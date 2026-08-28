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
            var (success, exitCode, output) = await ExecuteDockerCommandAsync(resource.DockerIdentifier, cancellationToken);

            if (success)
            {
                if (exitCode == 0 && output.Equals("running", StringComparison.OrdinalIgnoreCase))
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

    protected virtual async Task<(bool Success, int ExitCode, string Output)> ExecuteDockerCommandAsync(string identifier, CancellationToken cancellationToken)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "docker",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { "inspect", "--format={{.State.Status}}", identifier }
        };

        using var process = Process.Start(processInfo);
        if (process != null)
        {
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = (await outputTask).Trim();
            return (true, process.ExitCode, output);
        }

        return (false, -1, string.Empty);
    }
}
