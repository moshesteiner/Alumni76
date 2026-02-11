using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration; // Required for IConfiguration
using System.IO; // Required for Directory

namespace Bagrut_Eval.Data
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            // This method is used by the EF Core tools to create a DbContext instance at design time.
            // It needs to mimic how your DbContext is configured in your application's Program.cs (or Startup.cs).

            // Build configuration from appsettings.json
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            // Get the connection string from configuration
            var activeConnectionName = configuration.GetValue<string>("AppSettings:ActiveConnectionName")!;
            var connectionString = configuration.GetConnectionString(activeConnectionName);

            var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
            builder.UseMySql(connectionString,
                new MySqlServerVersion(new Version(8, 0, 42)) // <<< Adjust MySQL version if needed (e.g., 8.0.32 is a common example)
            );

            return new ApplicationDbContext(builder.Options);
        }
    }
}