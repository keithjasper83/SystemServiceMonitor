using System;
using System.ComponentModel;
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
    public async Task StartAsync_ReturnsFalse_WhenProcessStartThrowsException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ProcessResourceController>>();
        var controller = new ProcessResourceController(mockLogger.Object);
        var resource = new Resource
        {
            Id = "test-process-1",
            Type = ResourceType.Process,
            StartCommand = "this_executable_does_not_exist_12345.exe",
            // Using an invalid WorkingDirectory reliably throws a Win32Exception
            // across platforms when Process.Start is called, even with UseShellExecute = true.
            WorkingDirectory = "/this_directory_does_not_exist_12345"
        };

        // Act
        var result = await controller.StartAsync(resource);

        // Assert
        Assert.False(result);

        // Verify logger was called with an exception
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to start Process resource test-process-1.")),
                It.IsAny<Win32Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}
