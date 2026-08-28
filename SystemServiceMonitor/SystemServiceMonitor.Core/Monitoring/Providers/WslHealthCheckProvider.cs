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
            var processInfo = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                Arguments = $"-d {resource.WslDistroName} -- {resource.HealthcheckCommand}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var (exitCode, output) = await ExecuteProcessAsync(processInfo, cancellationToken);
            result.Output = output;

            if (exitCode == 0)
            {
                result.HealthState = HealthState.Healthy;
                result.Message = "WSL healthcheck command succeeded.";
            }
            else
            {
                result.HealthState = HealthState.Unhealthy;
                result.Message = $"WSL healthcheck failed with exit code {exitCode}.";
            }
        }
        catch (Exception ex)
        {
            result.HealthState = HealthState.Unhealthy;
            result.Message = $"WSL check error: {ex.Message}";
        }

        return result;
    }

    protected virtual async Task<(int ExitCode, string Output)> ExecuteProcessAsync(ProcessStartInfo processInfo, CancellationToken cancellationToken)
    {
        using var process = Process.Start(processInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start wsl.exe process.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await outputTask;

        return (process.ExitCode, output);
    }
}
