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
            var processResult = await SystemServiceMonitor.Core.Utilities.ProcessHelper.RunProcessAsync(
                "docker",
                $"inspect --format=\"{{{{.State.Status}}}}\" {resource.DockerIdentifier}",
                cancellationToken: cancellationToken
            );

            var output = processResult.Output.Trim();

            if (processResult.ExitCode == 0 && output.Equals("running", StringComparison.OrdinalIgnoreCase))
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
        catch (Exception ex)
        {
            result.HealthState = HealthState.Unhealthy;
            result.Message = $"Docker check error: {ex.Message}";
        }

        return result;
    }
}
