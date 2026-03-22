using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HelpDeskApi.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var provider = (config["Db:Provider"] ?? "sqlite").ToLowerInvariant();
        var conn = config.GetConnectionString("Default");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        if (provider == "postgres")
        {
            if (string.IsNullOrWhiteSpace(conn))
                throw new InvalidOperationException("Falta ConnectionStrings:Default para Postgres.");

            optionsBuilder.UseNpgsql(conn);
        }
        else
        {
            optionsBuilder.UseSqlite(conn ?? "Data Source=helpdesk.db");
        }

        return new AppDbContext(optionsBuilder.Options);
    }
}