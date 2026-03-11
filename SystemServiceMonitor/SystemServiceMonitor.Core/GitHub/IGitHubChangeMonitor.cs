using System.Threading.Tasks;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.GitHub;

public interface IGitHubChangeMonitor
{
    Task CheckForChangesAsync(Resource resource);
}
