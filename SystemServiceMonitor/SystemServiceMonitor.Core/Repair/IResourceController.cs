using System.Threading.Tasks;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.Repair;

public interface IResourceController
{
    ResourceType TargetType { get; }
    Task<bool> StartAsync(Resource resource);
    Task<bool> StopAsync(Resource resource);
    Task<bool> RestartAsync(Resource resource);
}
