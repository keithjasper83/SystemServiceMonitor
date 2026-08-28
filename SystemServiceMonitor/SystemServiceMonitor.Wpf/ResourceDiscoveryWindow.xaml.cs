using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SystemServiceMonitor.Core.Data;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Wpf;

public partial class ResourceDiscoveryWindow : Window
{
    private readonly IServiceProvider _serviceProvider;
    public ObservableCollection<DiscoveredResource> DiscoveredResources { get; } = new();

    public ResourceDiscoveryWindow(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;

        CmbResourceType.ItemsSource = Enum.GetValues(typeof(ResourceType));
        DiscoveryGrid.ItemsSource = DiscoveredResources;
    }

    private void CmbResourceType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
    }

    private void TxtResourceFilter_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            BtnDiscover_Click(sender, new RoutedEventArgs());
        }
    }

    private async void BtnDiscover_Click(object sender, RoutedEventArgs e)
    {
        if (CmbResourceType.SelectedItem == null)
        {
            MessageBox.Show("Please select a resource type.");
            return;
        }

        var type = (ResourceType)CmbResourceType.SelectedItem;
        var filter = TxtResourceFilter.Text?.Trim();
        DiscoveredResources.Clear();

        try
        {
            var discoveredItems = await Task.Run(() =>
            {
                var tempItems = new System.Collections.Generic.List<DiscoveredResource>();
                if (type == ResourceType.WindowsService)
                {
                    if (OperatingSystem.IsWindows())
                    {
#pragma warning disable CA1416
                        using var searcher = new ManagementObjectSearcher("SELECT Name, State, Description FROM Win32_Service");
                        foreach (ManagementObject queryObj in searcher.Get())
                        {
                            using (queryObj)
                            {
                                var name = queryObj["Name"]?.ToString();
                                if (string.IsNullOrEmpty(filter) || (name != null && name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                                {
                                    tempItems.Add(new DiscoveredResource
                                    {
                                        Name = name ?? "Unknown",
                                        Status = queryObj["State"]?.ToString() ?? "Unknown",
                                        Details = queryObj["Description"]?.ToString() ?? "",
                                        Type = ResourceType.WindowsService
                                    });
                                }
                            }
                        }
#pragma warning restore CA1416
                    }
                }
                else if (type == ResourceType.Process)
                {
                    foreach (var p in Process.GetProcesses())
                    {
                        using (p)
                        {
                            if (string.IsNullOrEmpty(filter) || p.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                            {
                                tempItems.Add(new DiscoveredResource
                                {
                                    Name = p.ProcessName,
                                    Status = "Running",
                                    Details = $"PID: {p.Id}",
                                    Type = ResourceType.Process
                                });
                            }
                        }
                    }
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Discovery for {type} is not fully implemented or requires external CLI parsing.");
                    });
                }
                return tempItems;
            });

            if (discoveredItems == null) return;

            foreach (var item in discoveredItems)
            {
                DiscoveredResources.Add(item);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error discovering resources: {ex.Message}");
        }
    }

    private async void BtnAddDiscovered_Click(object sender, RoutedEventArgs e)
    {
        var selected = DiscoveryGrid.SelectedItems.Cast<DiscoveredResource>().ToList();
        if (!selected.Any())
        {
            MessageBox.Show("Select resources to add.");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Find highest display order
        int maxOrder = 0;
        if (db.Resources.Any())
        {
            maxOrder = db.Resources.Max(r => r.DisplayOrder);
        }

        foreach (var item in selected)
        {
            maxOrder++;
            var res = new Resource
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = item.Name,
                Type = item.Type,
                DesiredState = ResourceState.Running,
                StartCommand = item.Name,
                DisplayOrder = maxOrder
            };
            db.Resources.Add(res);
        }

        await db.SaveChangesAsync();
        MessageBox.Show($"Added {selected.Count} resources to Dashboard.");
        DialogResult = true;
        Close();
    }
}

public class DiscoveredResource
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public ResourceType Type { get; set; }
}
