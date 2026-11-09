using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Common.Models;
using VehicleTracker.Application.Ingestion;
using VehicleTracker.Infrastructure;
using VehicleTracker.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. Altyapı servislerini (Postgres, Ingestion, Retention, Metrics, Cache) yükle
builder.Services.AddInfrastructureServices(builder.Configuration);

// 2. Swagger / OpenAPI desteği
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Live Vehicle Tracker API",
        Version = "v1",
        Description = "Entur GTFS-Realtime Canli Arac Takip Platformu"
    });
});

// 3. CORS desteği (Frontend Next.js entegrasyonu için)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetIsOriginAllowed(_ => true);
    });
});

// SignalR ve Publisher Kaydı
builder.Services.AddSignalR(options => 
{
    options.MaximumReceiveMessageSize = 32768;
    options.StreamBufferCapacity = 10;
});
builder.Services.AddSingleton<VehicleTracker.WebApi.Services.SignalRVehiclePublisher>();
builder.Services.AddSingleton<IVehiclePublisher>(sp => sp.GetRequiredService<VehicleTracker.WebApi.Services.SignalRVehiclePublisher>());

var app = builder.Build();

// DOCKER İÇİN OTOMATİK VERİTABANI MİGRATİON'I VE DİNAMİK CHUNK AYARI
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        logger.LogInformation("Veritabani migration'lari kontrol ediliyor ve uygulaniyor...");
        
        // 1. Önce tabloları oluştur
        db.Database.Migrate();
        
        // 2. Tablo oluştuktan hemen sonra chunk aralığını config'den dinamik olarak ayarla
        var ingestion = scope.ServiceProvider.GetRequiredService<IOptions<IngestionOptions>>().Value;
        var chunkSeconds = (int)ingestion.ChunkInterval.TotalSeconds;
        #pragma warning disable EF1002 // chunkSeconds int tipindedir, SQL injection riski yoktur
        db.Database.ExecuteSqlRaw($"SELECT set_chunk_time_interval('vehicle_positions', INTERVAL '{chunkSeconds} seconds');");
        #pragma warning restore EF1002

        logger.LogInformation("Veritabani migration'lari ve chunk araligi ({Seconds} sn) basariyla tamamlandi.", chunkSeconds);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Veritabani migration veya chunk ayarlamasi yapilirken bir hata olustu!");
        throw;
    }
}

app.UseCors();

// Şartname 3.2: Değerlendiricinin konteyner üzerinden test edebilmesi için Swagger UI her ortamda açık
// Swagger UI, Swashbuckle'ın ürettiği openapi 3.0.4'ü render edemiyor; V2'ye sabitlendi.
app.UseSwagger(c => 
{
    c.SerializeAsV2 = true; 
});
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Live Vehicle Tracker API v1");
    c.RoutePrefix = "swagger";
});

// -----------------------------------------------------------------------------
// REST Uç Noktaları (Minimal API)
// -----------------------------------------------------------------------------

var vehiclesGroup = app.MapGroup("/api/vehicles").WithTags("Vehicles");

// 1. Uç Nokta: Tüm araçların anlık en son konumu (RAM'den anında döner)
vehiclesGroup.MapGet("/latest", (
    [FromServices] IVehicleLiveStateStore store,
    [FromQuery] string? routeId,
    [FromQuery] string? search) =>
{
    var records = store.GetAllLatest();

    if (!string.IsNullOrWhiteSpace(routeId))
    {
        records = records.Where(r => string.Equals(r.RouteId, routeId, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    if (!string.IsNullOrWhiteSpace(search))
    {
        records = records.Where(r => 
            (r.VehicleId != null && r.VehicleId.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
            (r.RouteId != null && r.RouteId.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
            (r.VehicleLabel != null && r.VehicleLabel.Contains(search, StringComparison.OrdinalIgnoreCase))
        ).ToList();
    }

    return Results.Ok(records);
})
.WithName("GetLatestVehiclePositions")
.WithSummary("Tum aktif araclarin anlik guncel durumlarini doner.");

// 2. Uç Nokta: Bir aracın zaman aralığına göre sayfalanmış konum geçmişi
vehiclesGroup.MapGet("/{vehicleId}/history", async (
    [FromRoute] string vehicleId,
    [FromServices] IVehicleHistoryService historyService,
    [FromQuery] DateTime? from,
    [FromQuery] DateTime? to,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 50,
    CancellationToken ct = default) =>
{
    try
    {
        var query = new VehicleHistoryQuery(vehicleId, from, to, page, pageSize);
        var result = await historyService.GetHistoryAsync(query, ct);
        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("GetVehicleHistory")
.WithSummary("Secilen aracin son 3 saatlik filtrelenebilir konum gecmisini sayfali doner.");

// 3. Uç Nokta: Sistem Durumu ve Metrikleri
var systemGroup = app.MapGroup("/api/system").WithTags("System");

systemGroup.MapGet("/status", ([FromServices] ISystemMetricsService metrics) =>
{
    var status = metrics.GetStatus();
    return Results.Ok(status);
})
.WithName("GetSystemStatus")
.WithSummary("Sistem durumu, son basarili okuma, saatlik yazim ve hata metriklerini doner.");

// SignalR Hub Endpoint'i
app.MapHub<VehicleTracker.WebApi.Hubs.VehicleHub>("/hubs/vehicles");
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();