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
    public async Task HealthCheckManager_ReturnsUnknown_WhenCircuitBreakerTrips()
    {
        var mockProvider = new Mock<IHealthCheckProvider>();
        mockProvider.Setup(p => p.TargetType).Returns(ResourceType.Process);

        // Setup provider to throw an exception to trigger the circuit breaker
        mockProvider
            .Setup(p => p.CheckHealthAsync(It.IsAny<Resource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Exception("Simulated failure"));

        var logger = new Mock<ILogger<HealthCheckManager>>();
        var manager = new HealthCheckManager(new[] { mockProvider.Object }, logger.Object);

        var resource = new Resource { Type = ResourceType.Process };

        // Call 5 times to trip the circuit breaker
        for (int i = 0; i < 5; i++)
        {
            var result = await manager.ExecuteCheckAsync(resource);
            Assert.Equal(HealthState.Unhealthy, result.HealthState);
        }

        // On the 6th call, the circuit breaker should be open and throw BrokenCircuitException
        // which HealthCheckManager catches and returns HealthState.Unknown
        var trippedResult = await manager.ExecuteCheckAsync(resource);

        Assert.Equal(HealthState.Unknown, trippedResult.HealthState);
        Assert.Equal("Circuit breaker open, check aborted.", trippedResult.Message);
    }
}
