using System.Threading.Tasks;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.Monitoring;

public interface IHealthCheckProvider
{
    ResourceType TargetType { get; }
    Task<HealthCheckResult> CheckHealthAsync(Resource resource);
}
