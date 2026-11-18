using Azure.Storage.Blobs;
using Freitty.Application.Abstractions;
using Freitty.Application.Dashboard;
using Freitty.Application.Orders;
using Freitty.Infrastructure.Dashboard;
using Freitty.Infrastructure.Identity;
using Freitty.Infrastructure.Orders;
using Freitty.Infrastructure.Persistence;
using Freitty.Infrastructure.Persistence.Seed;
using Freitty.Infrastructure.Photos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Freitty.Infrastructure;

public static class DependencyInjection
{
    private const string PostgresConnectionStringName = "Postgres";
    private const string PhotoStorageConnectionStringName = "PhotoStorage";
    private const string PhotoContainerName = "photos";
    private const string MigrationsHistoryTable = "__ef_migrations_history";

    // The defaults, five retries of up to 100 s each, would hold the startup past its probe and the closed request
    // gate of a rebuild for minutes when the storage cannot be reached.
    private const int PhotoStorageMaxRetries = 2;
    private static readonly TimeSpan PhotoStorageNetworkTimeout = TimeSpan.FromSeconds(10);

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // One data source for both ways to the database: EF Core and the Dapper reader of the dashboard.
        services.AddNpgsqlDataSource(ReadConnectionString(configuration, PostgresConnectionStringName));
        services.AddDbContext<AppDbContext>((provider, options) => options
            .UseNpgsql(
                provider.GetRequiredService<NpgsqlDataSource>(),
                npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IOrderSearch, OrderSearch>();
        services.AddScoped<IOrderNumbers, OrderNumbers>();
        services.AddScoped<IDashboardReader, DashboardReader>();
        services.AddScoped<ICurrentUser, FirstAdminCurrentUser>();
        // Read here, like the database's, so a missing connection string stops the startup. The client is
        // thread-safe and holds no connection of its own: one serves the process.
        services.AddSingleton(new BlobContainerClient(
            ReadConnectionString(configuration, PhotoStorageConnectionStringName),
            PhotoContainerName,
            PhotoStorageOptions()));
        services.AddSingleton<IPhotoStorage, BlobPhotoStorage>();
        services.AddScoped<DatabaseSeeder>();
        services.AddSingleton<SampleData>();
        return services;
    }

    private static BlobClientOptions PhotoStorageOptions()
    {
        var options = new BlobClientOptions();
        options.Retry.MaxRetries = PhotoStorageMaxRetries;
        options.Retry.NetworkTimeout = PhotoStorageNetworkTimeout;
        return options;
    }

    private static string ReadConnectionString(IConfiguration configuration, string name)
    {
        string? connectionString = configuration.GetConnectionString(name);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"The connection string '{name}' is not configured. " +
                $"Set the ConnectionStrings__{name} environment variable.");
        }

        return connectionString;
    }
}
