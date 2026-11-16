using VehicleTracker.Infrastructure;
using VehicleTracker.Infrastructure.Initialization;
using VehicleTracker.WebApi.Configuration;
using VehicleTracker.WebApi.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddWebApiServices(builder.Configuration);

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

app.UseWebApiMiddleware();

app.MapControllers();
app.MapHub<VehicleHub>("/hubs/vehicles");

app.Run();
