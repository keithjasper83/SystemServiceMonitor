using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SystemServiceMonitor.Core.Data;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Tests;

public class DatabaseInitializerTests
{
    [Fact]
    public async Task CanInitializeDatabase()
    {
        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("TestDatabase"));

        var mockLoggerFactory = new Mock<ILoggerFactory>();
        mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        services.AddSingleton<ILoggerFactory>(mockLoggerFactory.Object);

        var serviceProvider = services.BuildServiceProvider();

        // InMemory doesn't support migrations but Database.EnsureCreatedAsync() does. Let's just create a test to verify context.
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();

        context.Resources.Add(new Resource { DisplayName = "Test Resource", Type = ResourceType.Process });
        await context.SaveChangesAsync();

        var resources = await context.Resources.ToListAsync();
        Assert.Single(resources);
        Assert.Equal("Test Resource", resources[0].DisplayName);
    }

    [Fact]
    public async Task InitializeAsync_LogsErrorAndThrows_WhenMigrationFails()
    {
        var services = new ServiceCollection();

        // Use an invalid SQLite path to force MigrateAsync to fail
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite("Data Source=/:/invalid.db"));

        var mockLogger = new Mock<ILogger>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);
        services.AddSingleton<ILoggerFactory>(mockLoggerFactory.Object);

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var exception = await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(
            () => DatabaseInitializer.InitializeAsync(serviceProvider));

        // Assert
        Assert.Contains("unable to open database file", exception.Message, System.StringComparison.OrdinalIgnoreCase);

        // Verify the logger recorded the error
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("An error occurred while migrating the database.")),
                It.IsAny<System.Exception>(),
                It.Is<System.Func<It.IsAnyType, System.Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}
