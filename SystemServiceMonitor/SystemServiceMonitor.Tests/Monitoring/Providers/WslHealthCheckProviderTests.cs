using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SystemServiceMonitor.Core.Models;
using SystemServiceMonitor.Core.Monitoring;
using SystemServiceMonitor.Core.Monitoring.Providers;
using Xunit;

namespace SystemServiceMonitor.Tests.Monitoring.Providers;

public class WslHealthCheckProviderTests
{
    public class TestableWslHealthCheckProvider : WslHealthCheckProvider
    {
        public Process? MockProcess { get; set; }
        public Exception? ExceptionToThrow { get; set; }

        protected override Process? StartProcess(ProcessStartInfo processInfo)
        {
            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }
            return MockProcess;
        }
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnknown_WhenResourceIsNull()
    {
        // Arrange
        var provider = new TestableWslHealthCheckProvider();

        // Act
        var result = await provider.CheckHealthAsync(null!);

        // Assert
        Assert.Equal(HealthState.Unknown, result.HealthState);
        Assert.Equal("Resource is null.", result.Message);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnknown_WhenHealthcheckCommandIsMissing()
    {
        // Arrange
        var provider = new TestableWslHealthCheckProvider();
        var resource = new Resource
        {
            WslDistroName = "Ubuntu",
            HealthcheckCommand = ""
        };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Unknown, result.HealthState);
        Assert.Contains("Missing", result.Message);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnknown_WhenWslDistroNameIsMissing()
    {
        // Arrange
        var provider = new TestableWslHealthCheckProvider();
        var resource = new Resource
        {
            WslDistroName = "",
            HealthcheckCommand = "echo hi"
        };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Unknown, result.HealthState);
        Assert.Contains("Missing", result.Message);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenProcessExitsWithZero()
    {
        // Arrange
        var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
        var processInfo = new ProcessStartInfo
        {
            FileName = isWindows ? "cmd.exe" : "echo",
            Arguments = isWindows ? "/c echo success" : "success",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var process = Process.Start(processInfo);

        var provider = new TestableWslHealthCheckProvider { MockProcess = process };
        var resource = new Resource
        {
            WslDistroName = "Ubuntu",
            HealthcheckCommand = "success_command"
        };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Healthy, result.HealthState);
        Assert.Contains("succeeded", result.Message);
        Assert.Contains("success", result.Output);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenProcessExitsWithNonZero()
    {
        // Arrange
        var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
        var processInfo = new ProcessStartInfo
        {
            FileName = isWindows ? "cmd.exe" : "ls",
            Arguments = isWindows ? "/c exit 1" : "/nonexistent_path_for_testing",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var process = Process.Start(processInfo);

        var provider = new TestableWslHealthCheckProvider { MockProcess = process };
        var resource = new Resource
        {
            WslDistroName = "Ubuntu",
            HealthcheckCommand = "fail_command"
        };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Contains("failed with exit code", result.Message);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenStartProcessThrowsException()
    {
        // Arrange
        var provider = new TestableWslHealthCheckProvider
        {
            ExceptionToThrow = new Win32Exception("File not found")
        };
        var resource = new Resource
        {
            WslDistroName = "Ubuntu",
            HealthcheckCommand = "command"
        };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Contains("WSL check error: File not found", result.Message);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenStartProcessReturnsNull()
    {
        // Arrange
        var provider = new TestableWslHealthCheckProvider
        {
            MockProcess = null // StartProcess returns null
        };
        var resource = new Resource
        {
            WslDistroName = "Ubuntu",
            HealthcheckCommand = "command"
        };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Contains("Failed to start wsl.exe process.", result.Message);
    }
}
