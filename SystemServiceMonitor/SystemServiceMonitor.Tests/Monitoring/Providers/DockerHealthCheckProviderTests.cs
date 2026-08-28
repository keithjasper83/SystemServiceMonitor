using System;
using System.Threading;
using System.Threading.Tasks;
using SystemServiceMonitor.Core.Models;
using SystemServiceMonitor.Core.Monitoring.Providers;
using Xunit;

namespace SystemServiceMonitor.Tests.Monitoring.Providers;

public class DockerHealthCheckProviderTests
{
    private class TestableDockerHealthCheckProvider : DockerHealthCheckProvider
    {
        public bool ShouldSucceed { get; set; } = true;
        public int MockExitCode { get; set; } = 0;
        public string MockOutput { get; set; } = "running";
        public bool ShouldThrow { get; set; } = false;

        protected override Task<(bool Success, int ExitCode, string Output)> ExecuteDockerCommandAsync(string identifier, CancellationToken cancellationToken)
        {
            if (ShouldThrow)
            {
                throw new Exception("Mock exception");
            }

            return Task.FromResult((ShouldSucceed, MockExitCode, MockOutput));
        }
    }

    [Fact]
    public async Task CheckHealthAsync_MissingIdentifier_ReturnsUnknown()
    {
        // Arrange
        var provider = new DockerHealthCheckProvider();
        var resource = new Resource
        {
            Type = ResourceType.Docker,
            DockerIdentifier = string.Empty
        };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Unknown, result.HealthState);
        Assert.Equal("Missing DockerIdentifier.", result.Message);
    }

    [Fact]
    public async Task CheckHealthAsync_RunningContainer_ReturnsHealthy()
    {
        // Arrange
        var provider = new TestableDockerHealthCheckProvider();
        var resource = new Resource
        {
            Type = ResourceType.Docker,
            DockerIdentifier = "my-container"
        };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Healthy, result.HealthState);
        Assert.Contains("is running", result.Message);
        Assert.Equal("running", result.Output);
    }

    [Fact]
    public async Task CheckHealthAsync_StoppedContainer_ReturnsUnhealthy()
    {
        // Arrange
        var provider = new TestableDockerHealthCheckProvider
        {
            MockExitCode = 0,
            MockOutput = "exited"
        };
        var resource = new Resource
        {
            Type = ResourceType.Docker,
            DockerIdentifier = "my-container"
        };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Contains("status is exited", result.Message);
        Assert.Equal("exited", result.Output);
    }

    [Fact]
    public async Task CheckHealthAsync_DockerCommandFails_ReturnsUnhealthy()
    {
        // Arrange
        var provider = new TestableDockerHealthCheckProvider
        {
            MockExitCode = 1,
            MockOutput = "Error: No such container: my-container"
        };
        var resource = new Resource
        {
            Type = ResourceType.Docker,
            DockerIdentifier = "my-container"
        };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Contains("status is Error", result.Message);
        Assert.Equal("Error: No such container: my-container", result.Output);
    }

    [Fact]
    public async Task CheckHealthAsync_ProcessStartFails_ReturnsUnhealthy()
    {
        // Arrange
        var provider = new TestableDockerHealthCheckProvider
        {
            ShouldSucceed = false
        };
        var resource = new Resource
        {
            Type = ResourceType.Docker,
            DockerIdentifier = "my-container"
        };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Equal("Failed to start docker process.", result.Message);
    }

    [Fact]
    public async Task CheckHealthAsync_ProcessThrowsException_ReturnsUnhealthy()
    {
        // Arrange
        var provider = new TestableDockerHealthCheckProvider
        {
            ShouldThrow = true
        };
        var resource = new Resource
        {
            Type = ResourceType.Docker,
            DockerIdentifier = "my-container"
        };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Contains("Docker check error: Mock exception", result.Message);
    }
}
