using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SeOne.Infrastructure.Persistence;

namespace SeOne.Api;

public class SeOnePostgresDbContextFactory : IDesignTimeDbContextFactory<SeOneDbContext>
{
    public SeOneDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("SEONE_POSTGRES_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "SEONE_POSTGRES_CONNECTION environment variable is not set.");
        }

        var optionsBuilder =
            new DbContextOptionsBuilder<SeOneDbContext>();

        optionsBuilder.UseNpgsql(
            connectionString,
            npgsqlOptions =>
                npgsqlOptions.MigrationsAssembly("SeOne.Migrations.Postgres"));

        return new SeOneDbContext(optionsBuilder.Options);
    }
}