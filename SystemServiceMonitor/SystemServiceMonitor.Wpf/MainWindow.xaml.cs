using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
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
    private Resource? _editingResource;
    private bool _isLeftPanelOpen;
    private bool _isRightPanelOpen;
    private Point _dragStartPoint;

    public ObservableCollection<DiscoveredResource> DiscoveredResources { get; } = new();

    public MainWindow(IServiceProvider serviceProvider, ILogger<MainWindow> logger)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _logger = logger;

        // NOTE: DO NOT TOUCH THE ICON FILE (icon.ico). It has been fixed. Replacing it or modifying it manually causes XamlParseException.

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
            e.Handled = true;
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
            var resources = await db.Resources.OrderBy(r => r.DisplayOrder).ToListAsync();
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
                    var logText = await reader.ReadToEndAsync();

                    LogParagraph.Inlines.Clear();
                    var lines = logText.Split(Environment.NewLine);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var run = new Run(line + Environment.NewLine);
                        if (line.Contains("[Error]", StringComparison.OrdinalIgnoreCase) || line.Contains("[ERR]", StringComparison.OrdinalIgnoreCase))
                        {
                            run.Foreground = Brushes.Red;
                        }
                        else if (line.Contains("[Warn]", StringComparison.OrdinalIgnoreCase) || line.Contains("[WRN]", StringComparison.OrdinalIgnoreCase))
                        {
                            run.Foreground = Brushes.Orange;
                        }
                        else if (line.Contains("[Fatal]", StringComparison.OrdinalIgnoreCase) || line.Contains("[FTL]", StringComparison.OrdinalIgnoreCase))
                        {
                            run.Foreground = Brushes.DarkRed;
                            run.FontWeight = FontWeights.Bold;
                        }
                        else
                        {
                            run.Foreground = Brushes.Black;
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

    private async void BtnToggleAiDiagnosis_Click(object sender, RoutedEventArgs e)
    {
        if (ResourceGrid.SelectedItem is not Resource selected)
        {
            MessageBox.Show("Select a resource to diagnose.");
            return;
        }

        ToggleRightPanel(true);
        AiLogTextBox.Text = "Requesting diagnosis from local AI...";

        // Grab recent logs
        var logContext = string.Join(Environment.NewLine, new TextRange(LogRichTextBox.Document.ContentStart, LogRichTextBox.Document.ContentEnd).Text.Split(Environment.NewLine).TakeLast(50));

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

    private void BtnToggleAddResource_Click(object sender, RoutedEventArgs e)
    {
        _editingResource = null;
        ClearForm();
        ToggleLeftPanel(true);
    }

    private void BtnToggleEditResource_Click(object sender, RoutedEventArgs e)
    {
        if (ResourceGrid.SelectedItem is Resource selected)
        {
            _editingResource = selected;
            PopulateForm(selected);
            ToggleLeftPanel(true);
        }
        else
        {
            MessageBox.Show("Select a resource to edit.");
        }
    }

    private async void BtnSaveForm_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtDisplayName.Text))
        {
            MessageBox.Show("Display name is required.");
            return;
        }

        bool isNew = _editingResource == null;
        var res = _editingResource ?? new Resource { Id = Guid.NewGuid().ToString() };

        res.DisplayName = TxtDisplayName.Text;
        res.Type = Enum.TryParse<ResourceType>(CboType.SelectedItem?.ToString(), out var parsedType) ? parsedType : ResourceType.WindowsService;
        res.StartCommand = TxtStartCommand.Text;
        res.StopCommand = TxtStopCommand.Text;
        res.RestartCommand = TxtRestartCommand.Text;
        res.HealthcheckCommand = TxtHealthcheck.Text;
        res.WorkingDirectory = TxtWorkingDir.Text;
        res.WslDistroName = TxtWslDistro.Text;
        res.DockerIdentifier = TxtDockerId.Text;
        res.DependencyIds = TxtDependencies.Text;
        res.GitHubRepoUrl = TxtGitHubRepo.Text;
        res.AutoRepairEnabled = ChkAutoRepair.IsChecked ?? false;
        res.RequiresElevation = ChkRequiresElevation.IsChecked ?? false;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (isNew)
            {
                res.DisplayOrder = db.Resources.Any() ? db.Resources.Max(r => r.DisplayOrder) + 1 : 0;
                db.Resources.Add(res);
            }
            else
            {
                db.Resources.Update(res);
            }
            await db.SaveChangesAsync();
            await LoadResourcesAsync();
            ToggleLeftPanel(false);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving resource: {ex.Message}");
        }
    }

    private void BtnCancelForm_Click(object sender, RoutedEventArgs e)
    {
        ToggleLeftPanel(false);
    }

    private void ToggleLeftPanel(bool open)
    {
        _isLeftPanelOpen = open;
        if (open)
        {
            LeftPanel.Visibility = Visibility.Visible;
            LeftPanel.Width = double.NaN;

            var grid = LeftPanel.Parent as Grid;
            if (grid != null)
            {
                grid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                grid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            }
            ToggleRightPanel(false);
        }
        else
        {
            LeftPanel.Visibility = Visibility.Collapsed;
            var grid = LeftPanel.Parent as Grid;
            if (grid != null)
            {
                grid.ColumnDefinitions[0].Width = GridLength.Auto;
                grid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            }
        }
    }

    private void ToggleRightPanel(bool open)
    {
        _isRightPanelOpen = open;
        if (open)
        {
            RightPanel.Visibility = Visibility.Visible;
            var grid = RightPanel.Parent as Grid;
            if (grid != null)
            {
                grid.ColumnDefinitions[2].Width = new GridLength(300);
            }
            ToggleLeftPanel(false);
        }
        else
        {
            RightPanel.Visibility = Visibility.Collapsed;
            var grid = RightPanel.Parent as Grid;
            if (grid != null)
            {
                grid.ColumnDefinitions[2].Width = GridLength.Auto;
            }
        }
    }

    private void ClearForm()
    {
        TxtDisplayName.Text = "";
        CboType.SelectedItem = null;
        TxtStartCommand.Text = "";
        TxtStopCommand.Text = "";
        TxtRestartCommand.Text = "";
        TxtHealthcheck.Text = "";
        TxtWorkingDir.Text = "";
        TxtWslDistro.Text = "";
        TxtDockerId.Text = "";
        TxtDependencies.Text = "";
        TxtGitHubRepo.Text = "";
        ChkAutoRepair.IsChecked = true;
        ChkRequiresElevation.IsChecked = false;

        CboType.Items.Clear();
        foreach (var t in Enum.GetValues<ResourceType>())
        {
            CboType.Items.Add(t);
        }
        CboType.SelectedIndex = 0;
    }

    private void PopulateForm(Resource res)
    {
        ClearForm();
        TxtDisplayName.Text = res.DisplayName;
        CboType.SelectedItem = res.Type;
        TxtStartCommand.Text = res.StartCommand;
        TxtStopCommand.Text = res.StopCommand;
        TxtRestartCommand.Text = res.RestartCommand;
        TxtHealthcheck.Text = res.HealthcheckCommand;
        TxtWorkingDir.Text = res.WorkingDirectory;
        TxtWslDistro.Text = res.WslDistroName;
        TxtDockerId.Text = res.DockerIdentifier;
        TxtDependencies.Text = res.DependencyIds;
        TxtGitHubRepo.Text = res.GitHubRepoUrl;
        ChkAutoRepair.IsChecked = res.AutoRepairEnabled;
        ChkRequiresElevation.IsChecked = res.RequiresElevation;
    }

    private void ResourceGrid_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void ResourceGrid_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        Point mousePos = e.GetPosition(null);
        Vector diff = _dragStartPoint - mousePos;

        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed &&
            (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
             Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
        {
            var dataGrid = sender as DataGrid;
            if (dataGrid == null || dataGrid.SelectedItem == null) return;

            var selectedResource = dataGrid.SelectedItem as Resource;
            if (selectedResource != null)
            {
                DragDrop.DoDragDrop(dataGrid, selectedResource, DragDropEffects.Move);
            }
        }
    }

    private void ResourceGrid_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(Resource)))
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private async void ResourceGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(Resource)))
        {
            var droppedResource = e.Data.GetData(typeof(Resource)) as Resource;
            var targetResource = GetObjectDataFromPoint(ResourceGrid, e.GetPosition(ResourceGrid)) as Resource;

            if (droppedResource != null && targetResource != null && droppedResource.Id != targetResource.Id)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var resources = await db.Resources.OrderBy(r => r.DisplayOrder).ToListAsync();

                    int droppedIdx = resources.FindIndex(r => r.Id == droppedResource.Id);
                    int targetIdx = resources.FindIndex(r => r.Id == targetResource.Id);

                    if (droppedIdx > -1 && targetIdx > -1)
                    {
                        var item = resources[droppedIdx];
                        resources.RemoveAt(droppedIdx);
                        resources.Insert(targetIdx, item);

                        for (int i = 0; i < resources.Count; i++)
                        {
                            resources[i].DisplayOrder = i;
                            db.Resources.Update(resources[i]);
                        }

                        await db.SaveChangesAsync();
                        await LoadResourcesAsync();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reordering: {ex.Message}");
                }
            }
        }
    }

    private object? GetObjectDataFromPoint(DataGrid source, Point point)
    {
        UIElement? element = source.InputHitTest(point) as UIElement;
        if (element != null)
        {
            object data = DependencyProperty.UnsetValue;
            while (data == DependencyProperty.UnsetValue)
            {
                data = source.ItemContainerGenerator.ItemFromContainer(element);
                if (data == DependencyProperty.UnsetValue)
                {
                    element = VisualTreeHelper.GetParent(element) as UIElement;
                }
                if (element == source || element == null)
                {
                    return null;
                }
            }
            if (data != DependencyProperty.UnsetValue)
            {
                return data;
            }
        }
        return null;
    }
}

public class DiscoveredResource
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public ResourceType Type { get; set; }
}
