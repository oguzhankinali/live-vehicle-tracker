using VehicleTracker.Application.Common.Interfaces;
using VehicleTracker.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure katmanımızı Dependency Injection sistemine tek satırda kaydediyoruz
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

// TEST BLOĞU: Sunucu ayağa kalkarken Entur'dan canlı araçları çekip ekrana basalım
using (var scope = app.Services.CreateScope())
{
    var feedService = scope.ServiceProvider.GetRequiredService<IVehicleFeedService>();
    
    Console.WriteLine("==================================================");
    Console.WriteLine("Norvec Entur API'sine baglaniliyor...");
    
    var positions = await feedService.FetchLatestPositionsAsync();
    
    Console.WriteLine($"BASARILI! Toplam {positions.Count} adet canli arac verisi alindi.");
    
    var firstVehicle = positions.FirstOrDefault();
    if (firstVehicle != null)
    {
        Console.WriteLine($"Ornek Arac ID: {firstVehicle.VehicleId}");
        Console.WriteLine($"Enlem: {firstVehicle.Latitude}, Boylam: {firstVehicle.Longitude}");
        Console.WriteLine($"Hiz: {firstVehicle.Speed} m/s, Yon: {firstVehicle.Bearing}");
        Console.WriteLine($"Zaman: {firstVehicle.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
    }
    Console.WriteLine("==================================================");
}

app.MapGet("/", () => "Live Vehicle Tracker API Calisiyor!");

app.Run();