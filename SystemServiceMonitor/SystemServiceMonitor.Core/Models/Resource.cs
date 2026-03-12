using System.ComponentModel.DataAnnotations;

namespace SystemServiceMonitor.Core.Models;

public class Resource
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string DisplayName { get; set; } = string.Empty;

    public ResourceType Type { get; set; }

    public ResourceState DesiredState { get; set; } = ResourceState.Running;
    public ResourceState ObservedState { get; set; } = ResourceState.Unknown;
    public HealthState HealthState { get; set; } = HealthState.Unknown;
    public RepairState RepairState { get; set; } = RepairState.None;

    public bool AutoRepairEnabled { get; set; } = true;

    // Commands
    public string? StartCommand { get; set; }
    public string? StopCommand { get; set; }
    public string? RestartCommand { get; set; }
    public string? HealthcheckCommand { get; set; }

    // Environment
    public string? WorkingDirectory { get; set; }
    public string? EnvironmentVariables { get; set; } // JSON serialized dictionary

    // Behavior
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
    public int CooldownSeconds { get; set; } = 60;
    public bool RequiresElevation { get; set; }

    // Optional metadata
    public string? GitHubRepoUrl { get; set; }
    public string? GitHubBranch { get; set; }
    public string? DeployedCommitHash { get; set; }
    public string? WslDistroName { get; set; }
    public string? DockerIdentifier { get; set; }

    // Dependencies
    public string? DependencyIds { get; set; } // Comma separated IDs
}
