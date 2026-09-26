using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api;

public class SeOnePostgresDbContextFactory : IDesignTimeDbContextFactory<SeOneDbContext>
{
    public SeOneDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.GetDirectoryName(typeof(SeOnePostgresDbContextFactory).Assembly.Location)
                       ?? Directory.GetCurrentDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var databaseProvider =
            configuration["DatabaseProvider"]?.Trim().ToLowerInvariant() ?? "sqlserver";

        var optionsBuilder = new DbContextOptionsBuilder<SeOneDbContext>();

        if (databaseProvider == "postgres")
        {
            var connectionString =
                Environment.GetEnvironmentVariable("SEONE_POSTGRES_CONNECTION")
                ?? configuration.GetConnectionString("PostgresConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "PostgreSQL connection string is not configured.");
            }

            optionsBuilder.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                    npgsqlOptions.MigrationsAssembly("SeOne.Migrations.Postgres"));
        }
        else if (databaseProvider == "sqlserver")
        {
            var connectionString =
                configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "SQL Server connection string is not configured.");
            }

            optionsBuilder.UseSqlServer(
                connectionString,
                sqlServerOptions =>
                    sqlServerOptions.MigrationsAssembly("SeOne.Infrastructure"));
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported database provider: {databaseProvider}");
        }

        return new SeOneDbContext(optionsBuilder.Options);
    }
}