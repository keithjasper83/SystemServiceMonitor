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
    private readonly Mock<ILogger<GitHubChangeMonitor>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly GitHubChangeMonitor _monitor;

    public GitHubChangeMonitorTests()
    {
        _loggerMock = new Mock<ILogger<GitHubChangeMonitor>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);

        _monitor = new GitHubChangeMonitor(_httpClient, _loggerMock.Object);
    }

    [Fact]
    public async Task CheckForChangesAsync_ReturnsImmediately_WhenRepoUrlIsNull()
    {
        var resource = new Resource { GitHubRepoUrl = null };

        await _monitor.CheckForChangesAsync(resource);

        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task CheckForChangesAsync_ReturnsImmediately_WhenRepoUrlIsNotGitHub()
    {
        var resource = new Resource { GitHubRepoUrl = "https://gitlab.com/owner/repo" };

        await _monitor.CheckForChangesAsync(resource);

        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task CheckForChangesAsync_LogsInformation_WhenNewCommitDetected()
    {
        var resource = new Resource
        {
            Id = "test-id",
            GitHubRepoUrl = "https://github.com/owner/repo",
            DeployedCommitHash = "old-sha"
        };

        var responseContent = new { sha = "new-sha" };
        var json = JsonSerializer.Serialize(responseContent);

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        await _monitor.CheckForChangesAsync(resource);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("New commit detected")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckForChangesAsync_DoesNotLogInformation_WhenSameCommitDetected()
    {
        var resource = new Resource
        {
            Id = "test-id",
            GitHubRepoUrl = "https://github.com/owner/repo",
            DeployedCommitHash = "same-sha"
        };

        var responseContent = new { sha = "same-sha" };
        var json = JsonSerializer.Serialize(responseContent);

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        await _monitor.CheckForChangesAsync(resource);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckForChangesAsync_LogsWarning_WhenApiReturnsError()
    {
        var resource = new Resource
        {
            Id = "test-id",
            GitHubRepoUrl = "https://github.com/owner/repo"
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        await _monitor.CheckForChangesAsync(resource);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("GitHub API returned NotFound")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckForChangesAsync_LogsError_WhenExceptionThrown()
    {
        var resource = new Resource
        {
            Id = "test-id",
            GitHubRepoUrl = "https://github.com/owner/repo"
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        await _monitor.CheckForChangesAsync(resource);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to check GitHub changes")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
