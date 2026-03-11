using System.Threading.Tasks;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.Repair;

public interface IRepairPolicyEngine
{
    Task HandleUnhealthyResourceAsync(Resource resource);
}
