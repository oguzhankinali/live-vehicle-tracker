using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var feedService = scope.ServiceProvider.GetRequiredService<IVehicleFeedService>();

    Console.WriteLine("==================================================");
    Console.WriteLine("ENTUR FEED NORMALIZASYON VE VERI DOGRULAMA TESTI");
    Console.WriteLine("==================================================");

    var result = await feedService.FetchLatestPositionsAsync();

    Console.WriteLine($"Toplam Basariyla Parse Edilen: {result.Records.Count:N0}");
    Console.WriteLine($"Payload Boyutu (Byte)        : {result.PayloadSizeBytes:N0}");
    Console.WriteLine($"Payload SHA256               : {result.PayloadSha256[..16]}...");
    Console.WriteLine($"HTTP + Parse Suresi          : {result.Duration.TotalMilliseconds:N0} ms");
    Console.WriteLine($"Gecersiz/Bozuk Atlanan Kayit : {result.TotalSkipped}");

    var sample = result.Records.FirstOrDefault(r => r.RouteId != null && r.StopId != null);
    if (sample != null)
    {
        Console.WriteLine("\nORNEK NORMALIZED KAYIT:");
        Console.WriteLine($"  FeedEntityId: {sample.FeedEntityId}");
        Console.WriteLine($"  VehicleId   : {sample.VehicleId}");
        Console.WriteLine($"  RouteId     : {sample.RouteId}");
        Console.WriteLine($"  DirectionId : {sample.DirectionId?.ToString() ?? "null"} (long?)");
        Console.WriteLine($"  Konum       : ({sample.Latitude}, {sample.Longitude})");
        Console.WriteLine($"  Zaman (UTC) : {sample.Timestamp:yyyy-MM-dd HH:mm:ss}");
    }
    Console.WriteLine("==================================================");
}

app.MapGet("/", () => "Live Vehicle Tracker API Calisiyor!");

app.Run();