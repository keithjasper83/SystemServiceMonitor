using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.Repair;

public class WindowsServiceResourceController : IResourceController
{
    private readonly ILogger<WindowsServiceResourceController> _logger;

    public WindowsServiceResourceController(ILogger<WindowsServiceResourceController> logger)
    {
        _logger = logger;
    }

    public ResourceType TargetType => ResourceType.WindowsService;

    public async Task<bool> StartAsync(Resource resource)
    {
        return await RunScCommandAsync("start", resource.StartCommand);
    }

    public async Task<bool> StopAsync(Resource resource)
    {
         return await RunScCommandAsync("stop", resource.StartCommand);
    }

    public async Task<bool> RestartAsync(Resource resource)
    {
        await StopAsync(resource);
        await Task.Delay(2000); // Give it time to stop
        return await StartAsync(resource);
    }

     private async Task<bool> RunScCommandAsync(string action, string? serviceName)
    {
         if (string.IsNullOrWhiteSpace(serviceName))
        {
            _logger.LogWarning("Service name missing for sc.exe {Action}", action);
            return false;
        }

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"{action} {serviceName}",
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
                var err = await process.StandardError.ReadToEndAsync();

                _logger.LogInformation("sc.exe {Action} {ServiceName} exited with {Code}. Out: {Out}, Err: {Err}", action, serviceName, process.ExitCode, output, err);

                // Allow exit code 1056 (already running) or 1062 (not started) to loosely pass
                return process.ExitCode == 0 || process.ExitCode == 1056 || process.ExitCode == 1062;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute sc.exe {Action} on {ServiceName}", action, serviceName);
            return false;
        }
    }
}
