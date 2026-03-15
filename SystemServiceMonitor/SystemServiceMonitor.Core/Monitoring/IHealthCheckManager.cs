using System.Threading.Tasks;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.Monitoring;

using System.Threading;

public interface IHealthCheckManager
{
    Task<HealthCheckResult> ExecuteCheckAsync(Resource resource, CancellationToken cancellationToken = default);
}
