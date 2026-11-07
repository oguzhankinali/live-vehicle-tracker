using VehicleTracker.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

app.MapGet("/", () => "Live Vehicle Tracker API Calisiyor!");

app.Run();