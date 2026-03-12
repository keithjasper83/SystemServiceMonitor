using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Windows;
using Serilog;
using SystemServiceMonitor.Core.Data;
using SystemServiceMonitor.Core.Monitoring;
using SystemServiceMonitor.Core.Monitoring.Providers;
using SystemServiceMonitor.Core.Repair;
using SystemServiceMonitor.Core.GitHub;
using SystemServiceMonitor.Core.AI;

namespace SystemServiceMonitor.Wpf;

public partial class App : Application
{
    public static IHost? AppHost { get; private set; }

    public App()
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File("Logs/app-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        AppHost = Host.CreateDefaultBuilder()
            .ConfigureServices((hostContext, services) =>
            {
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseSqlite(hostContext.Configuration["ConnectionStrings:DefaultConnection"] ?? "Data Source=systemservicemonitor.db");
                });

                services.AddHttpClient();

                // Core Services
                services.AddTransient<IHealthCheckProvider, ProcessHealthCheckProvider>();
                services.AddTransient<IHealthCheckProvider, HttpHealthCheckProvider>();
                services.AddTransient<IHealthCheckProvider, WindowsServiceHealthCheckProvider>();
                services.AddTransient<IHealthCheckProvider, WslHealthCheckProvider>();
                services.AddTransient<IHealthCheckProvider, DockerHealthCheckProvider>();
                services.AddSingleton<IHealthCheckManager, HealthCheckManager>();

                services.AddTransient<IResourceController, ProcessResourceController>();
                services.AddTransient<IResourceController, WindowsServiceResourceController>();
                services.AddTransient<IResourceController, WslResourceController>();
                services.AddTransient<IResourceController, DockerResourceController>();
                services.AddSingleton<IRepairPolicyEngine, RepairPolicyEngine>();

                services.AddTransient<IGitHubChangeMonitor, GitHubChangeMonitor>();
                services.AddTransient<IAiDiagnosisService, AiDiagnosisService>();
                services.AddSingleton<IMcpToolExecutionEngine, McpToolExecutionEngine>();

                // Background Service
                services.AddHostedService<MonitoringEngine>();

                services.AddSingleton<MainWindow>();
            })
            .UseSerilog()
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await AppHost!.StartAsync();

        // Initialize Database
        await DatabaseInitializer.InitializeAsync(AppHost.Services);

        var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await AppHost!.StopAsync();
        AppHost.Dispose();

        base.OnExit(e);
    }
}
