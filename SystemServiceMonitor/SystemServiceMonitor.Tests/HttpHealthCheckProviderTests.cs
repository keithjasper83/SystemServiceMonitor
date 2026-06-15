using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Moq.Protected;
using SystemServiceMonitor.Core.Models;
using SystemServiceMonitor.Core.Monitoring;
using SystemServiceMonitor.Core.Monitoring.Providers;

namespace SystemServiceMonitor.Tests;

public class HttpHealthCheckProviderTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenSuccessStatusCode()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new HttpHealthCheckProvider(httpClient);
        var resource = new Resource { Type = ResourceType.Http, HealthcheckCommand = "http://valid.url" };

        var result = await provider.CheckHealthAsync(resource);

        Assert.Equal(HealthState.Healthy, result.HealthState);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenErrorStatusCode(HttpStatusCode statusCode)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new HttpHealthCheckProvider(httpClient);
        var resource = new Resource { Type = ResourceType.Http, HealthcheckCommand = "http://valid.url" };

        var result = await provider.CheckHealthAsync(resource);

        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Contains(statusCode.ToString(), result.Message);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenInvalidUrl()
    {
        var httpClient = new HttpClient();
        var provider = new HttpHealthCheckProvider(httpClient);
        // "invalid-url" will fail parsing when GetAsync attempts to use it as an absolute URI or it fails to resolve
        var resource = new Resource { Type = ResourceType.Http, HealthcheckCommand = "invalid-url" };

        var result = await provider.CheckHealthAsync(resource);

        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Contains("error", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenNetworkError()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Network unreachable"));

        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new HttpHealthCheckProvider(httpClient);
        var resource = new Resource { Type = ResourceType.Http, HealthcheckCommand = "http://valid.url" };

        var result = await provider.CheckHealthAsync(resource);

        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Contains("Network unreachable", result.Message);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenTimeoutOccurs()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new TaskCanceledException("A task was canceled."));

        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new HttpHealthCheckProvider(httpClient);
        var resource = new Resource { Type = ResourceType.Http, HealthcheckCommand = "http://valid.url", TimeoutSeconds = 1 };

        var result = await provider.CheckHealthAsync(resource);

        Assert.Equal(HealthState.Unhealthy, result.HealthState);
        Assert.Contains("A task was canceled", result.Message);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnknown_WhenNoUrl()
    {
        var httpClient = new HttpClient();
        var provider = new HttpHealthCheckProvider(httpClient);
        var resource = new Resource { Type = ResourceType.Http, HealthcheckCommand = null };
        var result = await provider.CheckHealthAsync(resource);

        Assert.Equal(HealthState.Unknown, result.HealthState);
    }
}
