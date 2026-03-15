using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.AI;

public interface IAiDiagnosisService
{
    Task<AiDiagnosisResponse?> GetDiagnosisAsync(Resource resource, string recentLogs, CancellationToken cancellationToken = default);
}

public class AiDiagnosisService : IAiDiagnosisService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiDiagnosisService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly string _aiEndpoint;

    public AiDiagnosisService(HttpClient httpClient, ILogger<AiDiagnosisService> logger, IConfiguration configuration, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        _cache = cache;
        _aiEndpoint = _configuration["AI:EndpointUrl"] ?? "http://127.0.0.1:1234/v1/chat/completions";
    }

    public async Task<AiDiagnosisResponse?> GetDiagnosisAsync(Resource resource, string recentLogs, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(resource.Id, recentLogs);

        if (_cache.TryGetValue(cacheKey, out AiDiagnosisResponse? cachedResponse))
        {
            _logger.LogInformation("Returning cached AI diagnosis for resource {ResourceId}", resource.Id);
            return cachedResponse;
        }

        try
        {
            var prompt = $@"
You are an expert Windows System Administrator. A monitored resource has failed.
Resource Name: {resource.DisplayName}
Resource Type: {resource.Type}
Start Command: {resource.StartCommand}
Recent Logs:
{recentLogs}

Provide a structured JSON diagnosis containing:
- summary: A brief explanation of the failure.
- recommendedAction: The recommended command or action to fix it (must be safe).
- isSafeToAutomate: true/false if the action is safe to run automatically.
Ensure your entire response is valid JSON matching this structure.
";

            var requestBody = new
            {
                model = "local-model",
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful sysadmin AI. Always reply in valid JSON." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.1
            };

            var response = await _httpClient.PostAsJsonAsync(_aiEndpoint, requestBody, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                // Parse OpenAI compatible response
                using var jsonDoc = JsonDocument.Parse(content);
                var aiMessage = jsonDoc.RootElement
                                       .GetProperty("choices")[0]
                                       .GetProperty("message")
                                       .GetProperty("content")
                                       .GetString();

                if (!string.IsNullOrWhiteSpace(aiMessage))
                {
                    // Clean potential markdown blocks
                    aiMessage = aiMessage.Replace("```json", "").Replace("```", "").Trim();
                    var result = JsonSerializer.Deserialize<AiDiagnosisResponse>(aiMessage, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    _logger.LogInformation("AI Diagnosis received for {Resource}. Summary: {Summary}", resource.Id, result?.Summary);

                    if (result != null)
                    {
                        // Cache the diagnosis for 5 minutes
                        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
                    }

                    return result;
                }
            }
            else
            {
                _logger.LogWarning("AI endpoint returned {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Failed to get AI diagnosis for resource {Id}", resource.Id);
        }

        return null;
    }

    private string GetCacheKey(string resourceId, string logs)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(resourceId + logs));
        return "AiDiag_" + Convert.ToBase64String(hash);
    }
}

public class AiDiagnosisResponse
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("recommendedAction")]
    public string RecommendedAction { get; set; } = string.Empty;

    [JsonPropertyName("isSafeToAutomate")]
    public bool IsSafeToAutomate { get; set; }
}
