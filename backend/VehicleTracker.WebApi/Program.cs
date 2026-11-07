using Microsoft.AspNetCore.Mvc;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Common.Models;
using VehicleTracker.Infrastructure;
using Microsoft.OpenApi.Models;


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
builder.Services.AddSignalR();
builder.Services.AddSingleton<VehicleTracker.WebApi.Services.SignalRVehiclePublisher>();
builder.Services.AddSingleton<IVehiclePublisher>(sp => sp.GetRequiredService<VehicleTracker.WebApi.Services.SignalRVehiclePublisher>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<VehicleTracker.WebApi.Services.SignalRVehiclePublisher>());
var app = builder.Build();

app.UseCors();

// Geliştirme ortamında Swagger UI'ı etkinleştir
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(c => 
    {
        c.SerializeAsV2 = true; 
    });
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Live Vehicle Tracker API v1");
        c.RoutePrefix = "swagger";
    });
}

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
    var query = new VehicleHistoryQuery(vehicleId, from, to, page, pageSize);
    var result = await historyService.GetHistoryAsync(query, ct);
    return Results.Ok(result);
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
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();