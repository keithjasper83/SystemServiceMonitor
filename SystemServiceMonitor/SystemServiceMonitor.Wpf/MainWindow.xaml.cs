using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SystemServiceMonitor.Core.Data;
using SystemServiceMonitor.Core.Models;
using SystemServiceMonitor.Core.AI;
using SystemServiceMonitor.Core.Repair;
using System.Diagnostics;

namespace SystemServiceMonitor.Wpf;

using System.Collections.ObjectModel;
using System.Management;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MainWindow> _logger;
    private AiDiagnosisResponse? _currentAiDiagnosis;

    public ObservableCollection<DiscoveredResource> DiscoveredResources { get; } = new();

    public MainWindow(IServiceProvider serviceProvider, ILogger<MainWindow> logger)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _logger = logger;

        // TODO [Jules]: DO NOT TOUCH THE ICON FILE (icon.ico). It has been fixed. Replacing it or modifying it manually causes XamlParseException.

        // TODO [Jules]: Modernize UI and Window layout:
        // - Implement the Dashboard as the main visible window at all times.
        // - Add Drag & Drop (D&D) support to the Dashboard grid so users can manually set service display order.
        // - Move "Add Item" to an expandable left side-panel, making the dashboard shrink to ~50% width when open.
        // - Move AI Diagnosis output to an expandable right side-panel that can hide the left panel if necessary.
        // - Move the Log Viewer into a dockable/minimizable bottom panel with Visual Studio-style coloring (red for Errors, yellow for Warnings).
        // - Establish a unified starting list of common services/processes as placeholders or suggestions when no config exists.

        // TODO [Jules]: Implement automatic Dashboard refresh:
        // - Add a System.Windows.Threading.DispatcherTimer here.
        // - Choose a sensible tick interval (e.g., 3-5 seconds).
        // - Refresh the bounds and states on tick.

        // TODO [Jules]: Keyboard shortcuts & accessibility:
        // - Document keyboard shortcuts.
        // - Bind the TxtResourceFilter (filter text box) to respond to "Enter" as a substitute for clicking the discover/search button.
        // - Configure logical key-selectors (access keys) across menus and buttons (e.g., Alt+D for discover, Alt+A for add).

        // TODO [Jules]: Testing & CI/CD:
        // - Write extensive unit and UI/integration tests for these new behaviors.
        // - The CI/CD has been updated to build & test completely. Ensure all new code adheres strictly so it builds first time, every time.

        // Hide window initially to act as tray app
        this.WindowState = WindowState.Minimized;
        this.Hide();

        CmbResourceType.ItemsSource = Enum.GetValues(typeof(ResourceType));
        DiscoveryGrid.ItemsSource = DiscoveredResources;

        Loaded += MainWindow_Loaded;
    }

    private void CmbResourceType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Add any UI logic needed when selection changes
    }

    private void TxtResourceFilter_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            // Trigger discovery or filter search when user presses Enter
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
#pragma warning disable CA1416 // Validate platform compatibility
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
#pragma warning restore CA1416 // Validate platform compatibility
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

            // Update UI on the main thread once
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

        foreach (var item in selected)
        {
            var res = new Resource
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = item.Name,
                Type = item.Type,
                DesiredState = ResourceState.Running,
                // StartCommand is the key identifier used by health check providers and controllers
                StartCommand = item.Name
            };
            db.Resources.Add(res);
        }

        await db.SaveChangesAsync();
        await LoadResourcesAsync();
        MessageBox.Show($"Added {selected.Count} resources to Dashboard.");
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadResourcesAsync();
        await LoadLogsAsync();
    }

    private async Task LoadResourcesAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var resources = await db.Resources.ToListAsync();
            ResourceGrid.ItemsSource = resources;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading resources: {ex.Message}");
        }
    }

    private async Task LoadLogsAsync()
    {
        try
        {
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (Directory.Exists(logDir))
            {
                var latestLog = Directory.GetFiles(logDir, "app-*.txt")
                                         .OrderByDescending(f => f)
                                         .FirstOrDefault();

                if (latestLog != null)
                {
                    using var stream = new FileStream(latestLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    LogTextBox.Text = await reader.ReadToEndAsync();
                    LogTextBox.ScrollToEnd();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load logs in LoadLogsAsync.");
        }
    }

    private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        ShowDashboard();
    }

    private void MenuItem_OpenDashboard_Click(object sender, RoutedEventArgs e)
    {
        ShowDashboard();
    }

    private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void ShowDashboard()
    {
        this.Show();
        this.WindowState = WindowState.Normal;
        this.Activate();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        this.Hide();
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadResourcesAsync();
        await LoadLogsAsync();
    }

    private async void BtnAddResource_Click(object sender, RoutedEventArgs e)
    {
        var form = new ResourceFormWindow();
        if (form.ShowDialog() == true)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Resources.Add(form.Resource);
            await db.SaveChangesAsync();
            await LoadResourcesAsync();
        }
    }

    private async void BtnEditResource_Click(object sender, RoutedEventArgs e)
    {
        if (ResourceGrid.SelectedItem is Resource selected)
        {
            var form = new ResourceFormWindow(selected);
            if (form.ShowDialog() == true)
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Resources.Update(form.Resource);
                await db.SaveChangesAsync();
                await LoadResourcesAsync();
            }
        }
    }

    private async void BtnDeleteResource_Click(object sender, RoutedEventArgs e)
    {
        if (ResourceGrid.SelectedItem is Resource selected)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Resources.Remove(selected);
            await db.SaveChangesAsync();
            await LoadResourcesAsync();
        }
    }

    private void ResourceGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        BtnExecuteRepair.IsEnabled = false;
        _currentAiDiagnosis = null;
    }

    private async void BtnAiDiagnosis_Click(object sender, RoutedEventArgs e)
    {
        if (ResourceGrid.SelectedItem is not Resource selected)
        {
            MessageBox.Show("Select a resource to diagnose.");
            return;
        }

        AiLogTextBox.Text = "Requesting diagnosis from local AI...";

        // Grab recent logs
        var logContext = string.Join(Environment.NewLine, LogTextBox.Text.Split(Environment.NewLine).TakeLast(50));

        using var scope = _serviceProvider.CreateScope();
        var aiService = scope.ServiceProvider.GetRequiredService<IAiDiagnosisService>();

        _currentAiDiagnosis = await aiService.GetDiagnosisAsync(selected, logContext);

        if (_currentAiDiagnosis != null)
        {
            AiLogTextBox.Text = $"Summary:\n{_currentAiDiagnosis.Summary}\n\nRecommended Action:\n{_currentAiDiagnosis.RecommendedAction}\n\nIs Safe to Automate: {_currentAiDiagnosis.IsSafeToAutomate}";
            if (_currentAiDiagnosis.IsSafeToAutomate && !string.IsNullOrWhiteSpace(_currentAiDiagnosis.RecommendedAction))
            {
                BtnExecuteRepair.IsEnabled = true;
            }
        }
        else
        {
            AiLogTextBox.Text = "Failed to get a response from local AI. Ensure the endpoint is running at 127.0.0.1:1234.";
        }
    }

    private async void BtnExecuteRepair_Click(object sender, RoutedEventArgs e)
    {
        if (_currentAiDiagnosis != null && _currentAiDiagnosis.IsSafeToAutomate)
        {
            var result = MessageBox.Show($"Execute command?\n\n{_currentAiDiagnosis.RecommendedAction}", "Confirm AI Action", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                await ExecuteToolCommandAsync(_currentAiDiagnosis.RecommendedAction);
            }
        }
    }

    private async Task ExecuteToolCommandAsync(string command)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var mcpEngine = scope.ServiceProvider.GetRequiredService<IMcpToolExecutionEngine>();
            var (isAllowed, output) = await mcpEngine.ExecuteSafeToolAsync(command);

            AiLogTextBox.Text += $"\n\nExecution Result (Allowed: {isAllowed}):\nOutput: {output}";
        }
        catch (Exception ex)
        {
            AiLogTextBox.Text += $"\n\nFailed to execute AI command: {ex.Message}";
        }
    }
}

public class DiscoveredResource
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public ResourceType Type { get; set; }
}
