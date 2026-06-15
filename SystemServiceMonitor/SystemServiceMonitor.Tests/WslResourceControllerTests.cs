using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SystemServiceMonitor.Core.Models;
using SystemServiceMonitor.Core.Repair;

namespace SystemServiceMonitor.Tests;

public class WslResourceControllerTests
{
    private class TestableWslResourceController : WslResourceController
    {
        public Func<ProcessStartInfo, Process?>? StartProcessOverride { get; set; }

        public TestableWslResourceController(ILogger<WslResourceController> logger) : base(logger)
        {
        }

        protected override Process? StartProcess(ProcessStartInfo info)
        {
            if (StartProcessOverride != null)
            {
                return StartProcessOverride(info);
            }
            return base.StartProcess(info);
        }
    }

    [Fact]
    public async Task StartAsync_ReturnsFalse_WhenProcessStartThrowsException()
    {
        var loggerMock = new Mock<ILogger<WslResourceController>>();
        var controller = new TestableWslResourceController(loggerMock.Object)
        {
            StartProcessOverride = info => throw new Exception("Simulated process start failure")
        };
        var resource = new Resource { WslDistroName = "Ubuntu", StartCommand = "service start" };

        var result = await controller.StartAsync(resource);

        Assert.False(result);
    }

    [Fact]
    public async Task StartAsync_ReturnsFalse_WhenCommandIsMissing()
    {
        var loggerMock = new Mock<ILogger<WslResourceController>>();
        var controller = new WslResourceController(loggerMock.Object);
        var resource = new Resource { WslDistroName = "Ubuntu", StartCommand = "" };

        var result = await controller.StartAsync(resource);

        Assert.False(result);
    }
}
