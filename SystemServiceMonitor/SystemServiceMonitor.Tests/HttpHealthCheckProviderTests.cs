using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Moq.Protected;
using SystemServiceMonitor.Core.Models;
using SystemServiceMonitor.Core.Monitoring.Providers;

namespace SystemServiceMonitor.Tests;

public class HttpHealthCheckProviderTests
{
    private Mock<HttpMessageHandler> CreateHttpMessageHandlerMock(HttpResponseMessage responseMessage)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(responseMessage);
        return handlerMock;
    }

    private Mock<HttpMessageHandler> CreateHttpMessageHandlerExceptionMock(Exception exception)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(exception);
        return handlerMock;
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenHttpCheckSucceeds()
    {
        // Arrange
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);
        var handlerMock = CreateHttpMessageHandlerMock(responseMessage);
        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new HttpHealthCheckProvider(httpClient);

        var resource = new Resource
        {
            Type = ResourceType.Http,
            HealthcheckCommand = "https://example.com"
        };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Healthy, result.HealthState);
        Assert.Contains("successful (OK)", result.Message);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenHttpCheckFails()
    {
        // Arrange
        var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var handlerMock = CreateHttpMessageHandlerMock(responseMessage);
        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new HttpHealthCheckProvider(httpClient);

        var resource = new Resource
        {
            Type = ResourceType.Http,
            HealthcheckCommand = "https://example.com"
        };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Contains("failed (InternalServerError)", result.Message);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenExceptionThrown()
    {
        // Arrange
        var exception = new HttpRequestException("Network error");
        var handlerMock = CreateHttpMessageHandlerExceptionMock(exception);
        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new HttpHealthCheckProvider(httpClient);

        var resource = new Resource
        {
            Type = ResourceType.Http,
            HealthcheckCommand = "https://example.com"
        };

        // Act
        var result = await provider.CheckHealthAsync(resource);

        // Assert
        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Contains("HTTP check error: Network error", result.Message);
    }

    [Fact]
    public async Task CheckHealthAsync_RespectsTimeout()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();

        // We simulate a timeout by having the handler wait slightly, but the token should be cancelled
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Returns(async (HttpRequestMessage request, CancellationToken cancellationToken) =>
            {
                // This simulates the actual HTTP call taking a while, but it will throw TaskCanceledException
                // when the token is cancelled by the CancellationTokenSource in the provider.
                await Task.Delay(1000, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new HttpHealthCheckProvider(httpClient);

        var resource = new Resource
        {
            Type = ResourceType.Http,
            HealthcheckCommand = "https://example.com",
            TimeoutSeconds = 1 // Set a short timeout
        };

        // Act & Assert
        // The GetAsync inside CheckHealthAsync will throw TaskCanceledException or OperationCanceledException
        // when the internal CTS is cancelled after the timeout.
        var result = await provider.CheckHealthAsync(resource);

        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Contains("HTTP check error", result.Message);
    }
}
