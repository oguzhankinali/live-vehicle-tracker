using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VehicleTracker.Application.Ingestion;
using VehicleTracker.Infrastructure.Persistence;

namespace VehicleTracker.Infrastructure.Initialization;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();

        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseInitializer");

        try
        {
            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            logger.LogInformation(
                "Veritabani migration'lari kontrol ediliyor ve uygulaniyor...");

            await db.Database.MigrateAsync(cancellationToken);

            var options = scope.ServiceProvider
                .GetRequiredService<IOptions<IngestionOptions>>()
                .Value;

            var chunkIntervalSeconds =
                options.ChunkInterval.TotalSeconds;

            if (chunkIntervalSeconds <= 0 ||
                chunkIntervalSeconds > int.MaxValue ||
                chunkIntervalSeconds != Math.Truncate(chunkIntervalSeconds))
            {
                throw new InvalidOperationException(
                    "Ingestion ChunkInterval must be a positive whole number of seconds.");
            }

            var chunkSeconds = (int)chunkIntervalSeconds;

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT set_chunk_time_interval('vehicle_positions', make_interval(secs => {chunkSeconds}));",
                cancellationToken);

            logger.LogInformation(
                "Veritabani migration'lari ve chunk araligi ({Seconds} sn) basariyla tamamlandi.",
                chunkSeconds);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Veritabani migration veya chunk ayarlamasi yapilirken bir hata olustu!");

            throw;
        }
    }
}
