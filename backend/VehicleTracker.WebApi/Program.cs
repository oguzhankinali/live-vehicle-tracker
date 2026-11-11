using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Ingestion;
using VehicleTracker.Infrastructure;
using VehicleTracker.Infrastructure.Persistence;
using VehicleTracker.WebApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// 1. Altyapı servislerini yükle
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

// 3. CORS desteği
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

// 4. SignalR ve Publisher Kaydı
builder.Services.AddSignalR(options => 
{
    options.MaximumReceiveMessageSize = 32768;
    options.StreamBufferCapacity = 10;
});
builder.Services.AddSingleton<VehicleTracker.WebApi.Services.SignalRVehiclePublisher>();
builder.Services.AddSingleton<IVehiclePublisher>(sp => sp.GetRequiredService<VehicleTracker.WebApi.Services.SignalRVehiclePublisher>());

var app = builder.Build();

// Otomatik Veritabanı Migration ve Dinamik Chunk Ayarı
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        logger.LogInformation("Veritabani migration'lari kontrol ediliyor ve uygulaniyor...");
        
        db.Database.Migrate();
        
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

app.UseSwagger(c => 
{
    c.SerializeAsV2 = true; 
});
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Live Vehicle Tracker API v1");
    c.RoutePrefix = "swagger";
});

// REST ve SignalR Uç Noktaları
app.MapVehicleEndpoints();
app.MapSystemEndpoints();
app.MapHub<VehicleTracker.WebApi.Hubs.VehicleHub>("/hubs/vehicles");
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();