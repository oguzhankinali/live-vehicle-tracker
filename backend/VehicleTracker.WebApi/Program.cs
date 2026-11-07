using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Domain.Entities;
using VehicleTracker.Domain.Enums;
using VehicleTracker.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var writer = scope.ServiceProvider.GetRequiredService<IVehiclePositionWriter>();

    var testTime = DateTime.UtcNow;
    var testBatch = new List<VehiclePositionRecord>
    {
        new()
        {
            FeedEntityId = "test-entity-1",
            VehicleId = "vehicle-101",
            Latitude = 59.9139,
            Longitude = 10.7522,
            Timestamp = testTime,
            ReceivedAtUtc = testTime,
            CurrentStatus = VehicleStopStatus.InTransitTo,
            OccupancyStatus = OccupancyStatus.ManySeatsAvailable
        },
        new()
        {
            FeedEntityId = "test-entity-2",
            VehicleId = "vehicle-102",
            Latitude = 60.3913,
            Longitude = 5.3221,
            Timestamp = testTime,
            ReceivedAtUtc = testTime,
            CurrentStatus = VehicleStopStatus.StoppedAt,
            OccupancyStatus = OccupancyStatus.Empty
        }
    };

    Console.WriteLine("==================================================");
    Console.WriteLine("BULK WRITER TESTI BASLIYOR");
    
    // 1. İlk yazım: 2 satır eklenmeli
    var firstInsert = await writer.WriteAsync(testBatch);
    Console.WriteLine($"1. Deneme (Yeni kayitlar)       : {firstInsert} satir eklendi (Beklenen: 2)");

    // 2. İkinci yazım (aynı veriler): 0 satır eklenmeli (ON CONFLICT DO NOTHING)
    var duplicateInsert = await writer.WriteAsync(testBatch);
    Console.WriteLine($"2. Deneme (Ayni kayitlar tekrar): {duplicateInsert} satir eklendi (Beklenen: 0 - Idempotent)");
    Console.WriteLine("==================================================");
}

app.MapGet("/", () => "Live Vehicle Tracker API Calisiyor!");

app.Run();