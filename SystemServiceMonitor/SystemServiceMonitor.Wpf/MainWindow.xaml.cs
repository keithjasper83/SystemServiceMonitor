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

public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;
    private AiDiagnosisResponse? _currentAiDiagnosis;

    public MainWindow(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;

        // Hide window initially to act as tray app
        this.WindowState = WindowState.Minimized;
        this.Hide();

        Loaded += MainWindow_Loaded;
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
        catch { }
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
