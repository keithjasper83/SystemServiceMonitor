namespace SystemServiceMonitor.Core.Models;

public enum ResourceType
{
    WindowsService,
    Process,
    CustomCommand,
    Http,
    Wsl,
    Docker
}

public enum ResourceState
{
    Unknown,
    Stopped,
    Starting,
    Running,
    Stopping,
    Error,
    Quarantined
}

public enum HealthState
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy
}

public enum RepairState
{
    None,
    Retrying,
    Escalated,
    Quarantined
}
