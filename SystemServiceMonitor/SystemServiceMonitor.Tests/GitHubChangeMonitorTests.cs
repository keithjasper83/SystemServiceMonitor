using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using SystemServiceMonitor.Core.GitHub;
using SystemServiceMonitor.Core.Models;
using Xunit;

namespace SystemServiceMonitor.Tests;

public class GitHubChangeMonitorTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly Mock<ILogger<GitHubChangeMonitor>> _loggerMock;
    private readonly HttpClient _httpClient;
    private readonly GitHubChangeMonitor _monitor;

    public GitHubChangeMonitorTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_handlerMock.Object);
        _loggerMock = new Mock<ILogger<GitHubChangeMonitor>>();
        _monitor = new GitHubChangeMonitor(_httpClient, _loggerMock.Object);
    }

    [Fact]
    public async Task CheckForChangesAsync_ExitsEarly_WhenUrlIsMissing()
    {
        // Arrange
        var resource = new Resource { GitHubRepoUrl = null };

        // Act
        await _monitor.CheckForChangesAsync(resource);

        // Assert
        _handlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task CheckForChangesAsync_ExitsEarly_WhenUrlIsNotGithub()
    {
        // Arrange
        var resource = new Resource { GitHubRepoUrl = "https://gitlab.com/owner/repo" };

        // Act
        await _monitor.CheckForChangesAsync(resource);

        // Assert
        _handlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task CheckForChangesAsync_LogsWarning_WhenApiReturnsError()
    {
        // Arrange
        var resource = new Resource { Id = "resource-1", GitHubRepoUrl = "https://github.com/owner/repo", GitHubBranch = "main" };
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(response);

        // Act
        await _monitor.CheckForChangesAsync(resource);

        // Assert
        VerifyLog(_loggerMock, LogLevel.Warning, "GitHub API returned NotFound for resource resource-1", Times.Once());
    }

    [Fact]
    public async Task CheckForChangesAsync_LogsInformation_WhenNewCommitDetected()
    {
        // Arrange
        var resource = new Resource
        {
            Id = "resource-1",
            GitHubRepoUrl = "https://github.com/owner/repo",
            DeployedCommitHash = "old-sha"
        };
        var content = new { Sha = "new-sha" };
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(content))
        };

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(response);

        // Act
        await _monitor.CheckForChangesAsync(resource);

        // Assert
        VerifyLog(_loggerMock, LogLevel.Information, "New commit detected for resource resource-1. Old: old-sha, New: new-sha", Times.Once());
    }

    [Fact]
    public async Task CheckForChangesAsync_DoesNotLogInformation_WhenShaMatches()
    {
        // Arrange
        var resource = new Resource
        {
            Id = "resource-1",
            GitHubRepoUrl = "https://github.com/owner/repo",
            DeployedCommitHash = "same-sha"
        };
        var content = new { Sha = "same-sha" };
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(content))
        };

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(response);

        // Act
        await _monitor.CheckForChangesAsync(resource);

        // Assert
        VerifyLog(_loggerMock, LogLevel.Information, It.IsAny<string>(), Times.Never());
    }

    [Fact]
    public async Task CheckForChangesAsync_LogsError_WhenExceptionThrown()
    {
        // Arrange
        var resource = new Resource { Id = "resource-1", GitHubRepoUrl = "https://github.com/owner/repo" };
        var exception = new HttpRequestException("Network error");

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(exception);

        // Act
        await _monitor.CheckForChangesAsync(resource);

        // Assert
        VerifyLog(_loggerMock, LogLevel.Error, "Failed to check GitHub changes for resource resource-1", Times.Once());
    }

    private static void VerifyLog<T>(Mock<ILogger<T>> loggerMock, LogLevel level, string message, Times times)
    {
        loggerMock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => (v != null && v.ToString()!.Contains(message)) || message == null),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            times);
    }
}
