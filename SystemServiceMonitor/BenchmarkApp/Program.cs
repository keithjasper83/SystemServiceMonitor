using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using SystemServiceMonitor.Core.Data;
using SystemServiceMonitor.Core.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

[MemoryDiagnoser]
public class AddRangeBenchmark
{
    private DbContextOptions<AppDbContext> _options;

    [Params(10, 100, 1000)]
    public int N;

    private List<DiscoveredResource> _selected;

    [GlobalSetup]
    public void Setup()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Guid.NewGuid()}.db")
            .Options;

        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();

        _selected = Enumerable.Range(0, N).Select(i => new DiscoveredResource
        {
            Name = $"Resource {i}",
            Type = ResourceType.WindowsService
        }).ToList();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        using var db = new AppDbContext(_options);
        db.Database.EnsureDeleted();
    }

    [Benchmark(Baseline = true)]
    public void AddInLoop()
    {
        using var db = new AppDbContext(_options);
        foreach (var item in _selected)
        {
            var res = new Resource
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = item.Name,
                Type = item.Type,
                DesiredState = ResourceState.Running,
                StartCommand = item.Name
            };
            db.Resources.Add(res);
        }
    }

    [Benchmark]
    public void AddRange()
    {
        using var db = new AppDbContext(_options);
        var resourcesToAdd = new List<Resource>(_selected.Count);
        foreach (var item in _selected)
        {
            resourcesToAdd.Add(new Resource
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = item.Name,
                Type = item.Type,
                DesiredState = ResourceState.Running,
                StartCommand = item.Name
            });
        }
        db.Resources.AddRange(resourcesToAdd);
    }
}

public class DiscoveredResource
{
    public string Name { get; set; } = string.Empty;
    public ResourceType Type { get; set; }
}

public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<AddRangeBenchmark>();
    }
}
