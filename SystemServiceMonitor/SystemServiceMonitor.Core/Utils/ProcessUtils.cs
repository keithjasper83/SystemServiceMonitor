using System.Diagnostics;

namespace SystemServiceMonitor.Core.Utils;

public static class ProcessUtils
{
    public static ProcessStartInfo CreateProcessStartInfo(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        bool redirectStandardError = true)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = redirectStandardError
        };

        if (!string.IsNullOrEmpty(workingDirectory))
        {
            processInfo.WorkingDirectory = workingDirectory;
        }

        return processInfo;
    }
}
