using System.Diagnostics;

namespace SystemServiceMonitor.Core.Utils;

public static class ProcessHelper
{
    public static ProcessStartInfo CreateProcessStartInfo(string fileName, string arguments)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }
}
