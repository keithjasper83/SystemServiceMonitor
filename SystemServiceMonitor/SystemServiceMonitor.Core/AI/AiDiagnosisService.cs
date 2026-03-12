using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.AI;

public interface IAiDiagnosisService
{
    Task<AiDiagnosisResponse?> GetDiagnosisAsync(Resource resource, string recentLogs);
}

public class AiDiagnosisService : IAiDiagnosisService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiDiagnosisService> _logger;
    private const string AiEndpoint = "http://127.0.0.1:1234/v1/chat/completions";

    public AiDiagnosisService(HttpClient httpClient, ILogger<AiDiagnosisService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<AiDiagnosisResponse?> GetDiagnosisAsync(Resource resource, string recentLogs)
    {
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

            var response = await _httpClient.PostAsJsonAsync(AiEndpoint, requestBody);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

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
