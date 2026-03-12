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
            var processInfo = new ProcessStartInfo
            {
                FileName = "wsl.exe",
                Arguments = $"-d {distroName} -- {command}",
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

                _logger.LogInformation("WSL Command exited with {ExitCode}. Out: {Out}, Err: {Err}", process.ExitCode, output, error);
                return process.ExitCode == 0;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run WSL command.");
            return false;
        }
    }
}
