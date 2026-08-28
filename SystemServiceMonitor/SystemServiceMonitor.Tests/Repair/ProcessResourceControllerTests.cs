using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SystemServiceMonitor.Core.Models;
using SystemServiceMonitor.Core.Repair;

namespace SystemServiceMonitor.Tests.Repair;

public class ProcessResourceControllerTests
{
    private class TestableProcessResourceController : ProcessResourceController
    {
        public string? LastCommandRun { get; private set; }
        public string? LastWorkingDirectoryRun { get; private set; }
        public int LastTimeoutSecondsRun { get; private set; }

        public bool RunCommandAsyncResult { get; set; } = true;

        public TestableProcessResourceController(ILogger<ProcessResourceController> logger) : base(logger)
        {
        }

        protected override Task<bool> RunCommandAsync(string command, string? workingDirectory, int timeoutSeconds)
        {
            LastCommandRun = command;
            LastWorkingDirectoryRun = workingDirectory;
            LastTimeoutSecondsRun = timeoutSeconds;

            return Task.FromResult(RunCommandAsyncResult);
        }
    }

    [Fact]
    public async Task StopAsync_UsesStopCommand_WhenProvided()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ProcessResourceController>>();
        var controller = new TestableProcessResourceController(mockLogger.Object)
        {
            RunCommandAsyncResult = true
        };

        var resource = new Resource
        {
            Id = "test-resource-id",
            Type = ResourceType.Process,
            StartCommand = "myprocess.exe",
            StopCommand = "echo stop",
            WorkingDirectory = "C:\\test",
            TimeoutSeconds = 42
        };

        // Act
        var result = await controller.StopAsync(resource);

        // Assert
        Assert.True(result);
        Assert.Equal("echo stop", controller.LastCommandRun);
        Assert.Equal("C:\\test", controller.LastWorkingDirectoryRun);
        Assert.Equal(42, controller.LastTimeoutSecondsRun);
    }
}
