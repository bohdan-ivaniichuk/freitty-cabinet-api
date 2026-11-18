using Freitty.Application.Abstractions;
using Freitty.Infrastructure.Identity;
using Freitty.Infrastructure.Persistence;
using Freitty.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Freitty.Infrastructure;

public static class DependencyInjection
{
    private const string ConnectionStringName = "Postgres";
    private const string MigrationsHistoryTable = "__ef_migrations_history";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = ReadConnectionString(configuration);
        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IOrderSearch, OrderSearch>();
        services.AddScoped<ICurrentUser, FirstAdminCurrentUser>();
        services.AddScoped<DatabaseSeeder>();
        return services;
    }

    private static string ReadConnectionString(IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"The connection string '{ConnectionStringName}' is not configured. " +
                $"Set the ConnectionStrings__{ConnectionStringName} environment variable.");
        }

        return connectionString;
    }
}
