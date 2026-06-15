using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SystemServiceMonitor.Core.Models;
using SystemServiceMonitor.Core.Repair;

namespace SystemServiceMonitor.Tests;

public class ProcessResourceControllerTests
{
    private class TestableProcessResourceController : ProcessResourceController
    {
        public Action<ProcessStartInfo>? StartProcessAction { get; set; }
        public Action<string>? KillProcessesByNameAction { get; set; }
        public Func<string, string?, int, Task<bool>>? RunCommandAsyncFunc { get; set; }

        public TestableProcessResourceController(ILogger<ProcessResourceController> logger) : base(logger)
        {
        }

        protected override Process? StartProcess(ProcessStartInfo processInfo)
        {
            if (StartProcessAction != null)
            {
                StartProcessAction(processInfo);
                return null; // Return null as a dummy value since we aren't using the returned process for success detection in StartAsync anyway
            }
            return base.StartProcess(processInfo);
        }

        protected override void KillProcessesByName(string processName)
        {
            if (KillProcessesByNameAction != null)
            {
                KillProcessesByNameAction(processName);
                return;
            }
            base.KillProcessesByName(processName);
        }

        protected override Task<bool> RunCommandAsync(string command, string? workingDirectory, int timeoutSeconds)
        {
            if (RunCommandAsyncFunc != null)
            {
                return RunCommandAsyncFunc(command, workingDirectory, timeoutSeconds);
            }
            return base.RunCommandAsync(command, workingDirectory, timeoutSeconds);
        }
    }

    [Fact]
    public async Task StartAsync_ReturnsFalse_WhenStartCommandIsMissing()
    {
        var loggerMock = new Mock<ILogger<ProcessResourceController>>();
        var controller = new TestableProcessResourceController(loggerMock.Object);
        var resource = new Resource { Id = "res1", StartCommand = "" };

        var result = await controller.StartAsync(resource);

        Assert.False(result);
    }

    [Fact]
    public async Task StartAsync_ReturnsTrue_WhenStartCommandIsValid()
    {
        var loggerMock = new Mock<ILogger<ProcessResourceController>>();
        var controller = new TestableProcessResourceController(loggerMock.Object);
        controller.StartProcessAction = info => { };
        var resource = new Resource { Id = "res1", StartCommand = "myapp.exe" };

        var result = await controller.StartAsync(resource);

        Assert.True(result);
    }

    [Fact]
    public async Task StartAsync_ReturnsFalse_WhenStartProcessThrows()
    {
        var loggerMock = new Mock<ILogger<ProcessResourceController>>();
        var controller = new TestableProcessResourceController(loggerMock.Object);
        controller.StartProcessAction = info => { throw new Exception("Process failed to start"); };
        var resource = new Resource { Id = "res1", StartCommand = "myapp.exe" };

        var result = await controller.StartAsync(resource);

        Assert.False(result);
    }

    [Fact]
    public async Task StopAsync_ExecutesRunCommandAsync_WhenStopCommandIsValid()
    {
        var loggerMock = new Mock<ILogger<ProcessResourceController>>();
        var controller = new TestableProcessResourceController(loggerMock.Object);
        controller.RunCommandAsyncFunc = (cmd, dir, timeout) => Task.FromResult(true);
        var resource = new Resource { Id = "res1", StopCommand = "stopapp.bat" };

        var result = await controller.StopAsync(resource);

        Assert.True(result);
    }

    [Fact]
    public async Task StopAsync_ReturnsFalse_WhenNoStopOrStartCommand()
    {
        var loggerMock = new Mock<ILogger<ProcessResourceController>>();
        var controller = new TestableProcessResourceController(loggerMock.Object);
        var resource = new Resource { Id = "res1" }; // Neither StopCommand nor StartCommand

        var result = await controller.StopAsync(resource);

        Assert.False(result);
    }

    [Fact]
    public async Task StopAsync_FallsBackToKillProcesses_WhenStopCommandIsMissing()
    {
        var loggerMock = new Mock<ILogger<ProcessResourceController>>();
        var controller = new TestableProcessResourceController(loggerMock.Object);
        controller.KillProcessesByNameAction = name => { };
        var resource = new Resource { Id = "res1", StartCommand = "myapp.exe" }; // No StopCommand

        var result = await controller.StopAsync(resource);

        Assert.True(result);
    }

    [Fact]
    public async Task StopAsync_ReturnsFalse_WhenKillProcessesThrows()
    {
        var loggerMock = new Mock<ILogger<ProcessResourceController>>();
        var controller = new TestableProcessResourceController(loggerMock.Object);
        controller.KillProcessesByNameAction = name => { throw new Exception("Kill failed"); };
        var resource = new Resource { Id = "res1", StartCommand = "myapp.exe" }; // No StopCommand

        var result = await controller.StopAsync(resource);

        Assert.False(result);
    }

    [Fact]
    public async Task RestartAsync_ExecutesRunCommandAsync_WhenRestartCommandIsValid()
    {
        var loggerMock = new Mock<ILogger<ProcessResourceController>>();
        var controller = new TestableProcessResourceController(loggerMock.Object);
        controller.RunCommandAsyncFunc = (cmd, dir, timeout) => Task.FromResult(true);
        var resource = new Resource { Id = "res1", RestartCommand = "restartapp.bat" };

        var result = await controller.RestartAsync(resource);

        Assert.True(result);
    }

    [Fact]
    public async Task RestartAsync_PerformsStopAndStart_WhenRestartCommandIsMissing()
    {
        var loggerMock = new Mock<ILogger<ProcessResourceController>>();
        var controller = new TestableProcessResourceController(loggerMock.Object);
        controller.KillProcessesByNameAction = name => { };
        controller.StartProcessAction = info => { };
        var resource = new Resource { Id = "res1", StartCommand = "myapp.exe" }; // Implicit stop via kill, then start

        var result = await controller.RestartAsync(resource);

        Assert.True(result);
    }
}
