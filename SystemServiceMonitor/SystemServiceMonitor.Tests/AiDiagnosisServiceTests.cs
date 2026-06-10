using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using SystemServiceMonitor.Core.AI;
using SystemServiceMonitor.Core.Models;
using Xunit;

namespace SystemServiceMonitor.Tests;

public class AiDiagnosisServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<AiDiagnosisService>> _loggerMock;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly Resource _testResource;

    public AiDiagnosisServiceTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);

        _loggerMock = new Mock<ILogger<AiDiagnosisService>>();

        var inMemorySettings = new System.Collections.Generic.Dictionary<string, string> {
            {"AI:EndpointUrl", "http://test-ai-endpoint/v1/chat/completions"}
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        _cache = new MemoryCache(new MemoryCacheOptions());

        _testResource = new Resource
        {
            Id = "test-resource-id",
            DisplayName = "Test Resource",
            Type = ResourceType.Process,
            StartCommand = "test-command"
        };
    }


    private void SetupHttpMessageHandler(HttpStatusCode statusCode, string content)
    {
        var response = new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = new StringContent(content)
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }

    private string CreateOpenAiResponse(string innerContent)
    {
        var responseObj = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = innerContent
                    }
                }
            }
        };
        return JsonSerializer.Serialize(responseObj);
    }

    [Fact]
    public async Task GetDiagnosisAsync_ReturnsDiagnosis_OnSuccess()
    {
        // Arrange
        var service = new AiDiagnosisService(_httpClient, _loggerMock.Object, _configuration, _cache);
        var expectedDiagnosis = new AiDiagnosisResponse
        {
            Summary = "Test summary",
            RecommendedAction = "Test action",
            IsSafeToAutomate = true
        };
        var innerJson = JsonSerializer.Serialize(expectedDiagnosis);
        var httpResponseContent = CreateOpenAiResponse(innerJson);
        SetupHttpMessageHandler(HttpStatusCode.OK, httpResponseContent);

        // Act
        var result = await service.GetDiagnosisAsync(_testResource, "test logs");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedDiagnosis.Summary, result.Summary);
        Assert.Equal(expectedDiagnosis.RecommendedAction, result.RecommendedAction);
        Assert.True(result.IsSafeToAutomate);
    }

    [Fact]
    public async Task GetDiagnosisAsync_ReturnsCachedResponse_WhenCalledTwice()
    {
        // Arrange
        var service = new AiDiagnosisService(_httpClient, _loggerMock.Object, _configuration, _cache);
        var expectedDiagnosis = new AiDiagnosisResponse
        {
            Summary = "Cached summary",
            RecommendedAction = "Cached action",
            IsSafeToAutomate = false
        };
        var innerJson = JsonSerializer.Serialize(expectedDiagnosis);
        var httpResponseContent = CreateOpenAiResponse(innerJson);
        SetupHttpMessageHandler(HttpStatusCode.OK, httpResponseContent);

        // Act
        var result1 = await service.GetDiagnosisAsync(_testResource, "test logs");
        var result2 = await service.GetDiagnosisAsync(_testResource, "test logs");

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(result1.Summary, result2.Summary);

        // Verify HTTP handler was only called once
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task GetDiagnosisAsync_CleansMarkdownBlocks_Properly()
    {
        // Arrange
        var service = new AiDiagnosisService(_httpClient, _loggerMock.Object, _configuration, _cache);
        var expectedDiagnosis = new AiDiagnosisResponse
        {
            Summary = "Markdown summary",
            RecommendedAction = "Markdown action",
            IsSafeToAutomate = true
        };
        var innerJson = JsonSerializer.Serialize(expectedDiagnosis);
        var innerJsonWithMarkdown = $"```json\n{innerJson}\n```";
        var httpResponseContent = CreateOpenAiResponse(innerJsonWithMarkdown);
        SetupHttpMessageHandler(HttpStatusCode.OK, httpResponseContent);

        // Act
        var result = await service.GetDiagnosisAsync(_testResource, "test logs");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedDiagnosis.Summary, result.Summary);
        Assert.Equal(expectedDiagnosis.RecommendedAction, result.RecommendedAction);
        Assert.True(result.IsSafeToAutomate);
    }



    [Fact]
    public async Task GetDiagnosisAsync_ReturnsNull_OnNonSuccessStatusCode()
    {
        // Arrange
        var service = new AiDiagnosisService(_httpClient, _loggerMock.Object, _configuration, _cache);
        SetupHttpMessageHandler(HttpStatusCode.InternalServerError, "Error");

        // Act
        var result = await service.GetDiagnosisAsync(_testResource, "test logs");

        // Assert
        Assert.Null(result);

        // Verify logger was called with Warning
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("AI endpoint returned InternalServerError")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task GetDiagnosisAsync_ReturnsNull_OnException()
    {
        // Arrange
        var service = new AiDiagnosisService(_httpClient, _loggerMock.Object, _configuration, _cache);

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await service.GetDiagnosisAsync(_testResource, "test logs");

        // Assert
        Assert.Null(result);

        // Verify logger was called with Error
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Failed to get AI diagnosis for resource {_testResource.Id}")),
                It.IsAny<HttpRequestException>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task GetDiagnosisAsync_ReturnsNull_OnMalformedJsonResponse()
    {
        // Arrange
        var service = new AiDiagnosisService(_httpClient, _loggerMock.Object, _configuration, _cache);
        // Invalid JSON missing "choices"
        var malformedJson = "{\"invalid\": \"response\"}";
        SetupHttpMessageHandler(HttpStatusCode.OK, malformedJson);

        // Act
        var result = await service.GetDiagnosisAsync(_testResource, "test logs");

        // Assert
        Assert.Null(result); // The exception inside GetDiagnosisAsync should be caught, returning null

        // Verify logger was called with Error due to parsing exception
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Failed to get AI diagnosis for resource {_testResource.Id}")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}
