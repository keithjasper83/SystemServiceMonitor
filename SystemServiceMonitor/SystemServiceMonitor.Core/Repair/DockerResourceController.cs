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
            var result = await SystemServiceMonitor.Core.Utilities.ProcessHelper.RunProcessAsync(
                "docker",
                $"{action} {containerIdentifier}"
            );

            _logger.LogInformation("Docker {Action} exited with {ExitCode}. Out: {Out}, Err: {Err}", action, result.ExitCode, result.Output, result.Error);
            return result.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run Docker command.");
            return false;
        }
    }
}
