using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using SystemServiceMonitor.Core.Models;
using SystemServiceMonitor.Core.Monitoring;
using SystemServiceMonitor.Core.Monitoring.Providers;

namespace SystemServiceMonitor.Tests;

public class MonitoringTests
{
    [Fact]
    public async Task ProcessHealthCheck_ReturnsUnknown_WhenNoExecutable()
    {
        var provider = new ProcessHealthCheckProvider();
        var resource = new Resource { Type = ResourceType.Process, StartCommand = null };
        var result = await provider.CheckHealthAsync(resource);

        Assert.Equal(HealthState.Unknown, result.HealthState);
    }

    [Fact]
    public async Task HttpHealthCheck_ReturnsUnknown_WhenNoUrl()
    {
        var httpClient = new HttpClient();
        var provider = new HttpHealthCheckProvider(httpClient);
        var resource = new Resource { Type = ResourceType.Http, HealthcheckCommand = null };
        var result = await provider.CheckHealthAsync(resource);

        Assert.Equal(HealthState.Unknown, result.HealthState);
    }

    [Fact]
    public async Task HealthCheckManager_ReturnsUnknown_WhenNoProvider()
    {
        var providers = new IHealthCheckProvider[] { };
        var logger = new Mock<ILogger<HealthCheckManager>>();
        var manager = new HealthCheckManager(providers, logger.Object);
        var resource = new Resource { Type = ResourceType.Docker }; // Assuming we haven't registered Docker provider yet
        var result = await manager.ExecuteCheckAsync(resource);

        Assert.Equal(HealthState.Unknown, result.HealthState);
    }

    [Fact]
    public async Task HealthCheckManager_ExecutesProvider_WhenProviderExists()
    {
        var mockProvider = new Mock<IHealthCheckProvider>();
        mockProvider.Setup(p => p.TargetType).Returns(ResourceType.Process);
        mockProvider.Setup(p => p.CheckHealthAsync(It.IsAny<Resource>(), It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(new HealthCheckResult { HealthState = HealthState.Healthy });

        var logger = new Mock<ILogger<HealthCheckManager>>();
        var manager = new HealthCheckManager(new[] { mockProvider.Object }, logger.Object);

        var resource = new Resource { Type = ResourceType.Process };
        var result = await manager.ExecuteCheckAsync(resource);

        Assert.Equal(HealthState.Healthy, result.HealthState);
    }

    [Fact]
    public async Task HealthCheckManager_ReturnsUnhealthy_WhenGenericExceptionThrown()
    {
        var mockProvider = new Mock<IHealthCheckProvider>();
        mockProvider.Setup(p => p.TargetType).Returns(ResourceType.Process);
        mockProvider.Setup(p => p.CheckHealthAsync(It.IsAny<Resource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Exception("Test error"));

        var logger = new Mock<ILogger<HealthCheckManager>>();
        var manager = new HealthCheckManager(new[] { mockProvider.Object }, logger.Object);

        var resource = new Resource { Type = ResourceType.Process };
        var result = await manager.ExecuteCheckAsync(resource);

        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Equal("Health check threw an exception: Test error", result.Message);
    }

    [Fact]
    public async Task HealthCheckManager_ReturnsUnknown_WhenBrokenCircuitExceptionThrown()
    {
        var mockProvider = new Mock<IHealthCheckProvider>();
        mockProvider.Setup(p => p.TargetType).Returns(ResourceType.Process);
        mockProvider.Setup(p => p.CheckHealthAsync(It.IsAny<Resource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Exception("Test error"));

        var logger = new Mock<ILogger<HealthCheckManager>>();
        var manager = new HealthCheckManager(new[] { mockProvider.Object }, logger.Object);

        var resource = new Resource { Type = ResourceType.Process };

        // Break the circuit
        for (int i = 0; i < 5; i++)
        {
            var r = await manager.ExecuteCheckAsync(resource);
            Assert.Equal(HealthState.Unhealthy, r.HealthState); // The first 5 should be handled as generic exceptions
        }

        // 6th call should hit the broken circuit
        var result = await manager.ExecuteCheckAsync(resource);

        Assert.Equal(HealthState.Unknown, result.HealthState);
        Assert.Equal("Circuit breaker open, check aborted.", result.Message);
    }

    [Fact]
    public async Task HealthCheckManager_ReturnsUnknown_WhenTimeoutRejectedExceptionThrown()
    {
        var mockProvider = new Mock<IHealthCheckProvider>();
        mockProvider.Setup(p => p.TargetType).Returns(ResourceType.Process);

        // Simulate a long-running check that will exceed the 15-second Polly timeout
        mockProvider.Setup(p => p.CheckHealthAsync(It.IsAny<Resource>(), It.IsAny<CancellationToken>()))
            .Returns(async (Resource res, CancellationToken ct) =>
            {
                await Task.Delay(System.TimeSpan.FromSeconds(20), ct);
                return new HealthCheckResult { HealthState = HealthState.Healthy };
            });

        var logger = new Mock<ILogger<HealthCheckManager>>();
        var manager = new HealthCheckManager(new[] { mockProvider.Object }, logger.Object);

        var resource = new Resource { Type = ResourceType.Process };

        // This will throw a TimeoutRejectedException internally, which the manager catches
        var result = await manager.ExecuteCheckAsync(resource);

        Assert.Equal(HealthState.Unknown, result.HealthState);
        Assert.Equal("Health check timed out.", result.Message);
    }
}
