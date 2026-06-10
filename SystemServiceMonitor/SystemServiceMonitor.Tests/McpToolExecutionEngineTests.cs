using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using SystemServiceMonitor.Core.AI;
using Xunit;

namespace SystemServiceMonitor.Tests;

public class McpToolExecutionEngineTests
{
    private class TestMcpToolExecutionEngine : McpToolExecutionEngine
    {
        public ProcessStartInfo? CapturedProcessInfo { get; private set; }

        public TestMcpToolExecutionEngine(ILogger<McpToolExecutionEngine> logger) : base(logger)
        {
        }

        protected override Task<(bool Success, string Output, int ExitCode)> ExecuteProcessAsync(ProcessStartInfo processInfo)
        {
            CapturedProcessInfo = processInfo;
            // Mock a successful execution response
            return Task.FromResult((true, "mocked output", 0));
        }
    }

    private readonly Mock<ILogger<McpToolExecutionEngine>> _loggerMock;
    private readonly TestMcpToolExecutionEngine _engine;

    public McpToolExecutionEngineTests()
    {
        _loggerMock = new Mock<ILogger<McpToolExecutionEngine>>();
        _engine = new TestMcpToolExecutionEngine(_loggerMock.Object);
    }

    [Theory]
    [InlineData("ping 127.0.0.1 & whoami")]
    [InlineData("docker logs mycontainer | grep password")]
    [InlineData("tasklist > output.txt")]
    [InlineData("sc query wuauserv ; rm -rf /")]
    public async Task ExecuteSafeToolAsync_BlocksShellMetacharacters(string commandLine)
    {
        // Act
        var result = await _engine.ExecuteSafeToolAsync(commandLine);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Contains("shell metacharacters are not permitted", result.Output);
        Assert.Null(_engine.CapturedProcessInfo);
    }

    [Theory]
    [InlineData("format C:")]
    [InlineData("powershell -Command Invoke-WebRequest")]
    [InlineData("pingX 127.0.0.1")]
    [InlineData("docker")] // allowlist has "docker logs" and "docker ps", but not just "docker"
    public async Task ExecuteSafeToolAsync_BlocksNonAllowlistedCommands(string commandLine)
    {
        // Act
        var result = await _engine.ExecuteSafeToolAsync(commandLine);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Contains("Execution blocked by policy allowlist", result.Output);
        Assert.Null(_engine.CapturedProcessInfo);
    }

    [Theory]
    [InlineData("ping 127.0.0.1", "ping", new[] { "127.0.0.1" })]
    [InlineData("docker logs mycontainer", "docker", new[] { "logs", "mycontainer" })]
    [InlineData("sc query wuauserv", "sc", new[] { "query", "wuauserv" })]
    [InlineData("tasklist", "tasklist", new string[0])]
    public async Task ExecuteSafeToolAsync_AllowsSafeCommandsAndSetsCorrectProcessInfo(string commandLine, string expectedFileName, string[] expectedArgs)
    {
        // Act
        var result = await _engine.ExecuteSafeToolAsync(commandLine);

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Equal("mocked output", result.Output);

        Assert.NotNull(_engine.CapturedProcessInfo);
        var processInfo = _engine.CapturedProcessInfo;
        Assert.Equal(expectedFileName, processInfo.FileName);
        Assert.False(processInfo.UseShellExecute);

        var expectedArgsString = string.Join(" ", expectedArgs);
        Assert.Equal(expectedArgsString, processInfo.Arguments);
    }
}
