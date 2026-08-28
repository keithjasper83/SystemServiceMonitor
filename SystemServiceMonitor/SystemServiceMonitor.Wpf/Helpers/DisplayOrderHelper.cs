using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SystemServiceMonitor.Wpf.Helpers;

public static class DisplayOrderHelper
{
    private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "display_order.json");

    public static void SaveOrder(IEnumerable<string> ids)
    {
        try
        {
            var json = JsonSerializer.Serialize(ids);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Ignore errors for now
        }
    }

    public static List<string> LoadOrder()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var ids = JsonSerializer.Deserialize<List<string>>(json);
                return ids ?? new List<string>();
            }
        }
        catch
        {
            // Ignore errors and return empty
        }
        return new List<string>();
    }
}
