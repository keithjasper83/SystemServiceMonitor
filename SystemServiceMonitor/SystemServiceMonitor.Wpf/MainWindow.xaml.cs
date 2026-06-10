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

        ResourceFormCtrl.SaveCompleted += ResourceFormCtrl_SaveCompleted;
        ResourceFormCtrl.Cancelled += ResourceFormCtrl_Cancelled;

        Loaded += MainWindow_Loaded;
    }

    private async void ResourceFormCtrl_SaveCompleted(object? sender, Resource e)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = db.Resources.Find(e.Id);
        if (existing == null)
        {
            db.Resources.Add(e);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(e);
        }

        await db.SaveChangesAsync();
        LeftPanel.Visibility = Visibility.Collapsed;
        await LoadResourcesAsync();
    }

    private void ResourceFormCtrl_Cancelled(object? sender, EventArgs e)
    {
        LeftPanel.Visibility = Visibility.Collapsed;
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

            if (!resources.Any())
            {
                var explorer = new Resource { DisplayName = "Windows Explorer", Type = ResourceType.Process, StartCommand = "explorer", DisplayOrder = 0 };
                var spooler = new Resource { DisplayName = "Print Spooler", Type = ResourceType.WindowsService, StartCommand = "Spooler", DisplayOrder = 1 };
                db.Resources.AddRange(explorer, spooler);
                await db.SaveChangesAsync();
                resources = await db.Resources.OrderBy(r => r.DisplayOrder).ToListAsync();
            }

            ResourceGrid.ItemsSource = new ObservableCollection<Resource>(resources);
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

                    LogRichTextBox.Document.Blocks.Clear();
                    var paragraph = new System.Windows.Documents.Paragraph();

                    foreach (var line in logText.Split(Environment.NewLine))
                    {
                        var run = new System.Windows.Documents.Run(line + Environment.NewLine);
                        if (line.Contains("ERR", StringComparison.OrdinalIgnoreCase) || line.Contains("fail", StringComparison.OrdinalIgnoreCase))
                        {
                            run.Foreground = System.Windows.Media.Brushes.Red;
                        }
                        else if (line.Contains("WRN", StringComparison.OrdinalIgnoreCase) || line.Contains("warn", StringComparison.OrdinalIgnoreCase))
                        {
                            run.Foreground = System.Windows.Media.Brushes.Yellow;
                        }
                        else
                        {
                            run.Foreground = System.Windows.Media.Brushes.LightGray;
                        }
                        paragraph.Inlines.Add(run);
                    }
                    LogRichTextBox.Document.Blocks.Add(paragraph);
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
        RightPanel.Visibility = Visibility.Collapsed;
        ResourceFormCtrl.LoadResource(null);
        LeftPanel.Visibility = Visibility.Visible;
    }

    private void BtnEditResource_Click(object sender, RoutedEventArgs e)
    {
        if (ResourceGrid.SelectedItem is Resource selected)
        {
            RightPanel.Visibility = Visibility.Collapsed;
            ResourceFormCtrl.LoadResource(selected);
            LeftPanel.Visibility = Visibility.Visible;
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

        LeftPanel.Visibility = Visibility.Collapsed;
        RightPanel.Visibility = Visibility.Visible;
        AiLogTextBox.Text = "Requesting diagnosis from local AI...";

        // Grab recent logs

        var textRange = new System.Windows.Documents.TextRange(LogRichTextBox.Document.ContentStart, LogRichTextBox.Document.ContentEnd);
        var logContext = string.Join(Environment.NewLine, textRange.Text.Split(Environment.NewLine).TakeLast(50));


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
    private Point _dragStartPoint;

    private void ResourceGrid_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void ResourceGrid_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            Point currentPoint = e.GetPosition(null);
            if (Math.Abs(currentPoint.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(currentPoint.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (ResourceGrid.SelectedItem != null && ResourceGrid.SelectedItem is Resource selected)
                {
                    DragDrop.DoDragDrop(ResourceGrid, selected, DragDropEffects.Move);
                }
            }
        }
    }

    private async void ResourceGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(Resource)))
        {
            var droppedData = e.Data.GetData(typeof(Resource)) as Resource;
            var target = ((FrameworkElement)e.OriginalSource).DataContext as Resource;

            if (droppedData != null && target != null && droppedData != target)
            {
                if (ResourceGrid.ItemsSource is ObservableCollection<Resource> resources)
                {
                    int droppedIndex = resources.IndexOf(droppedData);
                    int targetIndex = resources.IndexOf(target);

                    if (droppedIndex > -1 && targetIndex > -1)
                    {
                        resources.RemoveAt(droppedIndex);
                        resources.Insert(targetIndex, droppedData);

                        // Update display orders
                        for (int i = 0; i < resources.Count; i++)
                        {
                            resources[i].DisplayOrder = i;
                        }

                        // Save to DB
                        using var scope = _serviceProvider.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        foreach (var res in resources)
                        {
                            var entry = db.Resources.Find(res.Id);
                            if (entry != null)
                            {
                                entry.DisplayOrder = res.DisplayOrder;
                            }
                        }
                        await db.SaveChangesAsync();
                    }
                }
            }
        }
    }


    private async void BtnDiscoverWindow_Click(object sender, RoutedEventArgs e)
    {
        var discoveryWindow = new ResourceDiscoveryWindow(_serviceProvider);
        if (discoveryWindow.ShowDialog() == true)
        {
            await LoadResourcesAsync();
        }
    }

}
