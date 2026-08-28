using System.Diagnostics;

namespace SystemServiceMonitor.Core.Utils;

public static class ProcessStartInfoHelper
{
    public static ProcessStartInfo CreateProcessStartInfo(string fileName, string arguments)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
    }
}
