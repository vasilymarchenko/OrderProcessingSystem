using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using InventoryService.Infrastructure.Persistence;

namespace InventoryService.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for creating InventoryDbContext instances.
/// 
/// This class is ONLY used by EF Core CLI tools (dotnet ef migrations add, dotnet ef database update, etc.)
/// when generating or applying migrations. It's NOT used during normal application runtime.
/// 
/// Required because Program.cs uses top-level statements and doesn't expose a CreateHostBuilder method
/// that EF Core tooling can discover automatically.
/// 
/// The factory reads configuration from appsettings.json to avoid hardcoding connection strings.
/// </summary>
public class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        // Build configuration to read from appsettings.json
        // This mirrors the configuration setup in Program.cs to ensure consistency
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<InventoryDbContext>();
        var connectionString = configuration.GetConnectionString("InventoryDatabase");
        
        optionsBuilder.UseNpgsql(connectionString);

        return new InventoryDbContext(optionsBuilder.Options);
    }
}
