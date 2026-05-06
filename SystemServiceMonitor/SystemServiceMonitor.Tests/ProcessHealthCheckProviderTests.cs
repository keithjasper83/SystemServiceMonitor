using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;
using Moq;
using SystemServiceMonitor.Core.Models;
using SystemServiceMonitor.Core.Monitoring.Providers;

namespace SystemServiceMonitor.Tests;

public class TestableProcessHealthCheckProvider : ProcessHealthCheckProvider
{
    private readonly Func<string, Process[]> _getProcessesFunc;

    public TestableProcessHealthCheckProvider(Func<string, Process[]> getProcessesFunc)
    {
        _getProcessesFunc = getProcessesFunc;
    }

    protected override Process[] GetProcesses(string processName)
    {
        return _getProcessesFunc(processName);
    }
}

public class ProcessHealthCheckProviderTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenExceptionThrown()
    {
        var provider = new TestableProcessHealthCheckProvider(_ => throw new InvalidOperationException("Test exception"));
        var resource = new Resource { Type = ResourceType.Process, StartCommand = "dummy.exe" };

        var result = await provider.CheckHealthAsync(resource);

        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.StartsWith("Failed to check process:", result.Message);
        Assert.Contains("Test exception", result.Message);
    }
}
