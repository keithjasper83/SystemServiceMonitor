using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.Repair;

public class ProcessResourceController : IResourceController
{
    private readonly ILogger<ProcessResourceController> _logger;

    public ProcessResourceController(ILogger<ProcessResourceController> logger)
    {
        _logger = logger;
    }

    public ResourceType TargetType => ResourceType.Process;

    public Task<bool> StartAsync(Resource resource)
    {
        if (string.IsNullOrWhiteSpace(resource.StartCommand))
        {
            _logger.LogWarning("Cannot start Process resource {Id}: StartCommand is missing.", resource.Id);
            return Task.FromResult(false);
        }

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = resource.StartCommand,
                WorkingDirectory = resource.WorkingDirectory ?? string.Empty,
                UseShellExecute = true,
                Verb = resource.RequiresElevation ? "runas" : string.Empty
            };

            Process.Start(processInfo);
            _logger.LogInformation("Successfully started Process resource {Id}.", resource.Id);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Process resource {Id}.", resource.Id);
            return Task.FromResult(false);
        }
    }

    public Task<bool> StopAsync(Resource resource)
    {
        if (!string.IsNullOrWhiteSpace(resource.StopCommand))
        {
             return RunCommandAsync(resource.StopCommand, resource.WorkingDirectory);
        }

        // Fallback: kill process
        if (string.IsNullOrWhiteSpace(resource.StartCommand)) return Task.FromResult(false);
        try
        {
            var processName = System.IO.Path.GetFileNameWithoutExtension(resource.StartCommand);
            var processes = Process.GetProcessesByName(processName);
            foreach (var p in processes)
            {
                p.Kill();
            }
            _logger.LogInformation("Successfully killed Process resource {Id}.", resource.Id);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to kill Process resource {Id}.", resource.Id);
            return Task.FromResult(false);
        }
    }

    public async Task<bool> RestartAsync(Resource resource)
    {
        if (!string.IsNullOrWhiteSpace(resource.RestartCommand))
        {
            return await RunCommandAsync(resource.RestartCommand, resource.WorkingDirectory);
        }

        var stopped = await StopAsync(resource);
        if (!stopped) return false;

        await Task.Delay(1000); // Wait for graceful shutdown
        return await StartAsync(resource);
    }

    private Task<bool> RunCommandAsync(string command, string? workingDirectory)
    {
         try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                WorkingDirectory = workingDirectory ?? string.Empty,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var p = Process.Start(processInfo);
            if(p != null)
            {
                 p.WaitForExit();
                 return Task.FromResult(p.ExitCode == 0);
            }
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run command: {Command}", command);
            return Task.FromResult(false);
        }
    }
}
