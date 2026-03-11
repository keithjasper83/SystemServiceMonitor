using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.Monitoring;

public class HealthCheckResult
{
    public HealthState HealthState { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Output { get; set; }
}
