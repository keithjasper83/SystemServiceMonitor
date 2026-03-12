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

    // Shell metacharacters that must never appear in an allowed command line
    private static readonly char[] _shellMetacharacters = { '&', '|', ';', '>', '<', '`', '$', '(', ')' };

    public async Task<(bool IsAllowed, string Output)> ExecuteSafeToolAsync(string commandLine)
    {
        // Reject any command containing shell metacharacters
        if (commandLine.IndexOfAny(_shellMetacharacters) >= 0)
        {
            _logger.LogWarning("Blocked unsafe AI tool execution attempt due to shell metacharacters: {Command}", commandLine);
            return (false, "Execution blocked by policy: shell metacharacters are not permitted.");
        }

        // Exact token-by-token prefix match: "ping" must not match "pingX", "docker logs" requires both tokens
        var commandTokens = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var isAllowed = _allowList.Any(allowed =>
        {
            var allowedTokens = allowed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (commandTokens.Length < allowedTokens.Length)
                return false;
            for (int i = 0; i < allowedTokens.Length; i++)
            {
                if (!string.Equals(commandTokens[i], allowedTokens[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        });

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
