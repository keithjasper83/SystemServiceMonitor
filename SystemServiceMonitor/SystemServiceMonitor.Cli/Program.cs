using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using SystemServiceMonitor.Core.Data;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlite("Data Source=../SystemServiceMonitor.Wpf/systemservicemonitor.db");
        });
    })
    .UseSerilog()
    .Build();

await DatabaseInitializer.InitializeAsync(host.Services);
