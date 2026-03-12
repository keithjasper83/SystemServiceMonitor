using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.Repair;

public class DockerResourceController : IResourceController
{
    private readonly ILogger<DockerResourceController> _logger;

    public DockerResourceController(ILogger<DockerResourceController> logger)
    {
        _logger = logger;
    }

    public ResourceType TargetType => ResourceType.Docker;

    public async Task<bool> StartAsync(Resource resource)
    {
        return await RunDockerCommandAsync("start", resource.DockerIdentifier);
    }

    public async Task<bool> StopAsync(Resource resource)
    {
        return await RunDockerCommandAsync("stop", resource.DockerIdentifier);
    }

    public async Task<bool> RestartAsync(Resource resource)
    {
        return await RunDockerCommandAsync("restart", resource.DockerIdentifier);
    }

    private async Task<bool> RunDockerCommandAsync(string action, string? containerIdentifier)
    {
        if (string.IsNullOrWhiteSpace(containerIdentifier))
        {
            _logger.LogWarning("Docker action requires a DockerIdentifier.");
            return false;
        }

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"{action} {containerIdentifier}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                _logger.LogInformation("Docker {Action} exited with {ExitCode}. Out: {Out}, Err: {Err}", action, process.ExitCode, output, error);
                return process.ExitCode == 0;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run Docker command.");
            return false;
        }
    }
}
