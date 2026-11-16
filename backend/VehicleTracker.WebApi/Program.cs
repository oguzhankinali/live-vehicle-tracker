
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Application.Ingestion;
using VehicleTracker.Infrastructure;
using VehicleTracker.Infrastructure.Persistence;
using VehicleTracker.WebApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructureServices(builder.Configuration);

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

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>();

if (allowedOrigins is null || allowedOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "Cors:AllowedOrigins must contain at least one trusted origin.");
}

foreach (var origin in allowedOrigins)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp &&
         uri.Scheme != Uri.UriSchemeHttps) ||
        uri.AbsolutePath != "/" ||
        !string.IsNullOrEmpty(uri.Query) ||
        !string.IsNullOrEmpty(uri.Fragment) ||
        !string.IsNullOrEmpty(uri.UserInfo) ||
        origin != uri.GetLeftPart(UriPartial.Authority))
    {
        throw new InvalidOperationException(
            "Cors:AllowedOrigins contains an invalid origin.");
    }
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 32768;
    options.StreamBufferCapacity = 10;
});

builder.Services.AddSingleton<
    VehicleTracker.WebApi.Services.SignalRVehiclePublisher>();

builder.Services.AddSingleton<IVehiclePublisher>(sp =>
    sp.GetRequiredService<
        VehicleTracker.WebApi.Services.SignalRVehiclePublisher>());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider
        .GetRequiredService<ILogger<Program>>();

    try
    {
        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        logger.LogInformation(
            "Veritabani migration'lari kontrol ediliyor ve uygulaniyor...");

        db.Database.Migrate();

        var ingestion = scope.ServiceProvider
            .GetRequiredService<IOptions<IngestionOptions>>()
            .Value;

        var chunkSeconds =
            (int)ingestion.ChunkInterval.TotalSeconds;

#pragma warning disable EF1002
        db.Database.ExecuteSqlRaw(
            $"SELECT set_chunk_time_interval('vehicle_positions', INTERVAL '{chunkSeconds} seconds');");
#pragma warning restore EF1002

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

app.UseCors();

var webSocketOptions = new WebSocketOptions();

foreach (var origin in allowedOrigins)
{
    webSocketOptions.AllowedOrigins.Add(origin);
}

app.UseWebSockets(webSocketOptions);

var swaggerEnabled =
    app.Environment.IsDevelopment() ||
    app.Configuration.GetValue<bool>("Swagger:Enabled");

if (swaggerEnabled)
{
    app.UseSwagger(c =>
    {
        c.SerializeAsV2 = true;
    });

    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "Live Vehicle Tracker API v1");

        c.RoutePrefix = "swagger";
    });

    app.MapGet("/", () => Results.Redirect("/swagger"))
        .ExcludeFromDescription();
}

app.MapVehicleEndpoints();
app.MapSystemEndpoints();

app.MapHub<VehicleTracker.WebApi.Hubs.VehicleHub>(
    "/hubs/vehicles");

app.Run();
