using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Ingestion;
using VehicleTracker.Infrastructure.Ingestion;
using VehicleTracker.Infrastructure.Options;
using VehicleTracker.Infrastructure.Persistence;
using VehicleTracker.Infrastructure.Services;

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

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));

        services.AddDbContext<ApplicationDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>()));

        services.AddOptions<EnturOptions>()
            .Bind(configuration.GetSection(EnturOptions.SectionName))
            .Validate(
                options => Uri.TryCreate(options.FeedUrl, UriKind.Absolute, out var uri)
                           && uri.Scheme == Uri.UriSchemeHttps,
                "Entur FeedUrl must be a valid HTTPS URL.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ClientName),
                "Entur ClientName must not be empty.")
            .Validate(
                options => options.TimeoutSeconds > 0,
                "Entur TimeoutSeconds must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<IngestionOptions>()
            .Bind(configuration.GetSection(IngestionOptions.SectionName))
            .Validate(
                options => options.PollInterval > TimeSpan.Zero,
                "Ingestion PollInterval must be greater than zero.")
            .Validate(
                options => options.Retention > TimeSpan.Zero,
                "Ingestion Retention must be greater than zero.")
            .ValidateOnStart();

        services
            .AddHttpClient<IVehicleFeedService, EnturVehicleFeedService>((provider, client) =>
            {
                var entur = provider.GetRequiredService<IOptions<EnturOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(entur.TimeoutSeconds);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression =
                    DecompressionMethods.GZip |
                    DecompressionMethods.Deflate |
                    DecompressionMethods.Brotli,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            });

        services.AddSingleton<IVehiclePositionWriter, NpgsqlVehiclePositionWriter>();
        
        // Uygulama ayağa kalktığında otomatik başlayan arka plan veri toplama servisi
        services.AddHostedService<VehicleIngestionWorker>();

        return services;
    }
}