using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HelpDeskApi.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Misma conexión que usarás en Program.cs
        optionsBuilder.UseSqlite("Data Source=helpdesk.db");

        return new AppDbContext(optionsBuilder.Options);
    }
}