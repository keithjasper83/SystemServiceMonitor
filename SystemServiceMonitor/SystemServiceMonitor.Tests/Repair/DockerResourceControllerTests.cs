using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SystemServiceMonitor.Core.Models;
using SystemServiceMonitor.Core.Repair;

namespace SystemServiceMonitor.Tests.Repair;

public class DockerResourceControllerTests
{
    private class TestableDockerResourceController : DockerResourceController
    {
        public Exception? ExceptionToThrow { get; set; }
        public bool ReturnNullProcess { get; set; }

        public TestableDockerResourceController(ILogger<DockerResourceController> logger) : base(logger)
        {
        }

        protected override Process? StartProcess(ProcessStartInfo processInfo)
        {
            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }
            if (ReturnNullProcess)
            {
                return null;
            }
            // For testing other paths, we avoid starting real process unless necessary
            return null;
        }
    }

    [Fact]
    public async Task StartAsync_ReturnsFalse_WhenContainerIdentifierIsMissing()
    {
        var loggerMock = new Mock<ILogger<DockerResourceController>>();
        var controller = new TestableDockerResourceController(loggerMock.Object);
        var resource = new Resource { DockerIdentifier = null };

        var result = await controller.StartAsync(resource);

        Assert.False(result);
    }

    [Fact]
    public async Task StartAsync_ReturnsFalse_WhenProcessStartThrows()
    {
        var loggerMock = new Mock<ILogger<DockerResourceController>>();
        var controller = new TestableDockerResourceController(loggerMock.Object)
        {
            ExceptionToThrow = new InvalidOperationException("Test exception")
        };
        var resource = new Resource { DockerIdentifier = "my-container" };

        var result = await controller.StartAsync(resource);

        Assert.False(result);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString()!.Contains("Failed to run Docker command.")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_ReturnsFalse_WhenProcessIsNull()
    {
        var loggerMock = new Mock<ILogger<DockerResourceController>>();
        var controller = new TestableDockerResourceController(loggerMock.Object)
        {
            ReturnNullProcess = true
        };
        var resource = new Resource { DockerIdentifier = "my-container" };

        var result = await controller.StartAsync(resource);

        Assert.False(result);
    }
}
