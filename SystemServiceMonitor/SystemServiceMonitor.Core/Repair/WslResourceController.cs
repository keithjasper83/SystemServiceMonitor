using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.Repair;

public class WslResourceController : IResourceController
{
    private readonly ILogger<WslResourceController> _logger;

    public WslResourceController(ILogger<WslResourceController> logger)
    {
        _logger = logger;
    }

    public ResourceType TargetType => ResourceType.Wsl;

    public async Task<bool> StartAsync(Resource resource)
    {
        return await RunWslCommandAsync(resource.StartCommand, resource.WslDistroName);
    }

    public async Task<bool> StopAsync(Resource resource)
    {
        return await RunWslCommandAsync(resource.StopCommand, resource.WslDistroName);
    }

    public async Task<bool> RestartAsync(Resource resource)
    {
        if (!string.IsNullOrWhiteSpace(resource.RestartCommand))
        {
            return await RunWslCommandAsync(resource.RestartCommand, resource.WslDistroName);
        }

        await StopAsync(resource);
        await Task.Delay(1000);
        return await StartAsync(resource);
    }

    private async Task<bool> RunWslCommandAsync(string? command, string? distroName)
    {
        if (string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(distroName))
        {
            _logger.LogWarning("WSL start/stop requires both a command and WslDistroName.");
            return false;
        }

        try
        {
            var result = await SystemServiceMonitor.Core.Utilities.ProcessHelper.RunProcessAsync(
                "wsl.exe",
                $"-d {distroName} -- {command}"
            );

            _logger.LogInformation("WSL Command exited with {ExitCode}. Out: {Out}, Err: {Err}", result.ExitCode, result.Output, result.Error);
            return result.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run WSL command.");
            return false;
        }
    }
}
