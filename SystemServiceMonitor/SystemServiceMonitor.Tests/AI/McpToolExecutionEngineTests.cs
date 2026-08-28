using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using SystemServiceMonitor.Core.AI;
using Xunit;

namespace SystemServiceMonitor.Tests.AI;

public class McpToolExecutionEngineTests
{
    private readonly Mock<ILogger<McpToolExecutionEngine>> _mockLogger;

    public McpToolExecutionEngineTests()
    {
        _mockLogger = new Mock<ILogger<McpToolExecutionEngine>>();
    }

    [Theory]
    [InlineData("ping 8.8.8.8 & echo hack")]
    [InlineData("docker ps | grep image")]
    [InlineData("tasklist ; calc.exe")]
    [InlineData("ping > output.txt")]
    [InlineData("ping < input.txt")]
    [InlineData("echo `whoami`")]
    [InlineData("echo $USER")]
    [InlineData("echo $(whoami)")]
    public async Task ExecuteSafeToolAsync_RejectsMetacharacters(string command)
    {
        // Arrange
        var engine = new TestMcpToolExecutionEngine(_mockLogger.Object);
        bool processExecuted = false;
        engine.MockExecuteProcessAsync = (info, cmd) =>
        {
            processExecuted = true;
            return Task.FromResult((true, "mock output"));
        };

        // Act
        var result = await engine.ExecuteSafeToolAsync(command);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Contains("shell metacharacters", result.Output);
        Assert.False(processExecuted, "Process should not have been executed.");
    }

    [Theory]
    [InlineData("format c:")]
    [InlineData("docker run malware")]
    [InlineData("powershell -c ls")]
    [InlineData("pingX 8.8.8.8")] // Matches prefix partially but not a valid whole token
    [InlineData("docker")] // Incomplete allowed command ("docker logs" or "docker ps" is allowed)
    public async Task ExecuteSafeToolAsync_RejectsUnapprovedCommands(string command)
    {
        // Arrange
        var engine = new TestMcpToolExecutionEngine(_mockLogger.Object);
        bool processExecuted = false;
        engine.MockExecuteProcessAsync = (info, cmd) =>
        {
            processExecuted = true;
            return Task.FromResult((true, "mock output"));
        };

        // Act
        var result = await engine.ExecuteSafeToolAsync(command);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Contains("policy allowlist", result.Output);
        Assert.False(processExecuted, "Process should not have been executed.");
    }

    [Theory]
    [InlineData("ping 8.8.8.8 -n 4")]
    [InlineData("docker logs my-container")]
    [InlineData("docker ps -a")]
    [InlineData("sc query w3svc")]
    [InlineData("netstat -an")]
    [InlineData("tasklist")]
    public async Task ExecuteSafeToolAsync_AllowsApprovedCommands(string command)
    {
        // Arrange
        var engine = new TestMcpToolExecutionEngine(_mockLogger.Object);
        bool processExecuted = false;
        ProcessStartInfo? capturedProcessInfo = null;
        string capturedCmd = string.Empty;

        engine.MockExecuteProcessAsync = (info, cmd) =>
        {
            processExecuted = true;
            capturedProcessInfo = info;
            capturedCmd = cmd;
            return Task.FromResult((true, "mock output from allowed command"));
        };

        // Act
        var result = await engine.ExecuteSafeToolAsync(command);

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Equal("mock output from allowed command", result.Output);
        Assert.True(processExecuted, "Process should have been executed.");

        // Verify start info mapping
        Assert.NotNull(capturedProcessInfo);
        Assert.Equal("cmd.exe", capturedProcessInfo.FileName);
        Assert.Equal($"/c {command}", capturedProcessInfo.Arguments);
        Assert.Equal(command, capturedCmd);
    }

    [Fact]
    public async Task ExecuteSafeToolAsync_HandlesExceptions()
    {
        // Arrange
        var engine = new TestMcpToolExecutionEngine(_mockLogger.Object);
        engine.MockExecuteProcessAsync = (info, cmd) =>
        {
            throw new InvalidOperationException("Simulated process execution failure");
        };

        // Act
        var result = await engine.ExecuteSafeToolAsync("ping localhost");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal("Simulated process execution failure", result.Output);
    }

    private class TestMcpToolExecutionEngine : McpToolExecutionEngine
    {
        public Func<ProcessStartInfo, string, Task<(bool, string)>>? MockExecuteProcessAsync { get; set; }

        public TestMcpToolExecutionEngine(ILogger<McpToolExecutionEngine> logger) : base(logger)
        {
        }

        protected override Task<(bool IsAllowed, string Output)> ExecuteProcessAsync(ProcessStartInfo processInfo, string commandLine)
        {
            if (MockExecuteProcessAsync != null)
            {
                return MockExecuteProcessAsync(processInfo, commandLine);
            }
            return Task.FromResult((true, "Default mock success"));
        }
    }
}
