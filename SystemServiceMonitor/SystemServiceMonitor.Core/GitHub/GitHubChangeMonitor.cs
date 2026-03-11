using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.GitHub;

public class GitHubChangeMonitor : IGitHubChangeMonitor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubChangeMonitor> _logger;

    public GitHubChangeMonitor(HttpClient httpClient, ILogger<GitHubChangeMonitor> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Required for GitHub API
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SystemServiceMonitor-Client");
    }

    public async Task CheckForChangesAsync(Resource resource)
    {
        if (string.IsNullOrWhiteSpace(resource.GitHubRepoUrl)) return;

        try
        {
            var branch = string.IsNullOrWhiteSpace(resource.GitHubBranch) ? "main" : resource.GitHubBranch;
            var repoPath = GetRepoPath(resource.GitHubRepoUrl);

            if (repoPath == null) return;

            var url = $"https://api.github.com/repos/{repoPath}/commits/{branch}";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GitHubCommitResponse>();
                if (result != null && result.Sha != resource.DeployedCommitHash)
                {
                    _logger.LogInformation("New commit detected for resource {Id}. Old: {Old}, New: {New}", resource.Id, resource.DeployedCommitHash, result.Sha);
                    // In a full implementation, we would set an "UpdateAvailable" flag or trigger a redeploy.
                    // For V1, we log the delta and optionally store it.
                }
            }
            else
            {
                _logger.LogWarning("GitHub API returned {StatusCode} for resource {Id}", response.StatusCode, resource.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check GitHub changes for resource {Id}", resource.Id);
        }
    }

    private string? GetRepoPath(string url)
    {
        // simplistic parsing for https://github.com/owner/repo
        var uri = new Uri(url);
        if (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return uri.AbsolutePath.TrimStart('/');
        }
        return null;
    }

    private class GitHubCommitResponse
    {
        public string? Sha { get; set; }
    }
}
