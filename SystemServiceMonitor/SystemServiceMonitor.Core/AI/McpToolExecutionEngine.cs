using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SystemServiceMonitor.Core.AI;

public interface IMcpToolExecutionEngine
{
    Task<(bool IsAllowed, string Output)> ExecuteSafeToolAsync(string commandLine);
}

public class McpToolExecutionEngine : IMcpToolExecutionEngine
{
    private readonly ILogger<McpToolExecutionEngine> _logger;

    // Explicit allowlist of commands safe for AI/MCP execution
    private readonly string[] _allowList = new[]
    {
        "ping",
        "netstat",
        "docker logs",
        "docker ps",
        "sc query",
        "tasklist"
    };

    public McpToolExecutionEngine(ILogger<McpToolExecutionEngine> logger)
    {
        _logger = logger;
    }

    public async Task<(bool IsAllowed, string Output)> ExecuteSafeToolAsync(string commandLine)
    {
        // Explicitly block shell metacharacters
        var blockedChars = new[] { "&", "|", ";", ">", "<", "`", "$" };
        if (blockedChars.Any(c => commandLine.Contains(c)))
        {
            _logger.LogWarning("Blocked unsafe AI tool execution attempt due to shell metacharacters: {Command}", commandLine);
            return (false, "Execution blocked: shell metacharacters are not allowed.");
        }

        var isAllowed = _allowList.Any(allowed => commandLine.StartsWith(allowed, StringComparison.OrdinalIgnoreCase));

        if (!isAllowed)
        {
            _logger.LogWarning("Blocked unsafe AI tool execution attempt: {Command}", commandLine);
            return (false, "Execution blocked by policy allowlist.");
        }

        try
        {
             var processInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {commandLine}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                var output = await process.StandardOutput.ReadToEndAsync();
                var err = await process.StandardError.ReadToEndAsync();

                _logger.LogInformation("Executed MCP Tool {Command} - ExitCode: {ExitCode}", commandLine, process.ExitCode);
                return (true, $"{output}\n{err}".Trim());
            }

            return (false, "Failed to start process.");
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Exception executing MCP Tool {Command}", commandLine);
             return (false, ex.Message);
        }
    }
}
