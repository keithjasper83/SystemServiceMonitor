using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SystemServiceMonitor.Core.Data;
using SystemServiceMonitor.Core.Models;
using SystemServiceMonitor.Core.Repair;

namespace SystemServiceMonitor.Tests;

public class RepairTests
{
    private IServiceProvider CreateMockServiceProvider()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task RepairPolicyEngine_DoesNotRepair_WhenDisabled()
    {
        var mockController = new Mock<IResourceController>();
        var logger = new Mock<ILogger<RepairPolicyEngine>>();
        var engine = new RepairPolicyEngine(new[] { mockController.Object }, logger.Object, CreateMockServiceProvider());

        var resource = new Resource { AutoRepairEnabled = false };
        await engine.HandleUnhealthyResourceAsync(resource);

        mockController.Verify(c => c.RestartAsync(It.IsAny<Resource>()), Times.Never);
    }

    [Fact]
    public async Task RepairPolicyEngine_DoesNotRepair_WhenQuarantined()
    {
        var mockController = new Mock<IResourceController>();
        var logger = new Mock<ILogger<RepairPolicyEngine>>();
        var engine = new RepairPolicyEngine(new[] { mockController.Object }, logger.Object, CreateMockServiceProvider());

        var resource = new Resource { AutoRepairEnabled = true, RepairState = RepairState.Quarantined };
        await engine.HandleUnhealthyResourceAsync(resource);

        mockController.Verify(c => c.RestartAsync(It.IsAny<Resource>()), Times.Never);
    }

    [Fact]
    public async Task RepairPolicyEngine_DoesNotRepair_WhenInCooldown()
    {
        var mockController = new Mock<IResourceController>();
        mockController.Setup(c => c.TargetType).Returns(ResourceType.Process);
        mockController.Setup(c => c.RestartAsync(It.IsAny<Resource>())).ReturnsAsync(true);

        var logger = new Mock<ILogger<RepairPolicyEngine>>();
        var engine = new RepairPolicyEngine(new[] { mockController.Object }, logger.Object, CreateMockServiceProvider());

        var resource = new Resource { Type = ResourceType.Process, AutoRepairEnabled = true, CooldownSeconds = 10, MaxRetries = 3 };

        // Attempt 1
        await engine.HandleUnhealthyResourceAsync(resource);

        // Attempt 2 (should hit cooldown)
        await engine.HandleUnhealthyResourceAsync(resource);

        mockController.Verify(c => c.RestartAsync(It.IsAny<Resource>()), Times.Once);
    }

    [Fact]
    public async Task RepairPolicyEngine_DoesNotRepair_WhenDependenciesAreUnhealthy()
    {
        var mockController = new Mock<IResourceController>();
        mockController.Setup(c => c.TargetType).Returns(ResourceType.Process);

        var logger = new Mock<ILogger<RepairPolicyEngine>>();
        var serviceProvider = CreateMockServiceProvider();

        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Resources.Add(new Resource { Id = "Dep1", HealthState = HealthState.Unhealthy });
            await db.SaveChangesAsync();
        }

        var engine = new RepairPolicyEngine(new[] { mockController.Object }, logger.Object, serviceProvider);

        var resource = new Resource { Id = "MainRes", Type = ResourceType.Process, AutoRepairEnabled = true, DependencyIds = "Dep1" };
        await engine.HandleUnhealthyResourceAsync(resource);

        mockController.Verify(c => c.RestartAsync(It.IsAny<Resource>()), Times.Never);
    }

    [Fact]
    public async Task RepairPolicyEngine_DoesNotRepair_WhenNoControllerFound()
    {
        var mockController = new Mock<IResourceController>();
        mockController.Setup(c => c.TargetType).Returns(ResourceType.WindowsService);

        var logger = new Mock<ILogger<RepairPolicyEngine>>();
        var engine = new RepairPolicyEngine(new[] { mockController.Object }, logger.Object, CreateMockServiceProvider());

        var resource = new Resource { Type = ResourceType.Process, AutoRepairEnabled = true };
        await engine.HandleUnhealthyResourceAsync(resource);

        mockController.Verify(c => c.RestartAsync(It.IsAny<Resource>()), Times.Never);
    }

    [Fact]
    public async Task RepairPolicyEngine_RepairsSuccessfully_AndRecordsAttempt()
    {
        var mockController = new Mock<IResourceController>();
        mockController.Setup(c => c.TargetType).Returns(ResourceType.Process);
        mockController.Setup(c => c.RestartAsync(It.IsAny<Resource>())).ReturnsAsync(true);

        var logger = new Mock<ILogger<RepairPolicyEngine>>();
        var engine = new RepairPolicyEngine(new[] { mockController.Object }, logger.Object, CreateMockServiceProvider());

        var resource = new Resource { Type = ResourceType.Process, AutoRepairEnabled = true };
        await engine.HandleUnhealthyResourceAsync(resource);

        Assert.Equal(RepairState.Retrying, resource.RepairState);
        mockController.Verify(c => c.RestartAsync(It.IsAny<Resource>()), Times.Once);
    }

    [Fact]
    public async Task RepairPolicyEngine_TransitionsToQuarantine_WhenMaxRetriesExceeded()
    {
        var mockController = new Mock<IResourceController>();
        mockController.Setup(c => c.TargetType).Returns(ResourceType.Process);
        mockController.Setup(c => c.RestartAsync(It.IsAny<Resource>())).ReturnsAsync(false);

        var logger = new Mock<ILogger<RepairPolicyEngine>>();
        var engine = new RepairPolicyEngine(new[] { mockController.Object }, logger.Object, CreateMockServiceProvider());

        var resource = new Resource { Type = ResourceType.Process, AutoRepairEnabled = true, MaxRetries = 1, CooldownSeconds = 0 };

        // Attempt 1
        await engine.HandleUnhealthyResourceAsync(resource);
        Assert.Equal(RepairState.Retrying, resource.RepairState);

        // Advance time to bypass cooldown
        await Task.Delay(10);

        // Attempt 2 (Should fail due to max retries)
        await engine.HandleUnhealthyResourceAsync(resource);
        Assert.Equal(RepairState.Quarantined, resource.RepairState);
    }
}
