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
}
