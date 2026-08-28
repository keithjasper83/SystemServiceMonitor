using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;
using SystemServiceMonitor.Core.Models;
using SystemServiceMonitor.Core.Monitoring.Providers;

namespace SystemServiceMonitor.Tests.Monitoring.Providers;

public class ProcessHealthCheckProviderTests
{
    private class TestableProcessHealthCheckProvider : ProcessHealthCheckProvider
    {
        public Func<string, Process[]> GetProcessesOverride { get; set; } = _ => Array.Empty<Process>();

        protected override Process[] GetProcesses(string processName)
        {
            return GetProcessesOverride(processName);
        }
    }

    [Fact]
    public void TargetType_ReturnsProcess()
    {
        // Arrange
        var provider = new ProcessHealthCheckProvider();

        // Act
        var result = provider.TargetType;

        // Assert
        Assert.Equal(ResourceType.Process, result);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnknown_WhenNoExecutable()
    {
        // Arrange
        var provider = new ProcessHealthCheckProvider();
        var resource = new Resource { Type = ResourceType.Process, StartCommand = null };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Unknown, result.HealthState);
        Assert.Contains("No executable specified", result.Message);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenProcessIsRunning()
    {
        // Arrange
        var provider = new TestableProcessHealthCheckProvider
        {
            GetProcessesOverride = _ => new[] { new Process() }
        };
        var resource = new Resource { Type = ResourceType.Process, StartCommand = "myprocess.exe" };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Healthy, result.HealthState);
        Assert.Contains("is running", result.Message);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenProcessIsNotRunning()
    {
        // Arrange
        var provider = new TestableProcessHealthCheckProvider
        {
            GetProcessesOverride = _ => Array.Empty<Process>()
        };
        var resource = new Resource { Type = ResourceType.Process, StartCommand = "myprocess.exe" };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Contains("is not running", result.Message);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenExceptionThrown()
    {
        // Arrange
        var provider = new TestableProcessHealthCheckProvider
        {
            GetProcessesOverride = _ => throw new InvalidOperationException("Test exception")
        };
        var resource = new Resource { Type = ResourceType.Process, StartCommand = "myprocess.exe" };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Contains("Failed to check process: Test exception", result.Message);
    }
}
