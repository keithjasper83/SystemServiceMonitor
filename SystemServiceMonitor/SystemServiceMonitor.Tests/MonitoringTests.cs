using System;
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
    private class TestWindowsServiceHealthCheckProvider : WindowsServiceHealthCheckProvider
    {
        public Func<string, string>? MockExecuteQuery { get; set; }

        protected override Task<string> ExecuteQueryAsync(string serviceName, CancellationToken cancellationToken)
        {
            if (MockExecuteQuery != null)
            {
                return Task.FromResult(MockExecuteQuery(serviceName));
            }
            return Task.FromResult(string.Empty);
        }
    }

    [Fact]
    public async Task WindowsServiceHealthCheck_ReturnsUnhealthy_OnException()
    {
        var provider = new TestWindowsServiceHealthCheckProvider
        {
            MockExecuteQuery = name => throw new Exception("Simulated process failure")
        };
        var resource = new Resource { Type = ResourceType.WindowsService, StartCommand = "Spooler" };
        var result = await provider.CheckHealthAsync(resource);

        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Equal("Failed to query Windows Service: Simulated process failure", result.Message);
    }

    [Fact]
    public async Task WindowsServiceHealthCheck_ReturnsUnhealthy_WhenServiceIsNotRunning()
    {
        var provider = new TestWindowsServiceHealthCheckProvider
        {
            MockExecuteQuery = name => "SERVICE_NAME: Spooler\n        TYPE               : 20  WIN32_SHARE_PROCESS\n        STATE              : 1  STOPPED\n                                (STOPPABLE, NOT_PAUSABLE, ACCEPTS_SHUTDOWN)\n        WIN32_EXIT_CODE    : 0  (0x0)\n        SERVICE_EXIT_CODE  : 0  (0x0)\n        CHECKPOINT         : 0x0\n        WAIT_HINT          : 0x0"
        };
        var resource = new Resource { Type = ResourceType.WindowsService, StartCommand = "Spooler" };
        var result = await provider.CheckHealthAsync(resource);

        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Equal("Service Spooler is NOT RUNNING.", result.Message);
        Assert.Contains("STATE", result.Output);
        Assert.Contains("STOPPED", result.Output);
    }

    [Fact]
    public async Task WindowsServiceHealthCheck_ReturnsHealthy_WhenServiceIsRunning()
    {
        var provider = new TestWindowsServiceHealthCheckProvider
        {
            MockExecuteQuery = name => "SERVICE_NAME: Spooler\n        TYPE               : 20  WIN32_SHARE_PROCESS\n        STATE              : 4  RUNNING\n                                (STOPPABLE, NOT_PAUSABLE, ACCEPTS_SHUTDOWN)\n        WIN32_EXIT_CODE    : 0  (0x0)\n        SERVICE_EXIT_CODE  : 0  (0x0)\n        CHECKPOINT         : 0x0\n        WAIT_HINT          : 0x0"
        };
        var resource = new Resource { Type = ResourceType.WindowsService, StartCommand = "Spooler" };
        var result = await provider.CheckHealthAsync(resource);

        Assert.Equal(HealthState.Healthy, result.HealthState);
        Assert.Equal("Service Spooler is RUNNING.", result.Message);
        Assert.Contains("STATE", result.Output);
        Assert.Contains("RUNNING", result.Output);
    }

    [Fact]
    public async Task WindowsServiceHealthCheck_ReturnsUnknown_WhenNoServiceName()
    {
        var provider = new TestWindowsServiceHealthCheckProvider();
        var resource = new Resource { Type = ResourceType.WindowsService, StartCommand = null };
        var result = await provider.CheckHealthAsync(resource);

        Assert.Equal(HealthState.Unknown, result.HealthState);
        Assert.Equal("No service name specified.", result.Message);
    }

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
}
