using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.Monitoring.Providers;

public class WslHealthCheckProvider : IHealthCheckProvider
{
    public ResourceType TargetType => ResourceType.Wsl;

    public async Task<HealthCheckResult> CheckHealthAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        var result = new HealthCheckResult();

        if (string.IsNullOrWhiteSpace(resource.HealthcheckCommand) || string.IsNullOrWhiteSpace(resource.WslDistroName))
        {
            result.HealthState = HealthState.Unknown;
            result.Message = "Missing HealthcheckCommand or WslDistroName.";
            return result;
        }

        try
        {
            var processResult = await SystemServiceMonitor.Core.Utilities.ProcessHelper.RunProcessAsync(
                "wsl.exe",
                $"-d {resource.WslDistroName} -- {resource.HealthcheckCommand}",
                cancellationToken: cancellationToken
            );

            result.Output = processResult.Output;

            if (processResult.ExitCode == 0)
            {
                result.HealthState = HealthState.Healthy;
                result.Message = "WSL healthcheck command succeeded.";
            }
            else
            {
                result.HealthState = HealthState.Unhealthy;
                result.Message = $"WSL healthcheck failed with exit code {processResult.ExitCode}.";
            }
        }
        catch (Exception ex)
        {
            result.HealthState = HealthState.Unhealthy;
            result.Message = $"WSL check error: {ex.Message}";
        }

        return result;
    }
}
