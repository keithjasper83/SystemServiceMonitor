using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SystemServiceMonitor.Core.Models;
using SystemServiceMonitor.Core.Repair;

namespace SystemServiceMonitor.Tests;

public class ProcessResourceControllerTests
{
    [Fact]
    public async Task StartAsync_ReturnsFalse_WhenExceptionIsThrown()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<ProcessResourceController>>();
        var controller = new ProcessResourceController(loggerMock.Object);
        var resource = new Resource
        {
            Id = "test-resource",
            Type = ResourceType.Process,
            StartCommand = "echo", // valid command to avoid file not found from 'echo' itself if possible, though 'echo' works
            WorkingDirectory = "/invalid/path/that/does/not/exist/12345" // this triggers an exception
        };

        // Act
        var result = await controller.StartAsync(resource);

        // Assert
        Assert.False(result);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString()!.Contains("Failed to start Process resource test-resource.")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
