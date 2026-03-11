using Microsoft.EntityFrameworkCore;
using SystemServiceMonitor.Core.Models;

namespace SystemServiceMonitor.Core.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Resource> Resources { get; set; } = null!;
}
