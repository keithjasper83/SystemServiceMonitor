using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SystemServiceMonitor.Core.Models;
using SystemServiceMonitor.Core.Repair;

using System.Runtime.InteropServices;

namespace SystemServiceMonitor.Tests;

[Collection("Sequential")]
public class ProcessResourceControllerTests : IDisposable
{
    private readonly string _originalPath;

    public ProcessResourceControllerTests()
    {
        _originalPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var testDir = AppContext.BaseDirectory;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Environment.SetEnvironmentVariable("PATH", testDir + Path.PathSeparator + _originalPath);
        }
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("PATH", _originalPath);
    }

    [Fact]
    public async Task StopAsync_ReturnsFalse_OnCommandTimeout()
    {
        var loggerMock = new Mock<ILogger<ProcessResourceController>>();
        var controller = new ProcessResourceController(loggerMock.Object);

        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var sleepCommand = isWindows ? "timeout /t 2 /nobreak" : "sleep 2";

        var resource = new Resource
        {
            Id = Guid.NewGuid().ToString(),
            DisplayName = "Test Process",
            Type = ResourceType.Process,
            StopCommand = sleepCommand, // Sleep longer than the timeout
            TimeoutSeconds = 1 // Short timeout to trigger OperationCanceledException
        };

        var result = await controller.StopAsync(resource);

        Assert.False(result);

        // Verify that a warning was logged regarding the timeout
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString()!.Contains("Command timed out")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.Once);
    }
}
