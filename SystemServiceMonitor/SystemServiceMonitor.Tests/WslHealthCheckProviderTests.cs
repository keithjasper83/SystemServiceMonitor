using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using SystemServiceMonitor.Core.Models;
using SystemServiceMonitor.Core.Monitoring.Providers;

namespace SystemServiceMonitor.Tests;

public class WslHealthCheckProviderTests
{
    private class TestableWslHealthCheckProvider : WslHealthCheckProvider
    {
        public Func<ProcessStartInfo, CancellationToken, Task<(int, string)>>? ExecuteProcessAsyncMock { get; set; }
        public ProcessStartInfo? CapturedProcessInfo { get; private set; }

        protected override Task<(int ExitCode, string Output)> ExecuteProcessAsync(ProcessStartInfo processInfo, CancellationToken cancellationToken)
        {
            CapturedProcessInfo = processInfo;
            if (ExecuteProcessAsyncMock != null)
            {
                return ExecuteProcessAsyncMock(processInfo, cancellationToken);
            }
            return Task.FromResult((0, string.Empty));
        }
    }

    [Theory]
    [InlineData(null, "command")]
    [InlineData("", "command")]
    [InlineData("   ", "command")]
    [InlineData("distro", null)]
    [InlineData("distro", "")]
    [InlineData("distro", "   ")]
    public async Task CheckHealthAsync_ReturnsUnknown_WhenMissingConfig(string distroName, string command)
    {
        // Arrange
        var provider = new TestableWslHealthCheckProvider();
        var resource = new Resource { WslDistroName = distroName, HealthcheckCommand = command };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Unknown, result.HealthState);
        Assert.Contains("Missing HealthcheckCommand or WslDistroName", result.Message);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenExecutionSucceeds()
    {
        // Arrange
        var provider = new TestableWslHealthCheckProvider();
        provider.ExecuteProcessAsyncMock = (info, ct) => Task.FromResult((0, "success output"));
        var resource = new Resource { WslDistroName = "Ubuntu", HealthcheckCommand = "echo test" };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Healthy, result.HealthState);
        Assert.Equal("WSL healthcheck command succeeded.", result.Message);
        Assert.Equal("success output", result.Output);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenExecutionFails()
    {
        // Arrange
        var provider = new TestableWslHealthCheckProvider();
        provider.ExecuteProcessAsyncMock = (info, ct) => Task.FromResult((1, "error output"));
        var resource = new Resource { WslDistroName = "Ubuntu", HealthcheckCommand = "false" };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Contains("WSL healthcheck failed with exit code 1", result.Message);
        Assert.Equal("error output", result.Output);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenExceptionThrown()
    {
        // Arrange
        var provider = new TestableWslHealthCheckProvider();
        provider.ExecuteProcessAsyncMock = (info, ct) => throw new InvalidOperationException("process failed to start");
        var resource = new Resource { WslDistroName = "Ubuntu", HealthcheckCommand = "echo test" };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Contains("WSL check error", result.Message);
        Assert.Contains("process failed to start", result.Message);
    }

    [Fact]
    public async Task CheckHealthAsync_SetsCorrectProcessStartInfo()
    {
        // Arrange
        var provider = new TestableWslHealthCheckProvider();
        provider.ExecuteProcessAsyncMock = (info, ct) => Task.FromResult((0, ""));
        var resource = new Resource { WslDistroName = "Ubuntu", HealthcheckCommand = "echo test" };

        // Act
        await provider.CheckHealthAsync(resource);

        // Assert
        var info = provider.CapturedProcessInfo;
        Assert.NotNull(info);
        Assert.Equal("wsl.exe", info.FileName);
        Assert.True(info.RedirectStandardOutput);
        Assert.True(info.RedirectStandardError);
        Assert.False(info.UseShellExecute);
        Assert.True(info.CreateNoWindow);
        Assert.Equal("-d Ubuntu -- echo test", info.Arguments);
    }
}
