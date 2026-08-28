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
    private Point _dragStartPoint;
    public ObservableCollection<Resource> DashboardResources { get; set; } = new ObservableCollection<Resource>();

    private void ResourceGrid_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void ResourceGrid_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = _dragStartPoint - mousePos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var datagrid = sender as System.Windows.Controls.DataGrid;
                var row = FindAncestor<System.Windows.Controls.DataGridRow>((DependencyObject)e.OriginalSource);

                if (row != null && row.Item != null)
                {
                    DragDrop.DoDragDrop(row, row.Item, DragDropEffects.Move);
                }
            }
        }
    }

    private void ResourceGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(Resource)))
        {
            var droppedData = e.Data.GetData(typeof(Resource)) as Resource;
            var targetRow = FindAncestor<System.Windows.Controls.DataGridRow>((DependencyObject)e.OriginalSource);
            var targetItem = targetRow?.Item as Resource;

            if (droppedData != null && targetItem != null && droppedData != targetItem)
            {
                var sourceIndex = DashboardResources.IndexOf(droppedData);
                var targetIndex = DashboardResources.IndexOf(targetItem);

                DashboardResources.Move(sourceIndex, targetIndex);

                // Save order
                SystemServiceMonitor.Wpf.Helpers.DisplayOrderHelper.SaveOrder(DashboardResources.Select(r => r.Id));
            }
        }
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        do
        {
            if (current is T ancestor) return ancestor;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        } while (current != null);
        return null;
    }

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MainWindow> _logger;
    private AiDiagnosisResponse? _currentAiDiagnosis;

    public ObservableCollection<DiscoveredResource> DiscoveredResources { get; } = new();

    public MainWindow(IServiceProvider serviceProvider, ILogger<MainWindow> logger)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _logger = logger;

        // NOTE: DO NOT TOUCH THE ICON FILE (icon.ico). It has been fixed. Replacing it or modifying it manually causes XamlParseException.

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

        CmbResourceType.ItemsSource = Enum.GetValues(typeof(ResourceType));
        CboType.ItemsSource = Enum.GetValues(typeof(ResourceType));
        DiscoveryGrid.ItemsSource = DiscoveredResources;

        Loaded += MainWindow_Loaded;
    }


    private Resource? _editingResource;

    private void PopulateForm(Resource? r)
    {
        _editingResource = r;
        if (r == null)
        {
            TxtDisplayName.Text = string.Empty;
            CboType.SelectedItem = ResourceType.WindowsService;
            TxtStartCommand.Text = string.Empty;
            TxtStopCommand.Text = string.Empty;
            TxtRestartCommand.Text = string.Empty;
            TxtHealthcheck.Text = string.Empty;
            TxtWorkingDir.Text = string.Empty;
            TxtWslDistro.Text = string.Empty;
            TxtDockerId.Text = string.Empty;
            TxtDependencies.Text = string.Empty;
            TxtGitHubRepo.Text = string.Empty;
            ChkAutoRepair.IsChecked = true;
            ChkRequiresElevation.IsChecked = false;
        }
        else
        {
            TxtDisplayName.Text = r.DisplayName;
            CboType.SelectedItem = r.Type;
            TxtStartCommand.Text = r.StartCommand;
            TxtStopCommand.Text = r.StopCommand;
            TxtRestartCommand.Text = r.RestartCommand;
            TxtHealthcheck.Text = r.HealthcheckCommand;
            TxtWorkingDir.Text = r.WorkingDirectory;
            TxtWslDistro.Text = r.WslDistroName;
            TxtDockerId.Text = r.DockerIdentifier;
            TxtDependencies.Text = r.DependencyIds;
            TxtGitHubRepo.Text = r.GitHubRepoUrl;
            ChkAutoRepair.IsChecked = r.AutoRepairEnabled;
            ChkRequiresElevation.IsChecked = r.RequiresElevation;
        }
    }

    private async void BtnFormSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtDisplayName.Text))
        {
            MessageBox.Show("Display Name is required.");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var resource = _editingResource ?? new Resource { Id = Guid.NewGuid().ToString() };

        resource.DisplayName = TxtDisplayName.Text;
        if (CboType.SelectedItem != null)
            resource.Type = (ResourceType)CboType.SelectedItem;
        resource.StartCommand = TxtStartCommand.Text;
        resource.StopCommand = TxtStopCommand.Text;
        resource.RestartCommand = TxtRestartCommand.Text;
        resource.HealthcheckCommand = TxtHealthcheck.Text;
        resource.WorkingDirectory = TxtWorkingDir.Text;
        resource.WslDistroName = TxtWslDistro.Text;
        resource.DockerIdentifier = TxtDockerId.Text;
        resource.DependencyIds = TxtDependencies.Text;
        resource.GitHubRepoUrl = TxtGitHubRepo.Text;
        resource.AutoRepairEnabled = ChkAutoRepair.IsChecked ?? false;
        resource.RequiresElevation = ChkRequiresElevation.IsChecked ?? false;

        if (_editingResource == null)
        {
            db.Resources.Add(resource);
        }
        else
        {
            db.Resources.Update(resource);
        }

        await db.SaveChangesAsync();
        await LoadResourcesAsync();

        ExpanderAddResource.IsExpanded = false;
        _editingResource = null;
    }

    private void BtnFormCancel_Click(object sender, RoutedEventArgs e)
    {
        ExpanderAddResource.IsExpanded = false;
        _editingResource = null;
    }

    private void CboType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
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
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!await db.Resources.AnyAsync())
        {
            db.Resources.AddRange(
                new Resource { Id = Guid.NewGuid().ToString(), DisplayName = "Print Spooler", Type = ResourceType.WindowsService, StartCommand = "spooler" },
                new Resource { Id = Guid.NewGuid().ToString(), DisplayName = "Windows Update", Type = ResourceType.WindowsService, StartCommand = "wuauserv" },
                new Resource { Id = Guid.NewGuid().ToString(), DisplayName = "IIS Admin Service", Type = ResourceType.WindowsService, StartCommand = "IISADMIN" }
            );
            await db.SaveChangesAsync();
        }

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

            var savedOrder = SystemServiceMonitor.Wpf.Helpers.DisplayOrderHelper.LoadOrder();
            var orderedList = resources.OrderBy(r => {
                var index = savedOrder.IndexOf(r.Id);
                return index == -1 ? int.MaxValue : index;
            }).ToList();

            DashboardResources.Clear();
            foreach (var r in orderedList)
            {
                DashboardResources.Add(r);
            }

            if (ResourceGrid.ItemsSource != DashboardResources)
            {
                ResourceGrid.ItemsSource = DashboardResources;
            }
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
                    var text = await reader.ReadToEndAsync();

                    LogParagraph.Inlines.Clear();
                    foreach (var line in text.Split(Environment.NewLine))
                    {
                        var run = new System.Windows.Documents.Run(line + Environment.NewLine);
                        if (line.Contains("ERR") || line.Contains("fail", StringComparison.OrdinalIgnoreCase))
                        {
                            run.Foreground = System.Windows.Media.Brushes.Red;
                        }
                        else if (line.Contains("WRN") || line.Contains("warn", StringComparison.OrdinalIgnoreCase))
                        {
                            run.Foreground = System.Windows.Media.Brushes.Orange;
                        }
                        LogParagraph.Inlines.Add(run);
                    }
                    LogRichTextBox.ScrollToEnd();
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

    private void BtnAddResource_Click(object sender, RoutedEventArgs e)
    {
        PopulateForm(null);
        ExpanderAddResource.IsExpanded = true;
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

        ExpanderAiDiagnosis.IsExpanded = true;
        AiLogTextBox.Text = "Requesting diagnosis from local AI...";

        // Grab recent logs
        var logContext = string.Join(Environment.NewLine, new System.Windows.Documents.TextRange(LogRichTextBox.Document.ContentStart, LogRichTextBox.Document.ContentEnd).Text.Split(Environment.NewLine).TakeLast(50));

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
