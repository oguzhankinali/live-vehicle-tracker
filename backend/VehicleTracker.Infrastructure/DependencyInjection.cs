using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Ingestion;
using VehicleTracker.Application.Vehicles;
using VehicleTracker.Infrastructure.Ingestion;
using VehicleTracker.Infrastructure.Options;
using VehicleTracker.Infrastructure.Persistence;
using VehicleTracker.Infrastructure.Retention;
using VehicleTracker.Infrastructure.Services;

namespace VehicleTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string 'DefaultConnection' is not configured.");
        }

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));

        services.AddDbContext<ApplicationDbContext>((provider, options) =>
            options.UseNpgsql(
                provider.GetRequiredService<NpgsqlDataSource>()));

        services.AddOptions<EnturOptions>()
            .Bind(configuration.GetSection(EnturOptions.SectionName))
            .Validate(
                options => Uri.TryCreate(
                    options.FeedUrl,
                    UriKind.Absolute,
                    out var uri) && uri.Scheme == Uri.UriSchemeHttps,
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
            .Validate(
                options => options.CleanupInterval > TimeSpan.Zero,
                "Ingestion CleanupInterval must be greater than zero.")
            .ValidateOnStart();

        services.AddHttpClient<IVehicleFeedService, EnturVehicleFeedService>(
                (provider, client) =>
                {
                    var entur = provider
                        .GetRequiredService<IOptions<EnturOptions>>()
                        .Value;

                    client.Timeout =
                        TimeSpan.FromSeconds(entur.TimeoutSeconds);
                })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression =
                    DecompressionMethods.GZip |
                    DecompressionMethods.Deflate |
                    DecompressionMethods.Brotli,

                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            });

            services.AddMemoryCache();

            services.AddHttpClient<IVehicleReferenceService,EnturVehicleReferenceService>((provider, client) =>
            {
                var entur = provider
                    .GetRequiredService<IOptions<EnturOptions>>()
                    .Value;

                client.Timeout = TimeSpan.FromSeconds(8);

                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    "ET-Client-Name",
                    entur.ClientName);
            });
        // Veritabani ve canli durum servisleri
        services.AddSingleton<IVehiclePositionWriter, NpgsqlVehiclePositionWriter>();
        services.AddSingleton<IVehicleRetentionService, TimescaleRetentionService>();
        services.AddSingleton<IVehicleLiveStateStore, InMemoryVehicleLiveStateStore>();
        services.AddSingleton<ISystemMetricsService, SystemMetricsService>();

        // History use-case ve repository
        services.AddSingleton<VehicleHistoryQueryValidator>();
        services.AddSingleton<VehicleReferenceQueryValidator>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IVehicleQueryService, VehicleQueryService>();

        services.AddSingleton(provider =>
            provider.GetRequiredService<IOptions<IngestionOptions>>().Value);

        services.AddScoped<
            IVehicleHistoryRepository,
            EfCoreVehicleHistoryRepository>();

        services.AddScoped<
            IVehicleHistoryService,
            VehicleHistoryService>();

        // DB yazma kuyrugu: Application interface, Infrastructure uygulamasi
        services.AddSingleton<DbWriteQueue>();

        services.AddSingleton<IVehicleWriteQueue>(provider =>
            provider.GetRequiredService<DbWriteQueue>());

        services.AddSingleton<IVehicleWriteQueueReader>(provider =>
            provider.GetRequiredService<DbWriteQueue>());

        // Application ingestion orkestrasyonu
        services.AddSingleton<VehicleIngestionService>();

        // Arka plan isçileri
        services.AddHostedService<DbWriterWorker>();
        services.AddHostedService<VehicleIngestionWorker>();
        services.AddHostedService<VehicleRetentionWorker>();

        return services;
    }
}
