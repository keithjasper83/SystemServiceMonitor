using System.Threading.Tasks;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.Monitoring;

public interface IHealthCheckManager
{
    Task<HealthCheckResult> ExecuteCheckAsync(Resource resource);
}
