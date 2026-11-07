using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Infrastructure.Persistence;

namespace VehicleTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string 'DefaultConnection' is not configured.");
        }

        // NpgsqlDataSource: EF Core ve raw SQL yazıcımız aynı yüksek performanslı bağlantı havuzunu paylaşır.
        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));

        services.AddDbContext<ApplicationDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>()));

        services.AddSingleton<IVehiclePositionWriter, NpgsqlVehiclePositionWriter>();

        return services;
    }
}